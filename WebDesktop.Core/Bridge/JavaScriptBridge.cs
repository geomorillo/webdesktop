using System.Text.Json;

namespace WebDesktop.Core.Bridge
{
    public class JavaScriptBridge
    {
        private readonly IJSExecutor jsExecutor;

        public JavaScriptBridge(IJSExecutor jsExecutor)
        {
            this.jsExecutor = jsExecutor;
        }

        public async Task InvokeJavaScriptMethod(string methodName, params object[] args)
        {
            var script = $"window.{methodName}.apply(null, {JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false })});";
            await jsExecutor.ExecuteScriptAsync(script);
        }

        public async Task SetProperty(string propertyPath, object value)
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var script = $"window.{propertyPath} = {serializedValue};";
            await jsExecutor.ExecuteScriptAsync(script);
        }

        public Dictionary<string, Func<string, Task>> Callbacks { get; } = new Dictionary<string, Func<string, Task>>();

        public async Task RegisterCallback(string methodName, Func<string, Task> handler)
        {
            Callbacks[methodName] = handler;
            var script = $$"""
        window.{{methodName}} = (args) => {
            window.Externo.InvokeDotNetMethodAsync('{{methodName}}', JSON.stringify(args[0]));
        };
    """;
            await jsExecutor.ExecuteScriptAsync(script);
        }

        public async Task HandleEvent(string elementId, string eventName, string handlerName)
        {
            var script = $"document.getElementById('{elementId}').addEventListener('{eventName}', (e) => {{ window.{handlerName}(e); }});";
            await jsExecutor.ExecuteScriptAsync(script);
        }
    }
}
