using System.Text.Json;

namespace WebDesktop.Core.Bridge
{
    public class JavaScriptBridge
    {
        private readonly WebWindow window;

        public JavaScriptBridge(WebWindow window)
        {
            this.window = window;
        }

        public async Task InvokeJavaScriptMethod(string methodName, params object[] args)
        {
            await window.ExecuteScriptAsync($"{methodName}({JsonSerializer.Serialize(args)});");
        }

        public async Task SetProperty(string propertyPath, object value)
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var script = $"window.{propertyPath} = {serializedValue};";
            await window.ExecuteScriptAsync(script);
        }
    }
}