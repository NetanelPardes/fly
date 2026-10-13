using Airport.Api.DTOs;
using System.Globalization;
using System.Net;
using System.Text;

namespace Airport.Api.Services;

public static class TicketUpdateEmailTemplate
{
    public const string Subject = "Your KodFly ticket was updated";
    public const string LogoContentId = "kodfly-logo";

    public static EmailMessage CreateUpdated(TicketDto ticket, IWebHostEnvironment environment) =>
        Create(ticket, environment, "Your ticket details have been updated.", "Ticket update");

    public static EmailMessage CreateCancelled(TicketDto ticket, IWebHostEnvironment environment) =>
        Create(ticket, environment, "Your ticket has been cancelled.", "Ticket cancellation");

    private static EmailMessage Create(TicketDto ticket, IWebHostEnvironment environment, string message, string title)
    {
        var logoPath = Path.Combine(environment.ContentRootPath, "Assets", "kodfly-logo.png");
        return new EmailMessage(
            ticket.PassengerEmail,
            Subject,
            BuildPlainText(ticket, message),
            BuildHtml(ticket, message, title, $"cid:{LogoContentId}"),
            new[] { new EmailInlineImage(LogoContentId, logoPath, "image/png") });
    }

    private static string BuildPlainText(TicketDto ticket, string message)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Hello {Text(ticket.PassengerName)},");
        builder.AppendLine(message);
        builder.AppendLine();
        builder.AppendLine("Ticket details");
        builder.AppendLine($"Booking reference: {BookingReference(ticket)}");
        builder.AppendLine($"Flight: {ticket.FlightNumber}");
        builder.AppendLine($"Route: {Place(ticket.Origin, ticket.OriginCountry)} to {Place(ticket.Destination, ticket.DestinationCountry)}");
        builder.AppendLine($"Departure: {FormatDateTime(ticket.DepartureTime)}");
        builder.AppendLine($"Arrival: {FormatDateTime(ticket.ArrivalTime)}");
        builder.AppendLine($"Seat: {ticket.SeatNumber}");
        builder.AppendLine($"Class: {ticket.Class}");
        builder.AppendLine($"Baggage: {ticket.BaggageAllowanceKg} kg");
        builder.AppendLine($"Status: {ticket.Status}");
        builder.AppendLine($"Amount: {FormatMoney(ticket.Price)}");
        builder.AppendLine();
        builder.AppendLine("KodFly team");
        return builder.ToString();
    }

    private static string BuildHtml(TicketDto ticket, string message, string title, string logoSource)
    {
        var sections = new[]
        {
            Section("Booking", new[]
            {
                Field("Reference", BookingReference(ticket)),
                Field("Passenger", ticket.PassengerName),
                Field("Email", ticket.PassengerEmail),
                Field("Status", ticket.Status.ToString())
            }),
            Section("Route", new[]
            {
                Field("From", AirportLabel(ticket.OriginAirportName, ticket.OriginAirportCode, ticket.Origin, ticket.OriginCountry)),
                Field("To", AirportLabel(ticket.DestinationAirportName, ticket.DestinationAirportCode, ticket.Destination, ticket.DestinationCountry)),
                Field("Flight", ticket.FlightNumber),
                Field("Airline", ticket.AirlineName)
            }),
            Section("Schedule", new[]
            {
                Field("Departure", FormatDateTime(ticket.DepartureTime)),
                Field("Arrival", FormatDateTime(ticket.ArrivalTime)),
                Field("Gate", ticket.BoardingGate)
            }),
            Section("Options", new[]
            {
                Field("Seat", ticket.SeatNumber),
                Field("Class", ticket.Class.ToString()),
                Field("Baggage", $"{ticket.BaggageAllowanceKg} kg"),
                Field("Amount", FormatMoney(ticket.Price))
            })
        };

        return $$"""
        <!doctype html>
        <html lang="en" dir="ltr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{Html(title)}}</title>
        </head>
        <body style="margin:0;padding:0;background:#eef2f6;color:#102338;font-family:Heebo,Assistant,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef2f6;margin:0;padding:28px 12px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:680px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #dbe5ee;box-shadow:0 16px 42px rgba(16,35,56,0.10);">
                  <tr>
                    <td style="padding:28px 34px 12px 34px;text-align:left;">
                      <img src="{{HtmlAttribute(logoSource)}}" width="170" alt="KodFly" style="display:block;width:170px;max-width:55%;height:auto;border:0;outline:none;text-decoration:none;margin:0 0 24px 0;">
                      <div style="font-size:28px;line-height:1.22;font-weight:800;color:#0f3761;margin:0 0 12px 0;">{{Html(title)}}</div>
                      <p style="font-size:16px;line-height:1.65;margin:0;color:#415466;">Hello {{Html(ticket.PassengerName)}},</p>
                      <p style="font-size:16px;line-height:1.65;margin:4px 0 0 0;color:#415466;">{{Html(message)}}</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 34px 20px 34px;">
                      {{RenderSections(sections)}}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:4px 34px 30px 34px;text-align:left;">
                      <p style="font-size:16px;line-height:1.65;margin:0;color:#102338;font-weight:700;">Thank you for choosing KodFly.</p>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string RenderSections(IEnumerable<EmailSection> sections) =>
        string.Join("", sections.Select(section =>
        {
            var rows = section.Fields
                .Where(field => !string.IsNullOrWhiteSpace(field.Value))
                .Select(field => $"""
                  <tr>
                    <td style="padding:9px 0;width:38%;font-size:13px;line-height:1.4;color:#6a7a8a;font-weight:700;text-transform:uppercase;letter-spacing:0.02em;vertical-align:top;">{Html(field.Label)}</td>
                    <td style="padding:9px 0;font-size:15px;line-height:1.5;color:#102338;font-weight:800;vertical-align:top;">{Html(field.Value)}</td>
                  </tr>
                """);

            return $"""
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #dbe5ee;margin-top:20px;">
                <tr>
                  <td style="padding:18px 0 6px 0;text-align:left;">
                    <h2 style="font-size:17px;line-height:1.35;color:#0f3761;margin:0;font-weight:800;">{Html(section.Title)}</h2>
                  </td>
                </tr>
                <tr>
                  <td>
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                      {string.Join("", rows)}
                    </table>
                  </td>
                </tr>
              </table>
            """;
        }));

    private static EmailSection Section(string title, IEnumerable<EmailField> fields) => new(title, fields.ToArray());

    private static EmailField Field(string label, string? value) => new(label, value);

    private static string BookingReference(TicketDto ticket) => $"KOD-{ticket.Id:000000}";

    private static string AirportLabel(string? airportName, string airportCode, string city, string? country)
    {
        var place = Place(city, country);
        var airport = string.IsNullOrWhiteSpace(airportName) ? place : airportName;
        var code = string.IsNullOrWhiteSpace(airportCode) ? null : $"({airportCode})";
        return string.Join(" ", new[] { airport, code }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string Place(string city, string? country) =>
        string.Join(", ", new[] { city, country }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string FormatDateTime(DateTime value) => value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) => $"${value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string Text(string? value) => value ?? "";

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string HtmlAttribute(string? value) => Html(value);

    private record EmailSection(string Title, IReadOnlyCollection<EmailField> Fields);

    private record EmailField(string Label, string? Value);
}
