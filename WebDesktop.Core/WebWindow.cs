using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms;

namespace WebDesktop.Core
{
    public class WebWindow : Form
    {
        private readonly WebView2 webView;
        public static CoreWebView2Environment SharedEnvironment { get; private set; }

        public class ExternalInvoker : IDisposable
    {
        private readonly WebWindow _window;
        private readonly Dictionary<string, Func<string, Task>> _handlers = new();

        public ExternalInvoker(WebWindow window)
        {
            _window = window;
        }

        public void RegisterHandler(string methodName, Func<string, Task> handler)
        {
            _handlers[methodName] = handler;
        }

        public async Task InvokeDotNetMethodAsync(string methodName, string argsJson)
        {
            if (_handlers.TryGetValue(methodName, out var handler))
            {
                await handler(argsJson);
            }
        }

        public void Dispose() => _handlers.Clear();
    }

    public ExternalInvoker External { get; private set; }

    public WebWindow(string title = "WebDesktop Application", int width = 800, int height = 600)
        {
            Text = title;
            ClientSize = new Size(width, height);
            
            webView = new WebView2();
            External = new ExternalInvoker(this);
            webView.Dock = DockStyle.Fill;
            Controls.Add(webView);

        }

        /// <summary>
        /// Adds a top-level menu item to the window
        /// </summary>
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
        /// Adds a submenu item to a parent menu
        /// </summary>
        public void AddMenuItem(ToolStripMenuItem parent, string text, EventHandler onClick)
        {
            var menuItem = new ToolStripMenuItem(text);
            menuItem.Click += onClick;
            parent.DropDownItems.Add(menuItem);
        }

        public virtual async Task InitializeAsync(CoreWebView2EnvironmentOptions options = null)
        {
            // Initialize WebView2 environment
            if (WebWindow.SharedEnvironment == null)
            {
                WebWindow.SharedEnvironment = await CoreWebView2Environment.CreateAsync(null, null, options);
            }
            await webView.EnsureCoreWebView2Async(WebWindow.SharedEnvironment);
            webView.CoreWebView2.AddHostObjectToScript("external", External);

            // Configure WebView2 settings
            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

            // Setup message handlers
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        public event EventHandler<WebMessageEventArgs>? WebMessageReceived = null!; // Null-forgiving operator for nullable event

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            WebMessageReceived?.Invoke(this, new WebMessageEventArgs(e.WebMessageAsJson));
        }

        public Task NavigateToString(string html)
        {
            webView.CoreWebView2.NavigateToString(html);
            return Task.CompletedTask;

        }

        public async Task ExecuteScriptAsync(string script)
        {
            if (webView.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        public string GetBrowserVersion()
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "No detectado";
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