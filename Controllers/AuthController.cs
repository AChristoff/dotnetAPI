using System.Security.Cryptography;
using DotnetAPI.Data;
using DotnetAPI.Models.DTOs;
using DotnetAPI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Net;
using DotnetAPI.Services;
using System;

namespace DotnetAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly DataContextDapper _dapper;
        private readonly AuthHelper _authHelper;
        private readonly EmailService _emailService;

        public AuthController(IConfiguration config, EmailService emailService)
        {
            _dapper = new DataContextDapper(config);
            _authHelper = new AuthHelper();
            _emailService = emailService;
        }
        /**
        *
        * Register USER
        *
        * POST: /auth/register
        * @return IActionResult
        *
        */
        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Register(UserForRegistrationDto userForRegistration)
        {
            if (userForRegistration.Password != userForRegistration.PasswordConfirm)
                return BadRequest("Passwords do not match!");

            // Check if user already exists
            string sqlCheckUserExists = "SELECT Email FROM TutorialAppSchema.Auth WHERE Email = @Email";
            var checkUserParams = new Dapper.DynamicParameters();
            checkUserParams.Add("Email", userForRegistration.Email);

            var existingUsers = _dapper.LoadData<string>(sqlCheckUserExists, checkUserParams);
            if (existingUsers.Any())
                return Conflict("User with this email already exists!");

            // Generate password salt and hash
            byte[] passwordSalt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetNonZeroBytes(passwordSalt);
            }
            byte[] passwordHash = _authHelper.GetPasswordHash(userForRegistration.Password, passwordSalt);

            // Insert the user with Active = 0 (inactive)
            string sqlAddUser = @"
                INSERT INTO TutorialAppSchema.Users (FirstName, LastName, Email, Gender, Active)
                VALUES (@FirstName, @LastName, @Email, @Gender, 0)";

            var userParams = new Dapper.DynamicParameters();
            userParams.Add("FirstName", userForRegistration.FirstName);
            userParams.Add("LastName", userForRegistration.LastName);
            userParams.Add("Email", userForRegistration.Email);
            userParams.Add("Gender", userForRegistration.Gender);

            if (!_dapper.ExecuteSql(sqlAddUser, userParams))
                throw new Exception("Failed to add user.");

            // Step 1: Generate OTP
            var otp = new Random().Next(100000, 999999); // 6-digit OTP
            var otpExpirationTime = DateTime.UtcNow.AddMinutes(30); // OTP valid for 30 minutes

            // Step 2: Save OTP, password hash, and password salt in the Auth table
            string sqlAddAuth = @"
                INSERT INTO TutorialAppSchema.Auth (Email, PasswordHash, PasswordSalt, OTP, OTPExpirationTime)
                VALUES (@Email, @PasswordHash, @PasswordSalt, @OTP, @OTPExpirationTime)";

            var authParams = new Dapper.DynamicParameters();
            authParams.Add("Email", userForRegistration.Email);
            authParams.Add("PasswordHash", passwordHash);
            authParams.Add("PasswordSalt", passwordSalt);
            authParams.Add("OTP", otp);
            authParams.Add("OTPExpirationTime", otpExpirationTime);

            if (!_dapper.ExecuteSql(sqlAddAuth, authParams))
                throw new Exception("Failed to register user authentication.");

            // Step 3: Send the OTP to the user's email using EmailService
            string subject = "Account Verification OTP";
            string body = $"Your OTP Code is: {otp}. \nIt is valid for 30 minutes.";

            try
            {
                _emailService.SendEmailAsync(userForRegistration.Email, subject, body).Wait();
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Console.WriteLine($"Failed to send OTP email: {ex.Message}");
                return StatusCode(500, "Failed to send OTP email. Please try again later.");
            }

            return Ok("Registration successful. Please check your email for the OTP to confirm your account.");
        }
        /**
        *
        * Verify Email - OTP
        *
        * POST: /auth/verify-email
        * @return IActionResult
        *
        */
        [AllowAnonymous]
        [HttpPost("verify-email")]
        public IActionResult VerifyOtp(UserEmailVerificationDto otpVerification)
        {
            // Query to check the OTP and expiration time
            string sqlCheckOTP = @"
                SELECT OTP, OTPExpirationTime FROM TutorialAppSchema.Auth 
                WHERE Email = @Email";

            var checkOtpParams = new Dapper.DynamicParameters();
            checkOtpParams.Add("Email", otpVerification.Email);

            var otpData = _dapper.LoadData<dynamic>(sqlCheckOTP, checkOtpParams).FirstOrDefault();

            if (otpData == null)
                return NotFound("OTP not found for this email.");

            if (otpData.OTP != otpVerification.OTP)
                return BadRequest("Invalid OTP.");

            if (DateTime.UtcNow > otpData.OTPExpirationTime)
                return BadRequest("OTP has expired.");

            // OTP is valid, so activate the user
            string sqlActivateUser = @"
                UPDATE TutorialAppSchema.Users
                SET Active = 1
                WHERE Email = @Email";

            var activateUserParams = new Dapper.DynamicParameters();
            activateUserParams.Add("Email", otpVerification.Email);

            if (!_dapper.ExecuteSql(sqlActivateUser, activateUserParams))
                throw new Exception("Failed to activate user.");

            // Clear OTP and expiration time after successful verification
            string sqlClearOTP = @"
                UPDATE TutorialAppSchema.Auth
                SET OTP = NULL, OTPExpirationTime = NULL
                WHERE Email = @Email";

            var clearOtpParams = new Dapper.DynamicParameters();
            clearOtpParams.Add("Email", otpVerification.Email);

            _dapper.ExecuteSql(sqlClearOTP, clearOtpParams);

            return Ok("User successfully verified and activated.");
        }

        /**
        *
        * Request New OTP
        *
        * POST: /auth/refresh-otp
        * @return IActionResult
        *
        */
        [AllowAnonymous]
        [HttpPost("refresh-otp")]
        public IActionResult RequestNewOtp(UserEmailDto userEmailDto)
        {
            // Step 1: Check if the user is already verified (Active = 1)
            string sqlCheckUserActive = @"
                SELECT Active FROM TutorialAppSchema.Users
                WHERE Email = @Email";

            var checkUserParams = new Dapper.DynamicParameters();
            checkUserParams.Add("Email", userEmailDto.Email);

            var userStatus = _dapper.LoadData<int>(sqlCheckUserActive, checkUserParams).FirstOrDefault();

            if (userStatus == 1) // User is active and verified
            {
                return BadRequest("Account is already verified.");
            }

            // Step 2: Generate a new OTP and update it in the Auth table
            var newOtp = new Random().Next(100000, 999999); // Generate a 6-digit OTP
            var newOtpExpirationTime = DateTime.UtcNow.AddMinutes(30); // Set expiration to 30 minutes from now

            string sqlUpdateOtp = @"
                UPDATE TutorialAppSchema.Auth
                SET OTP = @OTP, OTPExpirationTime = @OTPExpirationTime
                WHERE Email = @Email";

            var updateOtpParams = new Dapper.DynamicParameters();
            updateOtpParams.Add("Email", userEmailDto.Email);
            updateOtpParams.Add("OTP", newOtp);
            updateOtpParams.Add("OTPExpirationTime", newOtpExpirationTime);

            if (!_dapper.ExecuteSql(sqlUpdateOtp, updateOtpParams))
                throw new Exception("Failed to update OTP.");

            // Step 3: Send the new OTP to the user's email using EmailService
            string subject = "One-Time Password (OTP) for Account Verification";
            // add new line after the OTP code
            string body = $"Your OTP Code is: {newOtp}. \nIt is valid for 30 minutes.";

            try
            {
                _emailService.SendEmailAsync(userEmailDto.Email, subject, body).Wait();
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Console.WriteLine($"Failed to send OTP email: {ex.Message}");
                return StatusCode(500, "Failed to send OTP email. Please try again later.");
            }

            return Ok("A new OTP has been generated and sent to your email.");
        }
        /**
        *
        * Login USER
        *
        * POST: /auth/login
        * @return IActionResult
        *
        */
        [AllowAnonymous]
        [HttpPost("login")]
        public IActionResult Login(UserForLoginDto userForLogin)
        {
            // Step 1: Check if the account is active/verified(OTP)
            string sqlForUserStatus = @"
                SELECT Active FROM TutorialAppSchema.Users 
                WHERE Email = @Email";

            var statusParams = new Dapper.DynamicParameters();
            statusParams.Add("Email", userForLogin.Email);

            int userStatus = _dapper.LoadDataSingle<int>(sqlForUserStatus, statusParams);

            if (userStatus == 0) // If account is not active
            {
                return BadRequest("Account is not confirmed. Please check your email to confirm your account.");
            }

            // Step 2: Get password hash and salt 
            string sqlForHashAndSalt = @"SELECT 
                [PasswordHash],
                [PasswordSalt] FROM TutorialAppSchema.Auth WHERE Email = '" +
                userForLogin.Email + "'";

            UserForLoginConfirmationDto userForConfirmation = _dapper
                .LoadDataSingle<UserForLoginConfirmationDto>(sqlForHashAndSalt);

            byte[] passwordHash = _authHelper.GetPasswordHash(userForLogin.Password, userForConfirmation.PasswordSalt);

            // Step 3: Compare the computed password hash with the stored hash
            for (int index = 0; index < passwordHash.Length; index++)
            {
                if (passwordHash[index] != userForConfirmation.PasswordHash[index])
                {
                    return StatusCode(401, "Incorrect password!");
                }
            }

            // Step 4: Retrieve user ID 
            string userIdSql = @"
                SELECT UserId FROM TutorialAppSchema.Users WHERE Email = '" +
                userForLogin.Email + "'";

            int userId = _dapper.LoadDataSingle<int>(userIdSql);

            // Step 5: Return a JWT token
            return Ok(new Dictionary<string, string> {
                {"token", _authHelper.CreateToken(userId)}
            });
        }

        [HttpGet("refresh-token")]
        public string RefreshToken()
        {
            string userIdSql = @"
                SELECT UserId FROM TutorialAppSchema.Users WHERE UserId = '" +
                User.FindFirst("userId")?.Value + "'";

            int userId = _dapper.LoadDataSingle<int>(userIdSql);

            return _authHelper.CreateToken(userId);
        }

    }
}