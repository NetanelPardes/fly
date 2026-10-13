using Airport.Api.Models;
using System.Net;
using System.Text;

namespace Airport.Api.Services;

public static class ProfileUpdatedEmailTemplate
{
    public const string Subject = "Profile details updated | KodFly";

    public static EmailMessage Create(User user) =>
        new(
            user.Email,
            Subject,
            BuildPlainText(user),
            BuildHtml(user));

    public static EmailMessage Create(Employee employee) =>
        Create(new User
        {
            NationalId = employee.NationalId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = employee.FullName,
            Email = employee.Email,
            Gender = employee.Gender,
            Address = employee.Address,
            PhoneNumber = employee.PhoneNumber,
            Role = UserRole.Admin
        });

    private static string BuildPlainText(User user)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Your KodFly profile details were updated successfully.");
        builder.AppendLine();
        builder.AppendLine($"Hello {Text(user.FullName)},");
        builder.AppendLine("The details saved in your KodFly account were updated.");
        builder.AppendLine();
        builder.AppendLine("Updated account details:");
        AppendLine(builder, "Full name", user.FullName);
        AppendLine(builder, "Email", user.Email);
        AppendLine(builder, "Phone", user.PhoneNumber);
        AppendLine(builder, "National ID", user.NationalId);
        AppendLine(builder, "Address", user.Address);
        builder.AppendLine();
        builder.AppendLine("שלום, פרטי החשבון שלך עודכנו בהצלחה במערכת KodFly.");
        builder.AppendLine();
        builder.AppendLine("KodFly team");
        return builder.ToString();
    }

    private static string BuildHtml(User user) =>
        $$"""
        <!doctype html>
        <html lang="en" dir="ltr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>KodFly profile update</title>
        </head>
        <body style="margin:0;padding:0;background:#eef2f6;color:#102338;font-family:Heebo,Assistant,Arial,sans-serif;direction:ltr;text-align:left;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef2f6;margin:0;padding:28px 12px;direction:ltr;text-align:left;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:640px;background:#ffffff;border-radius:18px;border:1px solid #dbe5ee;overflow:hidden;box-shadow:0 16px 42px rgba(16,35,56,0.10);direction:ltr;text-align:left;">
                  <tr>
                    <td style="padding:32px 34px 20px 34px;text-align:left;">
                      <div style="font-size:26px;line-height:1.25;font-weight:800;color:#0f3761;margin:0 0 14px 0;">Your KodFly profile details were updated successfully.</div>
                      <p style="font-size:16px;line-height:1.6;margin:0;color:#415466;">Hello {{Html(user.FullName)}},</p>
                      <p style="font-size:16px;line-height:1.6;margin:6px 0 0 0;color:#415466;">The details saved in your KodFly account were updated.</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 34px 22px 34px;text-align:left;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #dbe5ee;margin-top:12px;direction:ltr;text-align:left;">
                        <tr>
                          <td style="padding:20px 0 6px 0;text-align:left;">
                            <h2 style="font-size:17px;line-height:1.35;color:#0f3761;margin:0;font-weight:800;">Updated account details</h2>
                          </td>
                        </tr>
                        {{FieldRows(new[]
                        {
                            Field("Full name", user.FullName),
                            Field("Email", user.Email),
                            Field("Phone", user.PhoneNumber),
                            Field("National ID", user.NationalId),
                            Field("Address", user.Address)
                        })}}
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 34px 30px 34px;text-align:left;">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #dbe5ee;margin-top:6px;direction:ltr;text-align:left;">
                        <tr>
                          <td style="padding:20px 0 0 0;text-align:left;">
                            <p style="font-size:15px;line-height:1.65;margin:0;color:#415466;">שלום, פרטי החשבון שלך עודכנו בהצלחה במערכת KodFly.</p>
                            <p style="font-size:15px;line-height:1.65;margin:14px 0 0 0;color:#102338;font-weight:700;">KodFly team</p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    private static string FieldRows(IEnumerable<EmailField> fields) =>
        string.Join("", fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .Select(field => $"""
              <tr>
                <td style="padding:9px 0;width:38%;font-size:13px;line-height:1.4;color:#6a7a8a;font-weight:700;text-transform:uppercase;letter-spacing:0.02em;vertical-align:top;text-align:left;">{Html(field.Label)}</td>
                <td style="padding:9px 0;font-size:15px;line-height:1.5;color:#102338;font-weight:800;vertical-align:top;text-align:left;">{Html(field.Value)}</td>
              </tr>
            """));

    private static void AppendLine(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}: {value}");
        }
    }

    private static EmailField Field(string label, string? value) => new(label, value);

    private static string Text(string? value) => value ?? "";

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? "");

    private record EmailField(string Label, string? Value);
}
