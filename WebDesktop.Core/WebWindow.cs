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
        private readonly List<string> _globalScripts = new();
        private readonly List<string> _globalStyles = new();
        private NotifyIcon? _trayIcon;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

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
                return JsonSerializer.Serialize(new { error = "Handler no encontrado" }, JsonOptions);
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
            FormClosing += WebWindow_FormClosing;

            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            Controls.Add(webView);
        }

        public WebWindow(WebView2Configuration configuration, string title = "WebDesktop Application", int width = 800, int height = 600)
        {
            Text = title;
            ClientSize = new Size(width, height);
            _configuration = configuration;
            FormClosing += WebWindow_FormClosing;

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
                    webView.CoreWebView2.NavigationStarting -= CoreWebView2_NavigationStarting;
                    webView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
                }
                _trayIcon?.Dispose();
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

        public void InjectGlobalScript(string script)
        {
            _globalScripts.Add(script);
        }

        public void InjectGlobalStyle(string css)
        {
            _globalStyles.Add(css);
        }

        public void EnableTrayIcon(string iconText, string? iconFile = null)
        {
            _trayIcon = new NotifyIcon();
            if (iconFile != null)
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(iconFile);
            else
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            _trayIcon.Text = iconText;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; };

            var trayMenu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("Show", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; });
            var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => Application.Exit());
            trayMenu.Items.Add(showItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitItem);
            _trayIcon.ContextMenuStrip = trayMenu;

            Resize += (_, _) =>
            {
                if (WindowState == FormWindowState.Minimized)
                    Hide();
            };
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
                webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

                RegisterBuiltInHandlers();

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

                OnBridgeReady?.Invoke(this, EventArgs.Empty);
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

        private void RegisterBuiltInHandlers()
        {
            Externo.RegisterHandler("__dialog.showMessage", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var text = args.GetProperty("text").GetString() ?? "";
                var caption = args.TryGetProperty("caption", out var c) ? c.GetString() ?? "" : "";
                var buttons = args.TryGetProperty("buttons", out var b) ? b.GetString() ?? "OK" : "OK";
                var icon = args.TryGetProperty("icon", out var i) ? i.GetString() ?? "None" : "None";

                var msgButtons = buttons switch
                {
                    "OKCancel" => MessageBoxButtons.OKCancel,
                    "YesNo" => MessageBoxButtons.YesNo,
                    "YesNoCancel" => MessageBoxButtons.YesNoCancel,
                    _ => MessageBoxButtons.OK
                };
                var msgIcon = icon switch
                {
                    "Info" => MessageBoxIcon.Information,
                    "Warning" => MessageBoxIcon.Warning,
                    "Error" => MessageBoxIcon.Error,
                    "Question" => MessageBoxIcon.Question,
                    _ => MessageBoxIcon.None
                };

                var result = MessageBox.Show(this, text, caption, msgButtons, msgIcon);
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    result = result.ToString()
                }, JsonOptions));
            });

            Externo.RegisterHandler("__dialog.selectFolder", (json) =>
            {
                using var dialog = new FolderBrowserDialog();
                var ok = dialog.ShowDialog(this) == DialogResult.OK;
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    ok,
                    path = ok ? dialog.SelectedPath : null
                }, JsonOptions));
            });

            Externo.RegisterHandler("__dialog.openFile", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var filter = args.TryGetProperty("filter", out var f) ? f.GetString() : null;
                var multi = args.TryGetProperty("multi", out var m) && m.GetBoolean();

                using var dialog = new OpenFileDialog();
                dialog.Multiselect = multi;
                if (filter != null) dialog.Filter = filter;

                var ok = dialog.ShowDialog(this) == DialogResult.OK;
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    ok,
                    file = ok ? dialog.FileName : null,
                    files = ok ? dialog.FileNames : Array.Empty<string>()
                }, JsonOptions));
            });

            Externo.RegisterHandler("__dialog.saveFile", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var filter = args.TryGetProperty("filter", out var f) ? f.GetString() : null;
                var defaultName = args.TryGetProperty("defaultName", out var d) ? d.GetString() : null;

                using var dialog = new SaveFileDialog();
                if (filter != null) dialog.Filter = filter;
                if (defaultName != null) dialog.FileName = defaultName;

                var ok = dialog.ShowDialog(this) == DialogResult.OK;
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    ok,
                    file = ok ? dialog.FileName : null
                }, JsonOptions));
            });

            Externo.RegisterHandler("__dialog.selectFolder", (json) =>
            {
                using var dialog = new FolderBrowserDialog();
                var ok = dialog.ShowDialog(this) == DialogResult.OK;
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    ok,
                    path = ok ? dialog.SelectedPath : null
                }, JsonOptions));
            });
        }

        public event EventHandler? OnBridgeReady;
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
                            JsonSerializer.Serialize(new { type = "result", id, result }, JsonOptions));
                    }
                    catch (Exception ex)
                    {
                        webView.CoreWebView2?.PostWebMessageAsJson(
                            JsonSerializer.Serialize(new { type = "result", id, error = ex.Message }, JsonOptions));
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

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            OnNavigating?.Invoke(this, EventArgs.Empty);
        }

        private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            OnNavigated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OnNavigating;
        public event EventHandler? OnNavigated;
        public event EventHandler<FormClosingEventArgs>? FormClosingEvent;

        private void WebWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            FormClosingEvent?.Invoke(this, e);
        }


        public string DefaultStyles
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine("* { margin: 0; padding: 0; box-sizing: border-box; }");
                sb.AppendLine("body { font-family: 'Segoe UI', system-ui, sans-serif; background: #fff; color: #222; padding: 20px; }");
                sb.AppendLine("input, button { font-family: inherit; font-size: 14px; padding: 6px 12px; margin: 4px 0; }");
                sb.AppendLine("button { background: #0078d4; color: #fff; border: none; border-radius: 4px; cursor: pointer; }");
                sb.AppendLine("button:hover { background: #106ebe; }");
                sb.AppendLine("pre { background: #f5f5f5; border: 1px solid #ddd; border-radius: 4px; padding: 10px; }");
                foreach (var style in _globalStyles)
                    sb.AppendLine(style);
                return sb.ToString();
            }
        }

        public virtual Task NavigateToString(string html)
        {
            var scriptsBuilder = new StringBuilder();
            foreach (var script in _globalScripts)
                scriptsBuilder.AppendLine($"<script>{script}</script>");

            string fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        {DefaultStyles}
    </style>
</head>
<body>
    {html}
    {scriptsBuilder}
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
