using System.Net; using System.Net.Mail; using System.Threading.Tasks; using Microsoft.Extensions.Configuration; using Microsoft.Extensions.Logging;
namespace UIStore.Services {
    /// <summary>真實 SMTP Email 服務，透過 appsettings.json 設定寄信參數</summary>
    public class SmtpEmailService : IEmailService {
        private readonly IConfiguration _cfg; private readonly ILogger<SmtpEmailService> _logger;
        public SmtpEmailService(IConfiguration cfg, ILogger<SmtpEmailService> logger) { _cfg = cfg; _logger = logger; }
        public async Task SendEmailAsync(string to, string subject, string htmlBody) {
            var host = _cfg["Smtp:Host"]; var port = int.Parse(_cfg["Smtp:Port"] ?? "587");
            var user = _cfg["Smtp:Username"]; var pass = _cfg["Smtp:Password"]; var from = _cfg["Smtp:From"] ?? user;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user)) {
                _logger.LogWarning("[Email] SMTP 未設定，跳過寄信 To:{To} Sub:{Sub}", to, subject);
                return;
            }
            try {
                using var client = new SmtpClient(host, port) { Credentials = new NetworkCredential(user, pass), EnableSsl = true };
                using var msg = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
                await client.SendMailAsync(msg);
                _logger.LogInformation("[Email] 已寄送 To:{To} Sub:{Sub}", to, subject);
            } catch (System.Exception ex) {
                _logger.LogError(ex, "[Email] 寄信失敗 To:{To}", to);
            }
        }
    }
}
