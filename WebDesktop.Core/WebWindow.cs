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

        private async Task<bool> RepairWebView2Runtime()
        {
            try
            {
                string bootstrapperPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    using (var fs = new FileStream(bootstrapperPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = bootstrapperPath,
                        Arguments = "/silent /install",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe")))
                    {
                        File.Delete(Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe"));
                    }
                }
                catch { }
            }
        }

        public async Task InitializeAsync()
        {
            // Verify WebView2 Runtime availability and attempt repair if needed
            string? browserVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrEmpty(browserVersion))
            {
                bool repaired = await RepairWebView2Runtime();
                if (!repaired)
                {
                    throw new InvalidOperationException("Failed to install WebView2 Runtime. Please install it manually from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/");
                }
                // Verify again after repair
                browserVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                if (string.IsNullOrEmpty(browserVersion))
                {
                    throw new InvalidOperationException("WebView2 Runtime installation verification failed");
                }
            }

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