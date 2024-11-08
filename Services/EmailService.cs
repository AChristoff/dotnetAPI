using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Options;
using DotnetAPI.Models;

namespace DotnetAPI.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string recipientEmail, string subject, string message)
        {
            // Create a new MimeMessage
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            email.To.Add(MailboxAddress.Parse(recipientEmail));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html) { Text = message };

            using var smtp = new SmtpClient();

            try
            {
                // Choose the correct secure socket options based on the SMTP port
                SecureSocketOptions socketOptions = _emailSettings.Port switch
                {
                    465 => SecureSocketOptions.SslOnConnect, // SSL for port 465
                    587 => SecureSocketOptions.StartTls,      // TLS for port 587
                    _ => SecureSocketOptions.Auto             // Default option
                };

                // Connect to the SMTP server
                await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, socketOptions);

                // Authenticate if necessary
                if (!string.IsNullOrEmpty(_emailSettings.Username) && !string.IsNullOrEmpty(_emailSettings.Password))
                {
                    await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
                }

                // Send the email
                await smtp.SendAsync(email);
                Console.WriteLine($"Email sent successfully to {recipientEmail}");
            }
            catch (SmtpCommandException smtpEx)
            {
                // Specific SMTP command failure handling
                Console.WriteLine($"SMTP Command Error: {smtpEx.Message} - StatusCode: {smtpEx.StatusCode}");
                throw;
            }
            catch (SmtpProtocolException protocolEx)
            {
                // Specific SMTP protocol failure handling
                Console.WriteLine($"SMTP Protocol Error: {protocolEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                // General exception handling for connection/authentication errors
                Console.WriteLine($"Email sending failed: {ex.Message}");
                throw;
            }
            finally
            {
                // Disconnect from the SMTP server
                await smtp.DisconnectAsync(true);
            }
        }
    }
}
