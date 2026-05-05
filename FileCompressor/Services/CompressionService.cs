using System.IO.Compression;
using System.Text.Json;
using WebDesktop.Core;
using FileCompressor.Models;

namespace FileCompressor.Services;

public class CompressionService
{
    public Task<string> Compress(string json)
    {
        return Task.Run(() =>
        {
            try
            {
                var args = JsonSerializer.Deserialize<CompressArgs>(json, WebWindow.JsonOptions)
                    ?? throw new ArgumentException("Invalid arguments");

                if (!File.Exists(args.SourcePath) && !Directory.Exists(args.SourcePath))
                    return ResponseHelper.Error("Source file or folder not found");

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
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Compressed successfully",
                    size = fiResult.Length,
                    path = args.DestinationZip
                }, WebWindow.JsonOptions);
            }
            catch (Exception ex) { return ResponseHelper.Error(ex.Message); }
        });
    }

    public Task<string> Decompress(string json)
    {
        return Task.Run(() =>
        {
            try
            {
                var args = JsonSerializer.Deserialize<DecompressArgs>(json, WebWindow.JsonOptions)
                    ?? throw new ArgumentException("Invalid arguments");

                if (!File.Exists(args.SourceZip))
                    return ResponseHelper.Error("Zip file not found");

                if (!Directory.Exists(args.DestinationFolder))
                    Directory.CreateDirectory(args.DestinationFolder);

                ZipFile.ExtractToDirectory(args.SourceZip, args.DestinationFolder, overwriteFiles: true);

                var entries = ZipFile.OpenRead(args.SourceZip).Entries.Count;
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"Extracted {entries} entries successfully",
                    entries,
                    path = args.DestinationFolder
                }, WebWindow.JsonOptions);
            }
            catch (Exception ex) { return ResponseHelper.Error(ex.Message); }
        });
    }

    public Task<string> ListZipContents(string json)
    {
        return Task.Run(() =>
        {
            try
            {
                var args = JsonSerializer.Deserialize<ZipArgs>(json, WebWindow.JsonOptions)
                    ?? throw new ArgumentException("Invalid arguments");

                if (!File.Exists(args.ZipPath))
                    return ResponseHelper.Error("Zip file not found");

                using var archive = ZipFile.OpenRead(args.ZipPath);
                var entries = archive.Entries.Select(e => new
                {
                    name = e.FullName,
                    size = e.Length,
                    compressedSize = e.CompressedLength,
                    isDirectory = e.Length == 0 && e.FullName.EndsWith("/"),
                    lastModified = e.LastWriteTime.DateTime.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

                return JsonSerializer.Serialize(new { success = true, entries }, WebWindow.JsonOptions);
            }
            catch (Exception ex) { return ResponseHelper.Error(ex.Message); }
        });
    }
}
