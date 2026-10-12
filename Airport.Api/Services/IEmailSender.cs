namespace Airport.Api.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public record EmailMessage(
    string To,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null,
    IReadOnlyCollection<EmailInlineImage>? InlineImages = null);

public record EmailInlineImage(
    string ContentId,
    string FilePath,
    string MediaType);
