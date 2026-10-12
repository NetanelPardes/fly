using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Airport.Api.Services;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
        SendAsync(new EmailMessage(to, subject, body), cancellationToken);

    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var host = configuration["SMTP_HOST"];
        var user = configuration["SMTP_USER"];
        var password = configuration["SMTP_PASSWORD"];
        var fromEmail = configuration["SMTP_FROM_EMAIL"] ?? user;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(user)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogInformation("SMTP email is not configured. Skipping email to {Recipient}.", email.To);
            return;
        }

        if (string.IsNullOrWhiteSpace(email.To))
        {
            logger.LogWarning("Cannot send email without a recipient address.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, configuration["SMTP_FROM_NAME"] ?? "DataFly", Encoding.UTF8),
            Subject = email.Subject,
            Body = email.PlainTextBody,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(email.To);

        if (!string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            var plainView = AlternateView.CreateAlternateViewFromString(email.PlainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain);
            var htmlView = AlternateView.CreateAlternateViewFromString(email.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);

            foreach (var image in email.InlineImages ?? Array.Empty<EmailInlineImage>())
            {
                if (!File.Exists(image.FilePath))
                {
                    logger.LogWarning("Inline email image {ContentId} was not found at {Path}.", image.ContentId, image.FilePath);
                    continue;
                }

                var resource = new LinkedResource(image.FilePath, image.MediaType)
                {
                    ContentId = image.ContentId,
                    TransferEncoding = TransferEncoding.Base64
                };
                resource.ContentLink = new Uri($"cid:{image.ContentId}");
                htmlView.LinkedResources.Add(resource);
            }

            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);
        }

        using var client = new SmtpClient(host, int.TryParse(configuration["SMTP_PORT"], out var port) ? port : 587)
        {
            EnableSsl = bool.TryParse(configuration["SMTP_ENABLE_SSL"], out var enableSsl) ? enableSsl : true,
            Credentials = new NetworkCredential(user, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = int.TryParse(configuration["SMTP_TIMEOUT_MS"], out var timeout) ? timeout : 10000
        };

        using var registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message, cancellationToken);
    }
}
