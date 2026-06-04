using System.Text.Json;

namespace WebDesktop.Core.Bridge
{
    /// <summary>
    /// Puente para invocar código JavaScript del lado del navegador desde C#,
    /// y registrar callbacks que JS puede disparar hacia C#.
    /// </summary>
    public class JavaScriptBridge
    {
        private readonly IJSExecutor jsExecutor;

        /// <summary>
        /// Crea un nuevo puente JS asociado a un ejecutor de scripts.
        /// </summary>
        /// <param name="jsExecutor">Implementación de <see cref="IJSExecutor"/> que ejecutará los scripts.</param>
        public JavaScriptBridge(IJSExecutor jsExecutor)
        {
            this.jsExecutor = jsExecutor;
        }

        /// <summary>
        /// Invoca un método JavaScript en el objeto <c>window</c> con los argumentos dados.
        /// </summary>
        /// <param name="methodName">Nombre del método en <c>window</c> (ej: "alert", "myApp.saludar").</param>
        /// <param name="args">Argumentos que se pasarán al método.</param>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        public async Task InvokeJavaScriptMethod(string methodName, params object[] args)
        {
            var script = $"window.{methodName}.apply(null, {JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false })});";
            await jsExecutor.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// Asigna un valor a una propiedad en el objeto <c>window</c>.
        /// Soporta rutas anidadas (ej: "myApp.config.theme").
        /// </summary>
        /// <param name="propertyPath">Ruta de la propiedad, separada por puntos (ej: "myApp.config.theme").</param>
        /// <param name="value">Valor a asignar. Se serializa a JSON automáticamente.</param>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        public async Task SetProperty(string propertyPath, object value)
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var script = $"window.{propertyPath} = {serializedValue};";
            await jsExecutor.ExecuteScriptAsync(script);
        }

        /// <summary>
        /// Callbacks registrados que JS puede invocar hacia C#.
        /// La clave es el nombre del método y el valor es el handler asíncrono.
        /// </summary>
        public Dictionary<string, Func<string, Task>> Callbacks { get; } = new Dictionary<string, Func<string, Task>>();

        /// <summary>
        /// Registra un callback invocable desde JavaScript hacia C#.
        /// Inyecta una función en <c>window.{methodName}</c> que, al ser llamada desde JS,
        /// ejecuta el handler asíncrono en C#.
        /// </summary>
        /// <param name="methodName">Nombre de la función que se creará en <c>window</c>.</param>
        /// <param name="handler">Handler asíncrono que recibe el argumento JSON desde JS.</param>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
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

        /// <summary>
        /// Conecta un evento del DOM a un callback registrado.
        /// Agrega un <c>addEventListener</c> al elemento del DOM especificado.
        /// </summary>
        /// <param name="elementId">ID del elemento HTML (ej: "miBoton").</param>
        /// <param name="eventName">Nombre del evento DOM (ej: "click", "change").</param>
        /// <param name="handlerName">Nombre del callback registrado con <see cref="RegisterCallback"/>.</param>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        public async Task HandleEvent(string elementId, string eventName, string handlerName)
        {
            var script = $"document.getElementById('{elementId}').addEventListener('{eventName}', (e) => {{ window.{handlerName}(e); }});";
            await jsExecutor.ExecuteScriptAsync(script);
        }
    }
}
