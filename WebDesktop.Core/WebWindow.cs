using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using WebDesktop.Core.Bridge;

namespace WebDesktop.Core
{
    public class WebWindow : Form, IJSExecutor
    {
        private readonly WebView2 webView;
        private WebView2Configuration? _configuration;

        public static CoreWebView2Environment? SharedEnvironment { get; set; }

        public sealed class ExternalInvoker : IDisposable
        {
            private readonly ConcurrentDictionary<string, Func<string, Task<string>>> _handlers = new();

            public void RegisterHandler(string methodName, Func<string, Task<string>> handler)
            {
                _handlers[methodName] = handler;
            }

            public async Task<string> InvokeDotNetMethodAsync(string methodName, string argsJson)
            {
                if (_handlers.TryGetValue(methodName, out var handler))
                {
                    return await handler(argsJson);
                }
                return JsonSerializer.Serialize(new { error = "Handler no encontrado" });
            }

            public void Dispose()
            {
                _handlers.Clear();
            }
        }

        public ExternalInvoker Externo { get; } = new ExternalInvoker();

        public WebWindow(string title = "WebDesktop Application", int width = 800, int height = 600)
        {
            Text = title;
            ClientSize = new Size(width, height);
            _configuration = new WebView2Configuration();

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            Controls.Add(webView);
        }

        public WebWindow(WebView2Configuration configuration, string title = "WebDesktop Application", int width = 800, int height = 600)
        {
            Text = title;
            ClientSize = new Size(width, height);
            _configuration = configuration;

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            Controls.Add(webView);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                }
                Externo.Dispose();
                webView.Dispose();
            }
            base.Dispose(disposing);
        }

        public void AddMenu(string text)
        {
            if (MainMenuStrip == null)
            {
                MainMenuStrip = new MenuStrip();
                Controls.Add(MainMenuStrip);
            }

            var menuItem = new ToolStripMenuItem(text);
            MainMenuStrip.Items.Add(menuItem);
        }

        public void AddMenuItem(ToolStripMenuItem parent, string text, EventHandler onClick)
        {
            var menuItem = new ToolStripMenuItem(text);
            menuItem.Click += onClick;
            parent.DropDownItems.Add(menuItem);
        }

        protected virtual async Task<CoreWebView2Environment> CreateEnvironmentAsync(CoreWebView2EnvironmentOptions? options = null)
        {
            var config = _configuration;
            return await CoreWebView2Environment.CreateAsync(
                config?.BrowserExecutableFolder,
                config?.UserDataFolder,
                options ?? config?.ToEnvironmentOptions());
        }

        public virtual async Task InitializeAsync()
        {
            await InitializeAsync(_configuration);
        }

        public virtual async Task InitializeAsync(WebView2Configuration? configuration)
        {
            if (configuration != null)
                _configuration = configuration;

            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(version))
                {
                    throw new WebDesktopException("WebView2 Runtime no está instalado o no se pudo detectar la versión");
                }

                if (WebWindow.SharedEnvironment == null)
                {
                    WebWindow.SharedEnvironment = await CreateEnvironmentAsync(_configuration?.ToEnvironmentOptions());
                }

                try
                {
                    await webView.EnsureCoreWebView2Async(WebWindow.SharedEnvironment);
                }
                catch (InvalidCastException ex) when (ex.Message.Contains("ICoreWebView2Controller"))
                {
                    throw new WebDesktopException("Error de compatibilidad con WebView2 Runtime. Por favor actualice a la versión más reciente", ex);
                }

                if (webView.CoreWebView2 == null)
                {
                    throw new WebDesktopException("No se pudo inicializar CoreWebView2");
                }

                webView.CoreWebView2.Settings.IsScriptEnabled = _configuration?.IsScriptEnabled ?? true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = _configuration?.AllowDevTools ?? true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = _configuration?.AllowContextMenus ?? true;

                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    window.WebDesktop = {
                        _pending: {},
                        _nextId: 1,
                        invoke: function(method, args) {
                            var self = this;
                            var id = self._nextId++;
                            return new Promise(function(resolve, reject) {
                                self._pending[id] = { resolve: resolve, reject: reject };
                                try {
                                    chrome.webview.postMessage(JSON.stringify({
                                        type: 'invoke',
                                        id: id,
                                        method: method,
                                        args: args !== undefined ? JSON.stringify(args) : ''
                                    }));
                                } catch (e) {
                                    delete self._pending[id];
                                    reject(e);
                                }
                            });
                        }
                    };
                    chrome.webview.addEventListener('message', function(e) {
                        var msg = e.data;
                        if (msg.type === 'result' && msg.id && window.WebDesktop._pending[msg.id]) {
                            if (msg.error) {
                                window.WebDesktop._pending[msg.id].reject(new Error(msg.error));
                            } else {
                                window.WebDesktop._pending[msg.id].resolve(msg.result);
                            }
                            delete window.WebDesktop._pending[msg.id];
                        }
                    });
                ");
            }
            catch (WebDesktopException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new WebDesktopException("Error durante la inicialización de WebView2", ex);
            }
        }

        public event EventHandler<WebMessageEventArgs>? WebMessageReceived;

        protected virtual void OnWebMessageReceived(WebMessageEventArgs e)
        {
            WebMessageReceived?.Invoke(this, e);
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using var outerDoc = JsonDocument.Parse(e.WebMessageAsJson);
                var raw = outerDoc.RootElement.GetString();
                if (raw == null) return;

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var type) && type.GetString() == "invoke")
                {
                    var id = root.GetProperty("id").GetInt64();
                    var method = root.GetProperty("method").GetString()!;
                    var args = root.TryGetProperty("args", out var argsEl) ? argsEl.GetString() ?? "{}" : "{}";

                    try
                    {
                        var result = await Externo.InvokeDotNetMethodAsync(method, args);
                        webView.CoreWebView2?.PostWebMessageAsJson(
                            JsonSerializer.Serialize(new { type = "result", id, result }));
                    }
                    catch (Exception ex)
                    {
                        webView.CoreWebView2?.PostWebMessageAsJson(
                            JsonSerializer.Serialize(new { type = "result", id, error = ex.Message }));
                    }
                }
                else
                {
                    OnWebMessageReceived(new WebMessageEventArgs(e.WebMessageAsJson));
                }
            }
            catch
            {
            }
        }

        public virtual Task NavigateToString(string html)
        {
            string fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', system-ui, sans-serif; background: #fff; color: #222; padding: 20px; }}
        input, button {{ font-family: inherit; font-size: 14px; padding: 6px 12px; margin: 4px 0; }}
        button {{ background: #0078d4; color: #fff; border: none; border-radius: 4px; cursor: pointer; }}
        button:hover {{ background: #106ebe; }}
        pre {{ background: #f5f5f5; border: 1px solid #ddd; border-radius: 4px; padding: 10px; }}
    </style>
</head>
<body>
    {html}
</body>
</html>";

            webView.CoreWebView2.NavigateToString(fullHtml);
            return Task.CompletedTask;
        }

        public virtual async Task ExecuteScriptAsync(string script)
        {
            if (webView.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        public string GetBrowserVersion()
        {
            try
            {
                if (webView.CoreWebView2 == null)
                    return "WebView2 no inicializado o no listo";

                if (WebWindow.SharedEnvironment == null)
                    return "Entorno WebView2 no inicializado";

                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(version))
                {
                    version = webView.CoreWebView2.Environment?.BrowserVersionString;
                }

                return string.IsNullOrEmpty(version)
                    ? "Versión WebView2: No detectada"
                    : $"Versión WebView2: {version}";
            }
            catch (Exception ex)
            {
                return $"Error obteniendo versión: {ex.Message}";
            }
        }

        public string GetAppStatus()
        {
            var status = new StringBuilder();
            status.AppendLine($"Estado de la aplicación:");
            status.AppendLine($"- Versión WebView2: {GetBrowserVersion()}");
            status.AppendLine($"- WebView inicializado: {(webView.CoreWebView2 != null ? "Sí" : "No")}");
            status.AppendLine($"- Entorno compartido: {(WebWindow.SharedEnvironment != null ? "Creado" : "No creado")}");
            return status.ToString();
        }
    }
}

public class WebMessageEventArgs : EventArgs
{
    public string Message { get; }

    public WebMessageEventArgs(string message)
    {
        Message = message;
    }
}
