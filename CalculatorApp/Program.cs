using System.Text.Json;
using WebDesktop.Core;

namespace CalculatorApp;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("Calculadora de Ejemplo", 380, 560);

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            // El motor de cálculo vive en C#; la UI web lo invoca por el puente WebDesktop.
            window.Externo.RegisterHandler("calc.evaluate", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var expresion = args.TryGetProperty("expression", out var e) ? e.GetString() ?? "" : "";

                try
                {
                    var resultado = Calculadora.Evaluar(expresion);
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        success = true,
                        result = Calculadora.FormatearResultado(resultado)
                    }, WebWindow.JsonOptions));
                }
                catch (Exception ex)
                {
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = ex.Message
                    }, WebWindow.JsonOptions));
                }
            });

            // La carpeta wwwroot se resuelve desde la ubicación del ejecutable
            // (no del directorio de trabajo), para que la app funcione sin
            // importar desde dónde se lance.
            window.SetAssetFolder(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }
}
