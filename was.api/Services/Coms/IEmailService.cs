namespace was.api.Services.Coms
{
    public interface IEmailService
    {
        public Task<bool> SendEmailAsync(string to, string subject, string htmlBody, List<string> cc = null);
        public Task SendTemplatedEmailAsync(string to, string subject, string templateName, Dictionary<string, string> placeholders, List<string> cc = null);

    }
}
