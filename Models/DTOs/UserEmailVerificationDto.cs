namespace DotnetAPI.Models.DTOs
{
    public class UserEmailVerificationDto
    {
        public string Email { get; set; }
        public int OTP { get; set; }

        public UserEmailVerificationDto()
        {
            // Default values if NULL
            Email ??= "";
        }
    }


}