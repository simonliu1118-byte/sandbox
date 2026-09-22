using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace GDriveDownloader;

internal sealed class DevToolsSession
{
    private readonly CoreWebView2 _webView;

    public DevToolsSession(CoreWebView2 webView)
    {
        _webView = webView;
    }

    public async Task<JsonElement> SendAsync(string method, object? parameters = null)
    {
        var json = parameters is null ? "{}" : JsonSerializer.Serialize(parameters);
        var resultJson = await _webView.CallDevToolsProtocolMethodAsync(method, json);
        using var document = JsonDocument.Parse(resultJson);
        return document.RootElement.Clone();
    }

    public IDisposable Subscribe(string eventName, Action<JsonElement> handler)
    {
        var receiver = _webView.GetDevToolsProtocolEventReceiver(eventName);

        void OnEvent(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
        {
            using var document = JsonDocument.Parse(e.ParameterObjectAsJson);
            handler(document.RootElement.Clone());
        }

        receiver.DevToolsProtocolEventReceived += OnEvent;
        return new Unsubscriber(() => receiver.DevToolsProtocolEventReceived -= OnEvent);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly Action _dispose;

        public Unsubscriber(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }
}
