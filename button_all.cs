using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyLoginApp
{
    public sealed class ButtonAll
    {
        private readonly WebView2 _wv;
        public event Action? BackHomeClicked;

        private ButtonAll(WebView2 wv) => _wv = wv;

        public static async Task<ButtonAll> WireAsync(WebView2 wv)
        {
            var x = new ButtonAll(wv);
            await x.InitAsync();
            return x;
        }

        private async Task InitAsync()
        {
            await _wv.EnsureCoreWebView2Async();
            _wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
            _wv.CoreWebView2.WebMessageReceived += OnMsg;
        }

        private void OnMsg(object? s, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                if (!doc.RootElement.TryGetProperty("type", out var t)) return;

                if (t.GetString() == "back_home")
                    BackHomeClicked?.Invoke();
            }
            catch { }
        }
    }
}
