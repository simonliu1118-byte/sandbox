using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GDownloader;

internal sealed class GoogleLoginForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly Button _doneButton = new() { Text = "完成登入，儲存登入狀態", Dock = DockStyle.Bottom, Height = 40 };

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

        _doneButton.Click += async (_, _) => await ExportAndCloseAsync();
        Load += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2ProfileDir);
        await _webView.EnsureCoreWebView2Async(env);
        _webView.CoreWebView2.Navigate("https://drive.google.com/");
    }

    private async Task ExportAndCloseAsync()
    {
        if (_webView.CoreWebView2 == null)
        {
            return;
        }

        var success = await CookieExporter.ExportAsync(_webView.CoreWebView2, AppPaths.CookiesFile);
        if (!success)
        {
            MessageBox.Show(
                this,
                "尚未偵測到有效的 Google 登入狀態，請先在上方視窗完成登入後再按下此按鈕。",
                "尚未登入",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
