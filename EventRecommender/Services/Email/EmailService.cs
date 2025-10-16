using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace EventRecommender.Services.Email
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly SmtpClient _smtp;

        public EmailService(IConfiguration config)
        {
            _config = config;

            var host = _config["Email:SmtpHost"];
            var port = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var user = _config["Email:Username"];
            var pass = _config["Email:Password"];

            _smtp = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, pass),
                EnableSsl = true
            };
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var from = _config["Email:From"] ?? "noreply@eventrecommender.local";

            var msg = new MailMessage(from, to, subject, body)
            {
                // We’re sending HTML now
                IsBodyHtml = true
            };

            await _smtp.SendMailAsync(msg);
        }
    }
}

