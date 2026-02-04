using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using was.api.Models;

namespace was.api.Services.Coms
{
    public class EmailService(ILogger<EmailService> logger, IOptions<Settings> options) : IEmailService
    {
        private readonly Settings _settings = options.Value;
        private ILogger<EmailService> _logger = logger;
        public async Task<string> LoadTemplateAsync(string templateName)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "EmailTemplates", $"{templateName}.html");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Template not found: {templateName}");

            return await File.ReadAllTextAsync(path);
        }
        public string PopulateTemplate(string template, Dictionary<string, string> placeholders)
        {
            foreach (var pair in placeholders)
            {
                template = template.Replace($"{{{{{pair.Key}}}}}", pair.Value);
            }
            return template;
        }
        public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody, List<string> cc = null)
        {
            var toList = to.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var message = new MailMessage
            {
                From = new MailAddress(_settings.SmtpSettings.From),
                Subject = subject,
                IsBodyHtml = true
            };
            foreach (var item in toList)
            {
                message.To.Add(item);
            }

            if (cc != null)
            {
                foreach (var ccAddress in cc)
                {
                   message.CC.Add(ccAddress.Trim());
                }
            }

            // Generate plain text fallback
            string plainTextBody = HtmlToPlainText(htmlBody);

            // Alternate views
            var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");

            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);

            using var smtp = new SmtpClient(_settings.SmtpSettings.Host, _settings.SmtpSettings.Port)
            {
                EnableSsl = _settings.SmtpSettings.EnableSsl,
                Credentials = new NetworkCredential(_settings.SmtpSettings.User, _settings.SmtpSettings.Password)
            };
            _logger.LogInformation($"Sending email to:{to}, cc:{(cc is null ? "" : string.Join(",", cc.ToList()))}, subject:{subject}");
            await smtp.SendMailAsync(message);
            _logger.LogInformation($"Email sent to:{to}, cc:{(cc is null ? "" : string.Join(",",cc.ToList()))}, subject:{subject}, body:{htmlBody}");
            return true;
        }

        public async Task SendTemplatedEmailAsync(string to, string subject, string templateName, Dictionary<string, string> placeholders, List<string> cc = null)
        {
            var template = await LoadTemplateAsync(templateName);
            var htmlBody = PopulateTemplate(template, placeholders);
            await SendEmailAsync(to, $"{_settings.AppName} - {subject}", htmlBody, cc);
        }
        private string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // Remove HTML tags
            string text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);

            // Decode HTML entities
            text = WebUtility.HtmlDecode(text);

            return text;
        }


    }
}
