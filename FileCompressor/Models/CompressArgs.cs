namespace FileCompressor.Models;

public record CompressArgs(string SourcePath, string DestinationZip);
public record DecompressArgs(string SourceZip, string DestinationFolder);
public record ZipArgs(string ZipPath);
public record PathArgs(string Path);
