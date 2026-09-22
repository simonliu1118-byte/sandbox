using System.Text;
using Microsoft.Web.WebView2.Core;

namespace GDriveDownloader;

internal static class CookieExporter
{
    private static readonly string[] Domains =
    {
        "https://drive.google.com",
        "https://docs.google.com",
        "https://accounts.google.com",
        "https://www.google.com",
    };

    private static readonly string[] SessionCookieNames =
    {
        "SID",
        "SSID",
        "SAPISID",
        "__Secure-1PSID",
        "__Secure-3PSID",
    };

    public static async Task<bool> ExportAsync(CoreWebView2 webView, string destinationPath)
    {
        var seen = new HashSet<string>();
        var sb = new StringBuilder();
        sb.AppendLine("# Netscape HTTP Cookie File");

        var hasSessionCookie = false;

        foreach (var domain in Domains)
        {
            var cookies = await webView.CookieManager.GetCookiesAsync(domain);
            foreach (var cookie in cookies)
            {
                var key = cookie.Domain + "|" + cookie.Name + "|" + cookie.Path;
                if (!seen.Add(key))
                {
                    continue;
                }

                if (Array.IndexOf(SessionCookieNames, cookie.Name) >= 0)
                {
                    hasSessionCookie = true;
                }

                var includeSubdomains = cookie.Domain.StartsWith('.') ? "TRUE" : "FALSE";
                var secure = cookie.IsSecure ? "TRUE" : "FALSE";
                var expires = cookie.IsSession ? 0L : (long)cookie.Expires;

                sb.AppendLine(string.Join(
                    '\t',
                    cookie.Domain,
                    includeSubdomains,
                    cookie.Path,
                    secure,
                    expires,
                    cookie.Name,
                    cookie.Value));
            }
        }

        await File.WriteAllTextAsync(destinationPath, sb.ToString(), Encoding.UTF8);
        return hasSessionCookie;
    }
}
