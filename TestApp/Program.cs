using WebDesktop.Core;
using System.Windows.Forms;

namespace TestApp
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            
            var window = new WebWindow("Prueba WebDesktop", 1024, 768);
            
            // Initialize async after showing the window
            window.Shown += async (sender, e) => 
            {
                await window.InitializeAsync();
                
                // Add test menu items
                window.AddMenu("File");
                var fileMenu = (ToolStripMenuItem)window.MainMenuStrip.Items[0];
                window.AddMenuItem(fileMenu, "Exit", (s, e) => Application.Exit());
                window.AddMenuItem(fileMenu, "Nueva Ventana", async (s, e) => {
                    try
                    {
                        var modal = new ModalWindow("<html><body><h1>Contenido Modal</h1><p>¡Cargado correctamente!</p></body></html>");
                        modal.Owner = window;
                        await modal.InitializeAsync();
                        modal.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al abrir ventana modal: {ex.Message}");
                    }
                });
                window.AddMenuItem(fileMenu, "About", (s, e) => MessageBox.Show("WebDesktop Test App\nVersion 1.0"));
                await window.NavigateToString($"<h1>¡Funciona correctamente!</h1><p>Versión WebView2: {window.GetBrowserVersion()}</p>");
            };
            
            Application.Run(window);
        }
    }
}