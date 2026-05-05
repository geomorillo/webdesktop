using WebDesktop.Core;
using FileCompressor.Services;

namespace FileCompressor;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("File Compressor", 850, 600);
        var compression = new CompressionService();
        var fileInfo = new FileInfoService();

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            window.Externo.RegisterHandler("compress", compression.Compress);
            window.Externo.RegisterHandler("decompress", compression.Decompress);
            window.Externo.RegisterHandler("listZipContents", compression.ListZipContents);
            window.Externo.RegisterHandler("getFileInfo", fileInfo.GetFileInfo);

            window.SetAssetFolder("wwwroot");
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }
}
