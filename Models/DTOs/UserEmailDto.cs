namespace DotnetAPI.Models.DTOs
{
    public class UserEmailDto
    {
        public string Email { get; set; }
        public UserEmailDto()
        {
            // Default values if NULL
            Email ??= "";
        }
    }


}