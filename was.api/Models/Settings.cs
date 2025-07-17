namespace was.api.Models
{
    public class Settings
    {
        public int OtpExpirySeconds { get; set; }
        public JwtSettings Jwt { get; set; }
        public SmtpSettings SmtpSettings { get; set; }
    }
    public class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
    }
    public class SmtpSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
    }
}
