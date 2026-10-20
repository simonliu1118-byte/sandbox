using System.Drawing;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GDriveDownloader;

internal static class LoginStatusChecker
{
    private static readonly string[] ProbeDomains =
    {
        "https://drive.google.com",
        "https://www.google.com",
        "https://accounts.google.com",
    };

    public static async Task<bool> IsLoggedInAsync()
    {
        using var probe = new WebView2();
        using var host = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.FixedToolWindow,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-3000, -3000),
            Size = new Size(200, 200),
        };
        host.Controls.Add(probe);
        host.Show();

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2ProfileDir);
            await probe.EnsureCoreWebView2Async(env);

            foreach (var domain in ProbeDomains)
            {
                var cookies = await probe.CoreWebView2.CookieManager.GetCookiesAsync(domain);
                if (cookies.Any(c =>
                        c.Domain.Contains("google.com", StringComparison.OrdinalIgnoreCase) &&
                        (c.Name.Contains("SID", StringComparison.OrdinalIgnoreCase) ||
                         c.Name.Contains("LSID", StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            host.Close();
        }
    }
}
