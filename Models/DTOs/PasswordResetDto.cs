public class PasswordResetDto
{
    public string OldPassword { get; set; }
    public string NewPassword { get; set; }
    public string ConfirmNewPassword { get; set; }

    public int OTP { get; set; }

    public PasswordResetDto()
    {
        // Default values if NULL
        OldPassword ??= "";
        NewPassword ??= "";
        ConfirmNewPassword ??= "";
    }
}