using System.Text.Json;
using WebDesktop.Core;
using FileCompressor.Models;

namespace FileCompressor.Services;

public class FileInfoService
{
    public Task<string> GetFileInfo(string json)
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
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        exists = true,
                        isDirectory = false,
                        name = fi.Name,
                        size = fi.Length,
                        lastModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    }, WebWindow.JsonOptions);
                }

                if (Directory.Exists(args.Path))
                {
                    var di = new DirectoryInfo(args.Path);
                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        exists = true,
                        isDirectory = true,
                        name = di.Name,
                        size = 0L,
                        lastModified = di.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    }, WebWindow.JsonOptions);
                }

                return JsonSerializer.Serialize(new { success = true, exists = false }, WebWindow.JsonOptions);
            }
            catch (Exception ex) { return ResponseHelper.Error(ex.Message); }
        });
    }
}
