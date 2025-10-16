// Services/EmailTemplates.cs
using System.Globalization;
using System.Text;
using EventRecommender.Models;

namespace EventRecommender.Services;

public static class EmailTemplates
{
    // bump this when you change layout to break Gmail threads/debug
    public const string PrettyTemplateVersion = "pretty-v2";

    public static string BuildPrettyDigestHtml(
        string recipientNameOrEmail,
        IEnumerable<Event> recommended,
        IEnumerable<Event> trending,
        string? appUrl = null,
        string? managePrefsUrl = null)
    {
        string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        string Row(Event e) =>
            $@"<tr>
                <td style=""padding:10px 0;border-bottom:1px solid #eef1f5;"">
                  <div style=""font-weight:600;color:#0b1220;font-size:15px;line-height:1.4;"">{H(e.Title)}</div>
                  <div style=""color:#4b5563;font-size:13px;line-height:1.5;margin-top:2px;"">
                    {H(e.DateTime.ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture))}
                    {(string.IsNullOrWhiteSpace(e.Location) ? "" : $" — {H(e.Location)}")}
                  </div>
                </td>
              </tr>";

        string Section(string title, IEnumerable<Event> items) =>
            $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
                  <tr>
                    <td style=""font-size:16px;font-weight:700;color:#0b1220;padding:2px 0 8px 0;"">{H(title)}</td>
                  </tr>
                  <tr>
                    <td>
                      <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
                        {string.Join("", items.Select(Row))}
                      </table>
                    </td>
                  </tr>
                </table>";

        var recList = recommended?.ToList() ?? new();
        var trList = trending?.ToList() ?? new();

        var cta = !string.IsNullOrWhiteSpace(appUrl)
            ? $@"<tr>
                  <td style=""padding:12px 24px 22px 24px;"">
                    <a href=""{H(appUrl)}"" style=""display:inline-block;background:#2563eb;color:#fff;border-radius:10px;padding:10px 14px;text-decoration:none;font-weight:600;font-size:14px;"">
                      Open the app →
                    </a>
                  </td>
                </tr>"
            : "";

        var prefs = !string.IsNullOrWhiteSpace(managePrefsUrl)
            ? $@"<div style=""margin-top:8px"">
                   <a href=""{H(managePrefsUrl)}"" style=""color:#2563eb;text-decoration:underline"">Manage preferences</a>
                 </div>"
            : "";

        return $@"<!doctype html>
<html>
  <body style=""margin:0;padding:0;background:#0b1220;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#0b1220;padding:24px 0;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;border-collapse:separate;background:#ffffff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.25);overflow:hidden;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Inter,Arial,sans-serif;"">
            <tr>
              <td style=""background:linear-gradient(135deg,#1f2937 0%,#0b1220 100%);padding:22px 24px;border-bottom:1px solid #101827;"">
                <div style=""color:#e0ecff;font-size:20px;font-weight:700;margin:0;"">Vaš nedeljni pregled — Eventualno</div>
                <div style=""color:#a8c3ff;font-size:13px;margin-top:4px;"">Zdravo{(string.IsNullOrWhiteSpace(recipientNameOrEmail) ? "" : $", {H(recipientNameOrEmail)}")}! Evo šta biste mogli da volite.</div>
              </td>
            </tr>

            <tr>
              <td style=""padding:20px 24px 8px 24px;"">
                {(recList.Count > 0 ? Section("Preporučeni događaji za vas:", recList)
                                    : "<div style='color:#4b5563;font-size:14px;'>Još uvek nemamo personalizovane predloge.</div>")}
              </td>
            </tr>

            <tr>
              <td style=""padding:8px 24px 20px 24px;"">
                <div style=""height:1px;background:#eef1f5;margin:8px 0 16px 0;""></div>
                {(trList.Count > 0 ? Section("Trending:", trList) : "")}
              </td>
            </tr>

            {cta}

            <tr>
              <td style=""background:#f8fafc;color:#64748b;font-size:12px;padding:14px 24px;border-top:1px solid #eef1f5;"">
                Poslato automatski iz Event Recommender-a. Ne odgovarajte na ovu poruku.
                {prefs}
                <div style=""margin-top:6px;color:#94a3b8;"">Template: {PrettyTemplateVersion}</div>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }
}
