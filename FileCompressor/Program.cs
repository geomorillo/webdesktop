using WebDesktop.Core;
using System.IO.Compression;
using System.Text.Json;

namespace FileCompressor;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("File Compressor", 850, 600);

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            RegisterHandlers(window);

            window.SetAssetFolder("wwwroot");
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }

    static void RegisterHandlers(WebWindow window)
    {
        window.Externo.RegisterHandler("compress", (json) =>
        {
            return Task.Run(() =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<CompressArgs>(json, WebWindow.JsonOptions)
                        ?? throw new ArgumentException("Invalid arguments");

                    if (!File.Exists(args.SourcePath) && !Directory.Exists(args.SourcePath))
                        return JsonError("Source file or folder not found");

                    var destDir = Path.GetDirectoryName(args.DestinationZip);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    if (File.Exists(args.DestinationZip))
                        File.Delete(args.DestinationZip);

                    if (File.Exists(args.SourcePath))
                    {
                        var fi = new FileInfo(args.SourcePath);
                        using var zip = ZipFile.Open(args.DestinationZip, ZipArchiveMode.Create);
                        zip.CreateEntryFromFile(args.SourcePath, fi.Name);
                    }
                    else
                    {
                        ZipFile.CreateFromDirectory(args.SourcePath, args.DestinationZip);
                    }

                    var fiResult = new FileInfo(args.DestinationZip);
                    return Json(new
                    {
                        success = true,
                        message = "Compressed successfully",
                        size = fiResult.Length,
                        path = args.DestinationZip
                    });
                }
                catch (Exception ex)
                {
                    return JsonError(ex.Message);
                }
            });
        });

        window.Externo.RegisterHandler("decompress", (json) =>
        {
            return Task.Run(() =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<DecompressArgs>(json, WebWindow.JsonOptions)
                        ?? throw new ArgumentException("Invalid arguments");

                    if (!File.Exists(args.SourceZip))
                        return JsonError("Zip file not found");

                    if (!Directory.Exists(args.DestinationFolder))
                        Directory.CreateDirectory(args.DestinationFolder);

                    ZipFile.ExtractToDirectory(args.SourceZip, args.DestinationFolder, overwriteFiles: true);

                    var entries = ZipFile.OpenRead(args.SourceZip).Entries.Count;
                    return Json(new
                    {
                        success = true,
                        message = $"Extracted {entries} entries successfully",
                        entries,
                        path = args.DestinationFolder
                    });
                }
                catch (Exception ex)
                {
                    return JsonError(ex.Message);
                }
            });
        });

        window.Externo.RegisterHandler("listZipContents", (json) =>
        {
            return Task.Run(() =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<ZipArgs>(json, WebWindow.JsonOptions)
                        ?? throw new ArgumentException("Invalid arguments");

                    if (!File.Exists(args.ZipPath))
                        return JsonError("Zip file not found");

                    using var archive = ZipFile.OpenRead(args.ZipPath);
                    var entries = archive.Entries.Select(e => new
                    {
                        name = e.FullName,
                        size = e.Length,
                        compressedSize = e.CompressedLength,
                        isDirectory = e.Length == 0 && e.FullName.EndsWith("/"),
                        lastModified = e.LastWriteTime.DateTime.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToList();

                    return Json(new { success = true, entries });
                }
                catch (Exception ex)
                {
                    return JsonError(ex.Message);
                }
            });
        });

        window.Externo.RegisterHandler("getFileInfo", (json) =>
        {
            return Task.Run(() =>
            {
                try
                {
                    var args = JsonSerializer.Deserialize<PathArgs>(json, WebWindow.JsonOptions)
                        ?? throw new ArgumentException("Invalid arguments");

                    if (File.Exists(args.Path))
                    {
                        var fi = new FileInfo(args.Path);
                        return Json(new
                        {
                            success = true,
                            exists = true,
                            isDirectory = false,
                            name = fi.Name,
                            size = fi.Length,
                            lastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    else if (Directory.Exists(args.Path))
                    {
                        var di = new DirectoryInfo(args.Path);
                        return Json(new
                        {
                            success = true,
                            exists = true,
                            isDirectory = true,
                            name = di.Name,
                            size = 0L,
                            lastModified = di.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }

                    return Json(new { success = true, exists = false });
                }
                catch (Exception ex)
                {
                    return JsonError(ex.Message);
                }
            });
        });
    }

    static string Json(object obj)
    {
        return JsonSerializer.Serialize(obj, WebWindow.JsonOptions);
    }

    static string JsonError(string message)
    {
        return Json(new { success = false, error = message });
    }

    record CompressArgs(string SourcePath, string DestinationZip);
    record DecompressArgs(string SourceZip, string DestinationFolder);
    record ZipArgs(string ZipPath);
    record PathArgs(string Path);
}
