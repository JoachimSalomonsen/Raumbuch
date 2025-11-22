using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace RaumbuchService.Services
{
    /// <summary>
    /// Service for sending email notifications.
    /// For production use, configure SMTP settings in Web.config.
    /// For development, this logs emails to console instead of sending.
    /// </summary>
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;
        private readonly bool _isDevelopment;

        public EmailService(
            string smtpHost = null,
            int smtpPort = 587,
            string smtpUsername = null,
            string smtpPassword = null,
            string fromEmail = "noreply@raumbuch.local",
            string fromName = "Raumbuch Manager",
            bool enableSsl = true,
            bool isDevelopment = true)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUsername = smtpUsername;
            _smtpPassword = smtpPassword;
            _fromEmail = fromEmail;
            _fromName = fromName;
            _enableSsl = enableSsl;
            _isDevelopment = isDevelopment;
        }

        /// <summary>
        /// Sends notification email about created Raumprogramm file.
        /// </summary>
        public async Task SendRaumprogrammNotificationAsync(
            List<string> recipientEmails,
            string fileName,
            string downloadUrl,
            string projectName = null)
        {
            if (recipientEmails == null || recipientEmails.Count == 0)
                throw new ArgumentException("Mindestens ein Empfänger erforderlich.", nameof(recipientEmails));

            string subject = "Raumprogramm wurde auf Trimble Connect erstellt";

            string body = BuildRaumprogrammEmailBody(fileName, downloadUrl, projectName);

            await SendEmailAsync(recipientEmails, subject, body, isHtml: true);
        }

        /// <summary>
        /// Sends notification email about created Raumbuch file.
        /// </summary>
        public async Task SendRaumbuchNotificationAsync(
            List<string> recipientEmails,
            string fileName,
            string downloadUrl,
            string projectName = null)
        {
            if (recipientEmails == null || recipientEmails.Count == 0)
                throw new ArgumentException("Mindestens ein Empfänger erforderlich.", nameof(recipientEmails));

            string subject = "Raumbuch wurde auf Trimble Connect erstellt";

            string body = BuildRaumbuchEmailBody(fileName, downloadUrl, projectName);

            await SendEmailAsync(recipientEmails, subject, body, isHtml: true);
        }

        /// <summary>
        /// Sends an email to multiple recipients.
        /// In development mode, logs to console instead of sending.
        /// </summary>
        private async Task SendEmailAsync(
            List<string> recipientEmails,
            string subject,
            string body,
            bool isHtml = false)
        {
            if (_isDevelopment)
            {
                // Development mode: log to console instead of sending
                Console.WriteLine("=== EMAIL (Development Mode - Not Sent) ===");
                Console.WriteLine($"From: {_fromName} <{_fromEmail}>");
                Console.WriteLine($"To: {string.Join(", ", recipientEmails)}");
                Console.WriteLine($"Subject: {subject}");
                Console.WriteLine($"Body:\n{body}");
                Console.WriteLine("==========================================");
                
                // Simulate async operation
                await Task.Delay(100);
                return;
            }

            // Production mode: send actual email
            if (string.IsNullOrWhiteSpace(_smtpHost))
                throw new InvalidOperationException("SMTP Host nicht konfiguriert.");

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(_fromEmail, _fromName);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = isHtml;

                foreach (var email in recipientEmails)
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        message.To.Add(email);
                    }
                }

                if (message.To.Count == 0)
                    throw new ArgumentException("Keine gültigen E-Mail-Adressen angegeben.");

                using (var smtp = new SmtpClient(_smtpHost, _smtpPort))
                {
                    smtp.EnableSsl = _enableSsl;
                    
                    if (!string.IsNullOrWhiteSpace(_smtpUsername))
                    {
                        smtp.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
                    }

                    await smtp.SendMailAsync(message);
                }
            }
        }

        /// <summary>
        /// Builds HTML email body for Raumprogramm notification.
        /// </summary>
        private string BuildRaumprogrammEmailBody(string fileName, string downloadUrl, string projectName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head><meta charset='UTF-8'></head>");
            sb.AppendLine("<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>");
            sb.AppendLine("<div style='max-width: 600px; margin: 0 auto; padding: 20px;'>");
            
            // Header
            sb.AppendLine("<div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>");
            sb.AppendLine("<h1 style='margin: 0;'>?? Raumprogramm erstellt</h1>");
            sb.AppendLine("</div>");

            // Content
            sb.AppendLine("<div style='background: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>");
            sb.AppendLine("<p style='font-size: 16px;'>Das Raumprogram wurde auf Trimble Connect erstellt.</p>");
            
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                sb.AppendLine($"<p><strong>Projekt:</strong> {projectName}</p>");
            }
            
            sb.AppendLine($"<p><strong>Datei:</strong> {fileName}</p>");
            sb.AppendLine("<p>Bitte klicken Sie auf den Link unten, um die Datei herunterzuladen:</p>");
            sb.AppendLine("</div>");

            // Download button
            sb.AppendLine("<div style='text-align: center; margin: 30px 0;'>");
            sb.AppendLine($"<a href='{downloadUrl}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;'>?? Raumprogramm herunterladen</a>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div style='text-align: center; color: #666; font-size: 12px; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0;'>");
            sb.AppendLine("<p>Diese E-Mail wurde automatisch generiert vom Raumbuch Manager.</p>");
            sb.AppendLine("<p>Bitte antworten Sie nicht auf diese E-Mail.</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Builds HTML email body for Raumbuch notification.
        /// </summary>
        private string BuildRaumbuchEmailBody(string fileName, string downloadUrl, string projectName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head><meta charset='UTF-8'></head>");
            sb.AppendLine("<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>");
            sb.AppendLine("<div style='max-width: 600px; margin: 0 auto; padding: 20px;'>");
            
            // Header
            sb.AppendLine("<div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>");
            sb.AppendLine("<h1 style='margin: 0;'>?? Raumbuch erstellt</h1>");
            sb.AppendLine("</div>");

            // Content
            sb.AppendLine("<div style='background: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>");
            sb.AppendLine("<p style='font-size: 16px;'>Das Raumbuch mit SOLL/IST-Analyse wurde auf Trimble Connect erstellt.</p>");
            
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                sb.AppendLine($"<p><strong>Projekt:</strong> {projectName}</p>");
            }
            
            sb.AppendLine($"<p><strong>Datei:</strong> {fileName}</p>");
            sb.AppendLine("<p>Bitte klicken Sie auf den Link unten, um die Datei herunterzuladen:</p>");
            sb.AppendLine("</div>");

            // Download button
            sb.AppendLine("<div style='text-align: center; margin: 30px 0;'>");
            sb.AppendLine($"<a href='{downloadUrl}' style='display: inline-block; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;'>?? Raumbuch herunterladen</a>");
            sb.AppendLine("</div>");

            // Footer
            sb.AppendLine("<div style='text-align: center; color: #666; font-size: 12px; margin-top: 30px; padding-top: 20px; border-top: 1px solid #e0e0e0;'>");
            sb.AppendLine("<p>Diese E-Mail wurde automatisch generiert vom Raumbuch Manager.</p>");
            sb.AppendLine("<p>Bitte antworten Sie nicht auf diese E-Mail.</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}
