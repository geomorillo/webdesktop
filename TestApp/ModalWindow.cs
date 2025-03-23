using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using WebDesktop.Core;

namespace TestApp
{
    public class ModalWindow : WebWindow
    {
        public ModalWindow(string htmlContent, string title = "Ventana Modal", int width = 400, int height = 300) : base(title, width, height)
    {
        HtmlContent = htmlContent;
        {
            StartPosition = FormStartPosition.CenterParent;
        }
    }
        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                await base.InitializeAsync();
                if (!string.IsNullOrEmpty(HtmlContent))
                {
                    await NavigateToString(HtmlContent);
                }

                Initialized?.Invoke(this, EventArgs.Empty);
            }

            catch (Exception ex)
            {
                MessageBox.Show(this, $"Modal initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        public override async Task InitializeAsync(CoreWebView2EnvironmentOptions? options = null)
        {
            // Initialization now handled in OnShown
            await Task.CompletedTask;
        }

        public event EventHandler Initialized;
        public virtual string HtmlContent { get; protected set; }
    }
}