using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GDriveDownloader;

internal sealed class GoogleLoginForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly Button _doneButton = new() { Text = "完成登入", Dock = DockStyle.Bottom, Height = 40 };

    public GoogleLoginForm()
    {
        Text = "登入 Google 帳號";
        Width = 900;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        Theme.ApplyForm(this);
        Theme.StylePrimaryButton(_doneButton);

        Controls.Add(_webView);
        Controls.Add(_doneButton);

        _doneButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        Load += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2ProfileDir);
        await _webView.EnsureCoreWebView2Async(env);
        _webView.CoreWebView2.Navigate("https://drive.google.com/");
    }
}
