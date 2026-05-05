using System.Text.Json;
using WebDesktop.Core;

namespace FileCompressor.Services;

static class ResponseHelper
{
    public static string Ok(object? data = null) =>
        JsonSerializer.Serialize(new { success = true, error = (string?)null, data }, WebWindow.JsonOptions);

    public static string Error(string message) =>
        JsonSerializer.Serialize(new { success = false, error = message }, WebWindow.JsonOptions);
}
