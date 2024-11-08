using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;

namespace DotnetAPI.Helpers
{
    public class AuthHelper
    {
        public AuthHelper()
        {
            Env.Load();
        }

        public byte[] GetPasswordHash(string password, byte[] passwordSalt)
        {
            // Get PasswordKey from environment variables
            string passwordKey = Environment.GetEnvironmentVariable("PasswordKey") ??
                                 throw new InvalidOperationException("PasswordKey is not configured."); // Get PasswordKey from environment variables

            string passwordSaltPlusString = passwordKey + Convert.ToBase64String(passwordSalt); // Combine PasswordKey and passwordSalt

            return KeyDerivation.Pbkdf2(
                password: password,
                salt: Encoding.ASCII.GetBytes(passwordSaltPlusString),
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 1000000,
                numBytesRequested: 256 / 8
            ); // Return password hash
        }

        public string CreateToken(int userId)
        {
            Claim[] claims = [
                new Claim("userId", userId.ToString())
            ];

            // Get TokenKey from environment variables
            string tokenKeyString = Environment.GetEnvironmentVariable("TokenKey")
                                    ?? throw new InvalidOperationException("TokenKey is not configured."); // Get TokenKey from environment variables

            SymmetricSecurityKey tokenKey = new(Encoding.UTF8.GetBytes(tokenKeyString)); // Convert tokenKey to byte array
            SigningCredentials credentials = new(tokenKey, SecurityAlgorithms.HmacSha512Signature); // Use HmacSha512 for token signature

            SecurityTokenDescriptor descriptor = new() // Create token descriptor
            {
                Subject = new ClaimsIdentity(claims), // Add userId claim
                SigningCredentials = credentials, // Use tokenKey for signing
                Expires = DateTime.Now.AddDays(30) // Token expires in 30 days
            };

            JwtSecurityTokenHandler tokenHandler = new(); // Create token handler
            SecurityToken token = tokenHandler.CreateToken(descriptor); // Create token

            return tokenHandler.WriteToken(token); // Return token as string
        }
    }
}
