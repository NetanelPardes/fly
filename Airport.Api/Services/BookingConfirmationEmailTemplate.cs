using Airport.Api.DTOs;
using System.Globalization;
using System.Net;
using System.Text;

namespace Airport.Api.Services;

public static class BookingConfirmationEmailTemplate
{
    public const string Subject = "Have a pleasant flight! Your booking confirmation | KodFly";
    public const string LogoContentId = "kodfly-logo";

    private const string OpeningLine = "Your booking was completed successfully – have a pleasant flight!";

    public static EmailMessage Create(TicketDto ticket, IWebHostEnvironment environment)
    {
        var logoPath = Path.Combine(environment.ContentRootPath, "Assets", "kodfly-logo.png");
        return new EmailMessage(
            ticket.PassengerEmail,
            Subject,
            BuildPlainText(ticket),
            BuildHtml(ticket, $"cid:{LogoContentId}"),
            new[] { new EmailInlineImage(LogoContentId, logoPath, "image/png") });
    }

    public static string BuildHtmlPreview(TicketDto ticket, string logoSource) => BuildHtml(ticket, logoSource);

    public static string BuildPlainText(TicketDto ticket)
    {
        var builder = new StringBuilder();
        builder.AppendLine(OpeningLine);
        builder.AppendLine();
        builder.AppendLine($"Hello {Text(ticket.PassengerName)},");
        builder.AppendLine("Thank you for choosing KodFly. Here are your booking details:");
        builder.AppendLine();

        AppendSection(builder, "Booking reference and passenger details", new[]
        {
            Field("Booking reference", BookingReference(ticket)),
            Field("Passenger", ticket.PassengerName),
            Field("Passenger email", ticket.PassengerEmail),
            Field("Passenger ID", ticket.PassengerNationalId)
        });

        AppendSection(builder, "Route and flight", new[]
        {
            Field("Departure airport", AirportLabel(ticket.OriginAirportName, ticket.OriginAirportCode, ticket.Origin, ticket.OriginCountry)),
            Field("Destination airport", AirportLabel(ticket.DestinationAirportName, ticket.DestinationAirportCode, ticket.Destination, ticket.DestinationCountry)),
            Field("Airline", ticket.AirlineName),
            Field("Flight number", ticket.FlightNumber)
        });

        AppendSection(builder, "Schedule", new[]
        {
            Field("Departure", FormatDateTime(ticket.DepartureTime)),
            Field("Arrival", FormatDateTime(ticket.ArrivalTime))
        });

        AppendSection(builder, "Travel options", new[]
        {
            Field("Travel class", ticket.Class.ToString()),
            Field("Included baggage", $"{ticket.BaggageAllowanceKg} kg"),
            Field("Seat", ticket.SeatNumber)
        });

        AppendSection(builder, "Payment summary", new[]
        {
            Field("Amount", FormatMoney(ticket.Price)),
            Field("Currency", "USD")
        });

        builder.AppendLine("Thank you for choosing KodFly. Have a pleasant flight!");
        return builder.ToString();
    }

    private static string BuildHtml(TicketDto ticket, string logoSource)
    {
        var sections = new[]
        {
            Section("Booking reference and passenger details", new[]
            {
                Field("Booking reference", BookingReference(ticket)),
                Field("Passenger", ticket.PassengerName),
                Field("Passenger email", ticket.PassengerEmail),
                Field("Passenger ID", ticket.PassengerNationalId)
            }),
            Section("Route and flight", new[]
            {
                Field("Departure airport", AirportLabel(ticket.OriginAirportName, ticket.OriginAirportCode, ticket.Origin, ticket.OriginCountry)),
                Field("Destination airport", AirportLabel(ticket.DestinationAirportName, ticket.DestinationAirportCode, ticket.Destination, ticket.DestinationCountry)),
                Field("Airline", ticket.AirlineName),
                Field("Flight number", ticket.FlightNumber)
            }),
            Section("Schedule", new[]
            {
                Field("Departure", FormatDateTime(ticket.DepartureTime)),
                Field("Arrival", FormatDateTime(ticket.ArrivalTime))
            }),
            Section("Travel options", new[]
            {
                Field("Travel class", ticket.Class.ToString()),
                Field("Included baggage", $"{ticket.BaggageAllowanceKg} kg"),
                Field("Seat", ticket.SeatNumber)
            }),
            Section("Payment summary", new[]
            {
                Field("Amount", FormatMoney(ticket.Price)),
                Field("Currency", "USD")
            })
        };

        return $$"""
        <!doctype html>
        <html lang="en" dir="ltr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>KodFly booking confirmation</title>
        </head>
        <body style="margin:0;padding:0;background:#eef2f6;color:#102338;font-family:Heebo,Assistant,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef2f6;margin:0;padding:28px 12px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:680px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #dbe5ee;box-shadow:0 16px 42px rgba(16,35,56,0.10);">
                  <tr>
                    <td style="padding:34px 34px 22px 34px;text-align:left;">
                      <div style="font-size:28px;line-height:1.22;font-weight:800;color:#0f3761;margin:0 0 18px 0;">{{Html(OpeningLine)}}</div>
                      <p style="font-size:16px;line-height:1.65;margin:0;color:#415466;">Hello {{Html(ticket.PassengerName)}},</p>
                      <p style="font-size:16px;line-height:1.65;margin:4px 0 0 0;color:#415466;">Thank you for choosing KodFly. Here are your booking details:</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 34px 20px 34px;">
                      {{RenderSections(sections)}}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:4px 34px 28px 34px;text-align:left;">
                      <p style="font-size:16px;line-height:1.65;margin:0;color:#102338;font-weight:700;">Thank you for choosing KodFly. Have a pleasant flight!</p>
                    </td>
                  </tr>
                  <tr>
                    <td align="center" style="padding:0 34px 34px 34px;">
                      <img src="{{HtmlAttribute(logoSource)}}" width="220" alt="KodFly" style="display:block;width:220px;max-width:70%;height:auto;border:0;outline:none;text-decoration:none;margin:0 auto;">
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
                    <td style="padding:9px 0;width:42%;font-size:13px;line-height:1.4;color:#6a7a8a;font-weight:700;text-transform:uppercase;letter-spacing:0.02em;vertical-align:top;">{Html(field.Label)}</td>
                    <td style="padding:9px 0;font-size:15px;line-height:1.5;color:#102338;font-weight:800;vertical-align:top;">{Html(field.Value)}</td>
                  </tr>
                """);

            return $"""
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #dbe5ee;margin-top:20px;">
                <tr>
                  <td style="padding:20px 0 6px 0;text-align:left;">
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

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<EmailField> fields)
    {
        builder.AppendLine(title);
        foreach (var field in fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)))
        {
            builder.AppendLine($"{field.Label}: {field.Value}");
        }

        builder.AppendLine();
    }

    private static EmailSection Section(string title, IEnumerable<EmailField> fields) => new(title, fields.ToArray());

    private static EmailField Field(string label, string? value) => new(label, value);

    private static string BookingReference(TicketDto ticket) => $"KOD-{ticket.Id:000000}";

    private static string AirportLabel(string? airportName, string airportCode, string city, string? country)
    {
        var place = string.Join(", ", new[] { city, country }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var airport = string.IsNullOrWhiteSpace(airportName) ? place : airportName;
        var code = string.IsNullOrWhiteSpace(airportCode) ? null : $"({airportCode})";
        return string.Join(" ", new[] { airport, code }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatDateTime(DateTime value) => value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) => $"${value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string Text(string? value) => value ?? "";

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string HtmlAttribute(string? value) => Html(value);

    private record EmailSection(string Title, IReadOnlyCollection<EmailField> Fields);

    private record EmailField(string Label, string? Value);
}
