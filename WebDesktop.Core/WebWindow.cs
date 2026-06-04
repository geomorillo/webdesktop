using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using WebDesktop.Core.Bridge;

namespace WebDesktop.Core
{
    /// <summary>
    /// Ventana principal del framework WebDesktop. Hereda de <see cref="Form"/> e implementa
    /// un WebView2 a pantalla completa con puente de comunicación bidireccional C# ↔ JavaScript.
    /// Proporciona menús, diálogos nativos, assets locales, bandeja de sistema e inyección
    /// de scripts/estilos globales.
    /// </summary>
    public class WebWindow : Form, IJSExecutor
    {
        private readonly WebView2 webView;
        private WebView2Configuration? _configuration;
        private readonly List<string> _globalScripts = new();
        private readonly List<string> _globalStyles = new();
        private NotifyIcon? _trayIcon;

        /// <summary>
        /// Opciones de serialización JSON estándar del framework: camelCase, sin indentación.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Entorno WebView2 compartido entre todas las ventanas de la aplicación.
        /// Se crea una sola vez en <see cref="InitializeAsync()"/> y se reusa para evitar
        /// la sobrecarga de múltiples procesos del navegador.
        /// </summary>
        public static CoreWebView2Environment? SharedEnvironment { get; set; }

        /// <summary>
        /// Invocador de métodos de C# desde JavaScript. Mantiene un diccionario thread-safe
        /// de handlers registrados y los ejecuta cuando JS envía un mensaje de tipo "invoke".
        /// </summary>
        public sealed class ExternalInvoker : IDisposable
        {
            private readonly ConcurrentDictionary<string, Func<string, Task<string>>> _handlers = new();

            /// <summary>
            /// Registra un handler invocable desde JavaScript. El handler recibe un string JSON
            /// con los argumentos y debe retornar un string JSON como respuesta.
            /// </summary>
            /// <param name="methodName">Nombre del método que JS usará para invocar.</param>
            /// <param name="handler">Función asíncrona que recibe JSON y retorna JSON.</param>
            public void RegisterHandler(string methodName, Func<string, Task<string>> handler)
            {
                _handlers[methodName] = handler;
            }

        /// <summary>
        /// Invoca un handler registrado por su nombre. Usado internamente por el puente JS
        /// cuando recibe un mensaje <c>chrome.webview.postMessage</c> de tipo "invoke".
        /// </summary>
        /// <param name="methodName">Nombre del handler a invocar.</param>
        /// <param name="argsJson">Argumentos en formato JSON.</param>
        /// <returns>Resultado del handler en formato JSON, o un error si no se encuentra el handler.</returns>
            public async Task<string> InvokeDotNetMethodAsync(string methodName, string argsJson)
            {
                if (_handlers.TryGetValue(methodName, out var handler))
                {
                    return await handler(argsJson);
                }
                return JsonSerializer.Serialize(new { error = "Handler no encontrado" }, JsonOptions);
            }

        /// <summary>
        /// Limpia todos los handlers registrados liberando referencias.
        /// </summary>
            public void Dispose()
            {
                _handlers.Clear();
            }
        }

        /// <summary>
        /// Acceso al invocador de métodos externos. Los handlers registrados aquí
        /// son invocables desde JavaScript mediante <c>chrome.webview.postMessage</c>.
        /// </summary>
        public ExternalInvoker Externo { get; } = new ExternalInvoker();
        /// <summary>
        /// Crea una nueva ventana WebDesktop con configuración predeterminada.
        /// </summary>
        /// <param name="title">Título de la ventana.</param>
        /// <param name="width">Ancho inicial en píxeles. Valor predeterminado: 800.</param>
        /// <param name="height">Alto inicial en píxeles. Valor predeterminado: 600.</param>
        public WebWindow(string title = "WebDesktop Application", int width = 800, int height = 600)
            : this(new WebView2Configuration(), title, width, height)
        {
        }

        /// <summary>
        /// Crea una nueva ventana WebDesktop con configuración personalizada.
        /// </summary>
        /// <param name="configuration">Configuración del entorno WebView2.</param>
        /// <param name="title">Título de la ventana.</param>
        /// <param name="width">Ancho inicial en píxeles. Valor predeterminado: 800.</param>
        /// <param name="height">Alto inicial en píxeles. Valor predeterminado: 600.</param>
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

        /// <summary>
        /// Libera los recursos administrados y no administrados. Desuscribe eventos de WebView2,
        /// limpia el invocador externo, destruye el icono de bandeja y el control WebView2.
        /// </summary>
        /// <param name="disposing"><c>true</c> si se están liberando recursos administrados.</param>
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

        /// <summary>
        /// Agrega un menú a la barra de menú de la ventana. Si no existe <see cref="Form.MainMenuStrip"/>,
        /// lo crea automáticamente.
        /// </summary>
        /// <param name="text">Texto del menú (ej: "File", "Editar").</param>
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

        /// <summary>
        /// Agrega un elemento a un menú existente.
        /// </summary>
        /// <param name="parent">Menú padre donde se agregará el elemento.</param>
        /// <param name="text">Texto del elemento de menú.</param>
        /// <param name="onClick">Handler del evento click.</param>
        public void AddMenuItem(ToolStripMenuItem parent, string text, EventHandler onClick)
        {
            var menuItem = new ToolStripMenuItem(text);
            menuItem.Click += onClick;
            parent.DropDownItems.Add(menuItem);
        }

        /// <summary>
        /// Agrega un script JavaScript que se inyectará en toda página HTML cargada
        /// mediante <see cref="NavigateToString"/>.
        /// </summary>
        /// <param name="script">Código JavaScript a inyectar.</param>
        public void InjectGlobalScript(string script)
        {
            _globalScripts.Add(script);
        }

        /// <summary>
        /// Agrega una regla CSS que se aplicará en toda página HTML cargada
        /// mediante <see cref="NavigateToString"/>.
        /// </summary>
        /// <param name="css">Código CSS a inyectar.</param>
        public void InjectGlobalStyle(string css)
        {
            _globalStyles.Add(css);
        }

        /// <summary>
        /// Habilita el icono en la bandeja del sistema. Al minimizar la ventana,
        /// se oculta automáticamente en la bandeja. Doble click restaura la ventana.
        /// </summary>
        /// <param name="iconText">Texto que se muestra al hacer hover sobre el icono.</param>
        /// <param name="iconFile">Ruta opcional al archivo .ico. Si es null, usa el icono predeterminado de la aplicación.</param>
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

        /// <summary>
        /// Crea el entorno WebView2. Se puede sobreescribir para personalizar la creación
        /// del entorno (ej: usar una carpeta de datos específica).
        /// </summary>
        /// <param name="options">Opciones adicionales del entorno WebView2.</param>
        /// <returns>El entorno WebView2 creado.</returns>
        protected virtual async Task<CoreWebView2Environment> CreateEnvironmentAsync(CoreWebView2EnvironmentOptions? options = null)
        {
            var config = _configuration;
            return await CoreWebView2Environment.CreateAsync(
                config?.BrowserExecutableFolder,
                config?.UserDataFolder,
                options ?? config?.ToEnvironmentOptions());
        }

        /// <summary>
        /// Inicializa el WebView2 con la configuración actual. Debe llamarse antes de
        /// cualquier navegación o interacción con el navegador.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            await InitializeAsync(_configuration);
        }

        /// <summary>
        /// Inicializa el WebView2 con una configuración específica, opcionalmente
        /// reemplazando la configuración actual.
        /// </summary>
        /// <param name="configuration">Configuración a usar. Si es null, mantiene la configuración anterior.</param>
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

        /// <summary>
        /// Se dispara cuando el puente de comunicación con JavaScript está listo.
        /// En este punto, <c>window.WebDesktop.invoke</c> ya está disponible en JS.
        /// </summary>
        public event EventHandler? OnBridgeReady;
        /// <summary>
        /// Se dispara cuando se recibe un mensaje desde JavaScript que no es del tipo "invoke".
        /// </summary>
        public event EventHandler<WebMessageEventArgs>? WebMessageReceived;

        /// <summary>
        /// Dispara el evento <see cref="WebMessageReceived"/>. Las subclases pueden
        /// sobreescribir este método para interceptar mensajes JS no procesados.
        /// </summary>
        /// <param name="e">Datos del mensaje recibido.</param>
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

        /// <summary>
        /// Se dispara cuando el WebView2 comienza a navegar a una nueva URL.
        /// </summary>
        public event EventHandler? OnNavigating;
        /// <summary>
        /// Se dispara cuando el WebView2 completa la navegación a una URL.
        /// </summary>
        public event EventHandler? OnNavigated;
        /// <summary>
        /// Se dispara cuando la ventana está por cerrarse. Permite cancelar el cierre.
        /// </summary>
        public event EventHandler<FormClosingEventArgs>? FormClosingEvent;

        private void WebWindow_FormClosing(object? sender, FormClosingEventArgs e)
        {
            FormClosingEvent?.Invoke(this, e);
        }


        /// <summary>
        /// Obtiene los estilos CSS predeterminados incluyendo los estilos globales
        /// agregados con <see cref="InjectGlobalStyle"/>. Se usa en <see cref="NavigateToString"/>.
        /// </summary>
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

        /// <summary>
        /// Navega a un string HTML. Inyecta automáticamente los estilos predeterminados,
        /// los estilos globales y los scripts globales registrados.
        /// </summary>
        /// <param name="html">Contenido HTML a mostrar.</param>
        /// <returns>Una tarea que representa la operación.</returns>
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

        /// <summary>
        /// Ejecuta un script JavaScript en el contexto del WebView2.
        /// Implementa <see cref="IJSExecutor.ExecuteScriptAsync"/>.
        /// </summary>
        /// <param name="script">Código JavaScript a ejecutar.</param>
        public virtual async Task ExecuteScriptAsync(string script)
        {
            if (webView.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private string? _assetFolder;
        private string? _virtualHost;

        /// <summary>
        /// Mapea una carpeta local a un nombre de host virtual para servir archivos
        /// HTML/CSS/JS estáticos desde el sistema de archivos.
        /// </summary>
        /// <param name="folderPath">Ruta a la carpeta que contiene los assets.</param>
        /// <param name="virtualHost">Nombre de host virtual (predeterminado: "app.local").</param>
        /// <exception cref="WebDesktopException">Si <see cref="InitializeAsync()"/> no se ha llamado aún o la carpeta no existe.</exception>
        public void SetAssetFolder(string folderPath, string virtualHost = "app.local")
        {
            if (webView.CoreWebView2 == null)
                throw new WebDesktopException("Debe llamar a InitializeAsync antes de SetAssetFolder");

            var fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
                throw new WebDesktopException($"La carpeta no existe: {fullPath}");

            _assetFolder = fullPath;
            _virtualHost = virtualHost;

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                virtualHost,
                fullPath,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        /// <summary>
        /// Navega a un archivo HTML dentro de la carpeta de assets configurada con
        /// <see cref="SetAssetFolder"/>.
        /// </summary>
        /// <param name="htmlFile">Nombre del archivo HTML (predeterminado: "index.html").</param>
        /// <exception cref="WebDesktopException">Si no se ha llamado a <see cref="SetAssetFolder"/> primero.</exception>
        public Task NavigateToAsset(string htmlFile = "index.html")
        {
            if (_virtualHost == null)
                throw new WebDesktopException("Debe llamar a SetAssetFolder antes de NavigateToAsset");

            webView.CoreWebView2.Navigate($"http://{_virtualHost}/{htmlFile}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Obtiene la versión del runtime WebView2 instalado en el sistema.
        /// </summary>
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

        /// <summary>
        /// Obtiene un resumen del estado actual de la aplicación incluyendo versión
        /// de WebView2 y estado de inicialización.
        /// </summary>
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

/// <summary>
/// Argumentos del evento <c>WebMessageReceived</c> de la clase <c>WebWindow</c>.
/// Contiene el mensaje JSON enviado desde JavaScript.
/// </summary>
public class WebMessageEventArgs : EventArgs
{
    /// <summary>
    /// Contenido del mensaje recibido desde JavaScript.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Crea una nueva instancia con el mensaje recibido.
    /// </summary>
    /// <param name="message">Contenido del mensaje JSON.</param>
    public WebMessageEventArgs(string message)
    {
        Message = message;
    }
}
