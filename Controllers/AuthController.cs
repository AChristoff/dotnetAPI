using System.Security.Cryptography;
using DotnetAPI.Data;
using DotnetAPI.Models.DTOs;
using DotnetAPI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotnetAPI.Services;

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

        /** -------------------------------- PUBLIC ROUTES -------------------------------- **/

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

            // Step 1: Generate OTP and Expiration Time
            var (otp, otpExpirationTime) = _authHelper.GenerateOtp();

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
        * POST: /auth/register-confirm
        * @return IActionResult
        *
        */
        [AllowAnonymous]
        [HttpPost("register-confirm")]
        public IActionResult VerifyOtp(UserEmailVerificationDto otpVerification)
        {
            // Step 1: Retrieve the OTP and expiration time from the Auth table for the given email
            string sqlCheckOTP = @"
                SELECT OTP, OTPExpirationTime FROM TutorialAppSchema.Auth 
                WHERE Email = @Email";

            var checkOtpParams = new Dapper.DynamicParameters();
            checkOtpParams.Add("Email", otpVerification.Email);

            var otpData = _dapper.LoadData<dynamic>(sqlCheckOTP, checkOtpParams).FirstOrDefault();

            // Step 2: Validate OTP existence, correctness, and expiration
            if (otpData == null)
                return NotFound("OTP not found for this email.");

            if (otpData.OTP != otpVerification.OTP)
                return BadRequest("Invalid OTP.");

            if (DateTime.UtcNow > otpData.OTPExpirationTime)
                return BadRequest("OTP has expired.");

            // Step 3: OTP is valid, so activate the user by updating the 'Active' status in the Users table
            string sqlActivateUser = @"
                UPDATE TutorialAppSchema.Users
                SET Active = 1
                WHERE Email = @Email";

            var activateUserParams = new Dapper.DynamicParameters();
            activateUserParams.Add("Email", otpVerification.Email);

            if (!_dapper.ExecuteSql(sqlActivateUser, activateUserParams))
                throw new Exception("Failed to activate user.");

            // Step 4: Clear OTP and expiration time after successful verification in the Auth table
            string sqlClearOTP = @"
                UPDATE TutorialAppSchema.Auth
                SET OTP = NULL, OTPExpirationTime = NULL
                WHERE Email = @Email";

            var clearOtpParams = new Dapper.DynamicParameters();
            clearOtpParams.Add("Email", otpVerification.Email);

            _dapper.ExecuteSql(sqlClearOTP, clearOtpParams);

            // Step 5: Retrieve the user ID from the Users table to generate a JWT token
            string userIdSql = @"
                SELECT UserId FROM TutorialAppSchema.Users 
                WHERE Email = @Email";

            int userId = _dapper.LoadDataSingle<int>(userIdSql, activateUserParams);

            // Step 6: Generate and return a JWT token for the user to log them in after successful email verification
            return Ok(new Dictionary<string, string> {
                {"token", _authHelper.CreateToken(userId)}
            });
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
            // Step 1: Check if the user is authenticated
            var userIdClaim = User.FindFirst("userId")?.Value;

            // Step 2: Check if the user is already verified (Active = 1) only if the user is not authenticated
            string sqlCheckUserActive = @"
                SELECT Active FROM TutorialAppSchema.Users
                WHERE Email = @Email";

            var checkUserParams = new Dapper.DynamicParameters();
            checkUserParams.Add("Email", userEmailDto.Email);

            var userStatus = _dapper.LoadData<int>(sqlCheckUserActive, checkUserParams).FirstOrDefault();

            if (userStatus == 1 && userIdClaim == null) // User is active and verified (OTP) only for non-authenticated users
            {
                return BadRequest("Account is already verified.");
            }

            // Step 2: Generate a new OTP and update it in the Auth table
            var (otp, otpExpirationTime) = _authHelper.GenerateOtp();

            string sqlUpdateOtp = @"
                UPDATE TutorialAppSchema.Auth
                SET OTP = @OTP, OTPExpirationTime = @OTPExpirationTime
                WHERE Email = @Email";

            var updateOtpParams = new Dapper.DynamicParameters();
            updateOtpParams.Add("Email", userEmailDto.Email);
            updateOtpParams.Add("OTP", otp);
            updateOtpParams.Add("OTPExpirationTime", otpExpirationTime);

            if (!_dapper.ExecuteSql(sqlUpdateOtp, updateOtpParams))
                throw new Exception("Failed to update OTP.");

            // Step 3: Send the new OTP to the user's email using EmailService
            string subject = "One-Time Password (OTP) for Account Verification";
            // add new line after the OTP code
            string body = $"Your OTP Code is: {otp}. \nIt is valid for 30 minutes.";

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

        /** -------------------------------- PROTECTED ROUTES -------------------------------- **/

        /**
        *
        * Refresh Token
        *
        * GET: /auth/refresh-token
        * @info Refresh the JWT token if user is AUTHENTICATED else if UNAUTHENTICATED and NOT ACTIVE (0)
        *
        */
        [HttpGet("refresh-token")]
        public string RefreshToken()
        {
            string userIdSql = @"
                SELECT UserId FROM TutorialAppSchema.Users WHERE UserId = '" +
                User.FindFirst("userId")?.Value + "'";

            int userId = _dapper.LoadDataSingle<int>(userIdSql);

            return _authHelper.CreateToken(userId);
        }

        /**
        *
        * Password Reset
        *
        * POST: /auth/password-reset
        * @info Reset the password requires: OTP and OldPassword
        *
        */
        [HttpPost("password-reset")]
        public IActionResult PasswordReset(PasswordResetDto resetDto)
        {
            // Step 1: Retrieve user ID from JWT token
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null)
            {
                return Unauthorized("User ID not found in token.");
            }
            int userId;
            if (!int.TryParse(userIdClaim, out userId))
            {
                return Unauthorized("Invalid user ID in token.");
            }

            // Step 2: Get the user's email using the UserId
            string sqlForUserEmail = @"
                SELECT Email FROM TutorialAppSchema.Users 
                WHERE UserId = @UserId";

            var emailParams = new Dapper.DynamicParameters();
            emailParams.Add("UserId", userId);
            string userEmail = _dapper.LoadData<string>(sqlForUserEmail, emailParams).FirstOrDefault() ?? string.Empty;

            if (string.IsNullOrEmpty(userEmail))
            {
                return NotFound("User email not found.");
            }

            // Step 3: Get the user's current password hash, salt, and OTP data using Email
            string sqlForAuthData = @"
                SELECT PasswordHash, PasswordSalt, OTP, OTPExpirationTime 
                FROM TutorialAppSchema.Auth 
                WHERE Email = @Email";

            var authDataParams = new Dapper.DynamicParameters();
            authDataParams.Add("Email", userEmail);
            var authData = _dapper.LoadData<dynamic>(sqlForAuthData, authDataParams).FirstOrDefault();

            if (authData == null)
                return NotFound("User data not found.");

            // Step 4: Verify old password
            byte[] oldPasswordHash = _authHelper.GetPasswordHash(resetDto.OldPassword, authData.PasswordSalt);
            for (int index = 0; index < oldPasswordHash.Length; index++)
            {
                if (oldPasswordHash[index] != authData.PasswordHash[index])
                {
                    return StatusCode(401, "Incorrect old password.");
                }
            }

            // Step 5: Validate OTP and expiration time
            if (authData.OTP != resetDto.OTP)
            {
                return BadRequest("Invalid OTP.");
            }

            if (DateTime.UtcNow > authData.OTPExpirationTime)
            {
                return BadRequest("OTP has expired.");
            }

            // Step 6: Validate new passwords
            if (resetDto.NewPassword != resetDto.ConfirmNewPassword)
            {
                return BadRequest("New password and confirmation do not match.");
            }

            // Step 7: Generate new password hash and salt for the new password
            byte[] newSalt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetNonZeroBytes(newSalt);
            }
            byte[] newPasswordHash = _authHelper.GetPasswordHash(resetDto.NewPassword, newSalt);

            // Step 8: Update the password hash, salt, and clear the OTP in the Auth table
            string sqlUpdatePassword = @"
                UPDATE TutorialAppSchema.Auth
                SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, OTP = NULL, OTPExpirationTime = NULL
                WHERE Email = @Email";

            var updatePasswordParams = new Dapper.DynamicParameters();
            updatePasswordParams.Add("Email", userEmail);
            updatePasswordParams.Add("PasswordHash", newPasswordHash);
            updatePasswordParams.Add("PasswordSalt", newSalt);

            bool isUpdated = _dapper.ExecuteSql(sqlUpdatePassword, updatePasswordParams);
            if (!isUpdated)
            {
                throw new Exception("Failed to reset password.");
            }

            return Ok("Password has been successfully reset.");
        }



    }
}