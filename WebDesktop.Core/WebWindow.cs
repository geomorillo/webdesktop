using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Windows.Forms;

namespace WebDesktop.Core
{
    public class WebWindow : Form
    {
        private readonly WebView2 webView;

        public WebWindow(string title = "WebDesktop Application", int width = 800, int height = 600)
        {
            Text = title;
            ClientSize = new Size(width, height);
            
            webView = new WebView2();
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

        public async Task InitializeAsync()
        {
            // Verify WebView2 Runtime availability and attempt repair if needed

            // Configure WebView2 environment
            var options = new CoreWebView2EnvironmentOptions()
            {
                Language = "es-ES",
                AdditionalBrowserArguments = "--disable-features=mojo-local-storage --no-sandbox"
            };

            // Initialize WebView2 environment
            var env = await CoreWebView2Environment.CreateAsync(null, null, options);
            await webView.EnsureCoreWebView2Async(env);

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

        public async Task NavigateToString(string html)
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.NavigateToString(html);
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