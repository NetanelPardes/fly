using Airport.Api.DTOs;
using System.Globalization;
using System.Net;
using System.Text;

namespace Airport.Api.Services;

public static class BookingConfirmationEmailTemplate
{
    public const string Subject = "Have a pleasant flight! Your booking confirmation | KodFly";
    public const string LogoContentId = "kodfly-logo";

    private const string OpeningLine = "Your booking was completed successfully - have a pleasant flight!";

    public static EmailMessage Create(TicketDto ticket, IWebHostEnvironment environment) =>
        Create(new[] { ticket }, environment);

    public static EmailMessage Create(IReadOnlyCollection<TicketDto> tickets, IWebHostEnvironment environment)
    {
        var orderedTickets = OrderTickets(tickets);
        var logoPath = Path.Combine(environment.ContentRootPath, "Assets", "kodfly-logo.png");
        return new EmailMessage(
            orderedTickets[0].PassengerEmail,
            Subject,
            BuildPlainText(orderedTickets),
            BuildHtml(orderedTickets, $"cid:{LogoContentId}"),
            new[] { new EmailInlineImage(LogoContentId, logoPath, "image/png") });
    }

    public static string BuildHtmlPreview(TicketDto ticket, string logoSource) => BuildHtml(new[] { ticket }, logoSource);

    public static string BuildPlainText(TicketDto ticket) => BuildPlainText(new[] { ticket });

    public static string BuildPlainText(IReadOnlyCollection<TicketDto> tickets)
    {
        var orderedTickets = OrderTickets(tickets);
        var first = orderedTickets[0];
        var last = orderedTickets[^1];
        var isConnection = orderedTickets.Count > 1;
        var builder = new StringBuilder();

        builder.AppendLine(OpeningLine);
        builder.AppendLine();
        builder.AppendLine($"Hello {Text(first.PassengerName)},");
        builder.AppendLine(isConnection
            ? $"Thank you for choosing KodFly. We booked {orderedTickets.Count} connected flights for your route:"
            : "Thank you for choosing KodFly. Here are your booking details:");
        builder.AppendLine();

        AppendSection(builder, "Booking reference and passenger details", new[]
        {
            Field(isConnection ? "Route reference" : "Booking reference", isConnection ? first.RouteBookingReference : BookingReference(first)),
            Field("Passenger", first.PassengerName),
            Field("Passenger email", first.PassengerEmail),
            Field("Passenger ID", first.PassengerNationalId)
        });

        AppendSection(builder, isConnection ? "Route summary" : "Route and flight", isConnection
            ? new[]
            {
                Field("Departure airport", AirportLabel(first.OriginAirportName, first.OriginAirportCode, first.Origin, first.OriginCountry)),
                Field("Destination airport", AirportLabel(last.DestinationAirportName, last.DestinationAirportCode, last.Destination, last.DestinationCountry)),
                Field("Flights", string.Join(" + ", orderedTickets.Select(ticket => ticket.FlightNumber))),
                Field("Connections", (orderedTickets.Count - 1).ToString(CultureInfo.InvariantCulture)),
                Field("Total connection wait", TotalWaitLabel(orderedTickets))
            }
            : new[]
            {
                Field("Departure airport", AirportLabel(first.OriginAirportName, first.OriginAirportCode, first.Origin, first.OriginCountry)),
                Field("Destination airport", AirportLabel(first.DestinationAirportName, first.DestinationAirportCode, first.Destination, first.DestinationCountry)),
                Field("Airline", first.AirlineName),
                Field("Flight number", first.FlightNumber)
            });

        if (!isConnection)
        {
            AppendSection(builder, "Schedule", new[]
            {
                Field("Departure", FormatDateTime(first.DepartureTime)),
                Field("Arrival", FormatDateTime(first.ArrivalTime))
            });

            AppendSection(builder, "Travel options", new[]
            {
                Field("Travel class", first.Class.ToString()),
                Field("Included baggage", $"{first.BaggageAllowanceKg} kg"),
                Field("Seat", first.SeatNumber)
            });
        }
        else
        {
            AppendConnectionItinerary(builder, orderedTickets);
        }

        AppendSection(builder, "Payment summary", new[]
        {
            Field("Amount", FormatMoney(orderedTickets.Sum(ticket => ticket.Price))),
            Field("Currency", "USD")
        });

        builder.AppendLine("Thank you for choosing KodFly. Have a pleasant flight!");
        return builder.ToString();
    }

    private static string BuildHtml(IReadOnlyCollection<TicketDto> tickets, string logoSource)
    {
        var orderedTickets = OrderTickets(tickets);
        var first = orderedTickets[0];
        var last = orderedTickets[^1];
        var isConnection = orderedTickets.Count > 1;
        var sections = new List<EmailSection>
        {
            Section("Booking reference and passenger details", new[]
            {
                Field(isConnection ? "Route reference" : "Booking reference", isConnection ? first.RouteBookingReference : BookingReference(first)),
                Field("Passenger", first.PassengerName),
                Field("Passenger email", first.PassengerEmail),
                Field("Passenger ID", first.PassengerNationalId)
            }),
            Section(isConnection ? "Route summary" : "Route and flight", isConnection
                ? new[]
                {
                    Field("Departure airport", AirportLabel(first.OriginAirportName, first.OriginAirportCode, first.Origin, first.OriginCountry)),
                    Field("Destination airport", AirportLabel(last.DestinationAirportName, last.DestinationAirportCode, last.Destination, last.DestinationCountry)),
                    Field("Flights", string.Join(" + ", orderedTickets.Select(ticket => ticket.FlightNumber))),
                    Field("Connections", (orderedTickets.Count - 1).ToString(CultureInfo.InvariantCulture)),
                    Field("Total connection wait", TotalWaitLabel(orderedTickets))
                }
                : new[]
                {
                    Field("Departure airport", AirportLabel(first.OriginAirportName, first.OriginAirportCode, first.Origin, first.OriginCountry)),
                    Field("Destination airport", AirportLabel(first.DestinationAirportName, first.DestinationAirportCode, first.Destination, first.DestinationCountry)),
                    Field("Airline", first.AirlineName),
                    Field("Flight number", first.FlightNumber)
                })
        };

        if (!isConnection)
        {
            sections.Add(Section("Schedule", new[]
            {
                Field("Departure", FormatDateTime(first.DepartureTime)),
                Field("Arrival", FormatDateTime(first.ArrivalTime))
            }));

            sections.Add(Section("Travel options", new[]
            {
                Field("Travel class", first.Class.ToString()),
                Field("Included baggage", $"{first.BaggageAllowanceKg} kg"),
                Field("Seat", first.SeatNumber)
            }));
        }

        sections.Add(Section("Payment summary", new[]
        {
            Field("Amount", FormatMoney(orderedTickets.Sum(ticket => ticket.Price))),
            Field("Currency", "USD")
        }));

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
                      <p style="font-size:16px;line-height:1.65;margin:0;color:#415466;">Hello {{Html(first.PassengerName)}},</p>
                      <p style="font-size:16px;line-height:1.65;margin:4px 0 0 0;color:#415466;">{{Html(isConnection ? $"Thank you for choosing KodFly. We booked {orderedTickets.Count} connected flights for your route." : "Thank you for choosing KodFly. Here are your booking details:")}}</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 34px 20px 34px;">
                      {{RenderSections(sections)}}
                      {{(isConnection ? RenderConnectionItinerary(orderedTickets) : "")}}
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

    private static string RenderConnectionItinerary(IReadOnlyList<TicketDto> tickets)
    {
        var rows = tickets.Select((ticket, index) =>
        {
            var wait = index < tickets.Count - 1
                ? $"""<p style="margin:10px 0 0 0;color:#0f66a7;font-size:14px;font-weight:800;">Connection wait: {Html(WaitLabel(ticket, tickets[index + 1]))}</p>"""
                : "";

            return $$"""
              <tr>
                <td style="padding:16px 0;border-top:1px solid #dbe5ee;">
                  <div style="font-size:12px;text-transform:uppercase;color:#6a7a8a;font-weight:800;">Leg {{index + 1}} of {{tickets.Count}} - Ticket #{{ticket.Id}}</div>
                  <div style="font-size:17px;line-height:1.45;color:#102338;font-weight:800;margin-top:6px;">{{Html(ticket.FlightNumber)}}: {{Html(Place(ticket.Origin, ticket.OriginCountry))}} to {{Html(Place(ticket.Destination, ticket.DestinationCountry))}}</div>
                  <div style="font-size:14px;line-height:1.6;color:#415466;margin-top:6px;">{{Html(FormatDateTime(ticket.DepartureTime))}} to {{Html(FormatDateTime(ticket.ArrivalTime))}}</div>
                  <div style="font-size:14px;line-height:1.6;color:#415466;">Seat {{Html(ticket.SeatNumber)}} - {{Html(ticket.Class.ToString())}} - {{ticket.BaggageAllowanceKg}} kg - {{Html(FormatMoney(ticket.Price))}}</div>
                  {{wait}}
                </td>
              </tr>
            """;
        });

        return $"""
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #dbe5ee;margin-top:20px;">
            <tr>
              <td style="padding:20px 0 6px 0;text-align:left;">
                <h2 style="font-size:17px;line-height:1.35;color:#0f3761;margin:0;font-weight:800;">Connection itinerary</h2>
              </td>
            </tr>
            {string.Join("", rows)}
          </table>
        """;
    }

    private static void AppendConnectionItinerary(StringBuilder builder, IReadOnlyList<TicketDto> tickets)
    {
        builder.AppendLine("Connection itinerary");
        for (var index = 0; index < tickets.Count; index++)
        {
            var ticket = tickets[index];
            builder.AppendLine($"Leg {index + 1} of {tickets.Count}");
            builder.AppendLine($"Booking reference: {BookingReference(ticket)}");
            builder.AppendLine($"Flight: {ticket.FlightNumber}");
            builder.AppendLine($"Route: {Place(ticket.Origin, ticket.OriginCountry)} to {Place(ticket.Destination, ticket.DestinationCountry)}");
            builder.AppendLine($"Departure: {FormatDateTime(ticket.DepartureTime)}");
            builder.AppendLine($"Arrival: {FormatDateTime(ticket.ArrivalTime)}");
            builder.AppendLine($"Seat: {ticket.SeatNumber}");
            builder.AppendLine($"Class: {ticket.Class}");
            builder.AppendLine($"Baggage: {ticket.BaggageAllowanceKg} kg");
            builder.AppendLine($"Amount: {FormatMoney(ticket.Price)}");
            if (index < tickets.Count - 1)
            {
                builder.AppendLine($"Connection wait: {WaitLabel(ticket, tickets[index + 1])}");
            }

            builder.AppendLine();
        }
    }

    private static void AppendSection(StringBuilder builder, string title, IEnumerable<EmailField> fields)
    {
        builder.AppendLine(title);
        foreach (var field in fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)))
        {
            builder.AppendLine($"{field.Label}: {field.Value}");
        }

        builder.AppendLine();
    }

    private static IReadOnlyList<TicketDto> OrderTickets(IReadOnlyCollection<TicketDto> tickets) =>
        tickets
            .OrderBy(ticket => ticket.RouteLegIndex ?? 0)
            .ThenBy(ticket => ticket.DepartureTime)
            .ToArray();

    private static string TotalWaitLabel(IReadOnlyList<TicketDto> tickets)
    {
        var minutes = 0;
        for (var index = 0; index < tickets.Count - 1; index++)
        {
            minutes += Math.Max(0, (int)Math.Round((tickets[index + 1].DepartureTime - tickets[index].ArrivalTime).TotalMinutes));
        }

        return DurationLabel(minutes);
    }

    private static string WaitLabel(TicketDto previous, TicketDto next) =>
        DurationLabel(Math.Max(0, (int)Math.Round((next.DepartureTime - previous.ArrivalTime).TotalMinutes)));

    private static string DurationLabel(int minutes) => $"{minutes / 60}h {minutes % 60:00}m";

    private static string Place(string city, string? country) =>
        string.Join(", ", new[] { city, country }.Where(value => !string.IsNullOrWhiteSpace(value)));

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

    private static string FormatDateTime(DateTime value) => value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    private static string FormatMoney(decimal value) => $"${value.ToString("0.00", CultureInfo.InvariantCulture)}";

    private static string Text(string? value) => value ?? "";

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");

    private static string HtmlAttribute(string? value) => Html(value);

    private record EmailSection(string Title, IReadOnlyCollection<EmailField> Fields);

    private record EmailField(string Label, string? Value);
}
