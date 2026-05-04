# WebDesktop — MVP

Framework híbrido C# + WebView2 para aplicaciones de escritorio modernas. Sin XAML.

## Requisitos

- .NET 9.0
- Microsoft Edge WebView2 Runtime
- Windows (WinForms)

## MVP — Cómo se usa

```csharp
using WebDesktop.Core;
using System.Text.Json;

var window = new WebWindow("Mi App", 1024, 768);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();

    // Menú básico
    window.AddMenu("File");
    var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
    window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

    // Registrar handlers C# invocables desde JS
    window.Externo.RegisterHandler("saludar", (json) =>
    {
        var args = JsonSerializer.Deserialize<JsonElement>(json);
        var nombre = args.GetProperty("nombre").GetString() ?? "Mundo";
        return Task.FromResult(
            JsonSerializer.Serialize(new { mensaje = $"Hola {nombre}!" }));
    });

    // HTML con comunicación directa a C#
    await window.NavigateToString(@"
        <h1>Hola Mundo</h1>
        <input id='nombreInput' placeholder='Tu nombre' />
        <button id='btn'>Saludar</button>
        <p id='output'></p>
        <script>
            async function invocar(method, args) {
                return JSON.parse(await chrome.webview.hostObjects.async
                    .Externo.InvokeDotNetMethodAsync(method, JSON.stringify(args)));
            }
            document.getElementById('btn').onclick = async () => {
                var r = await invocar('saludar', { nombre: document.getElementById('nombreInput').value });
                document.getElementById('output').textContent = r.mensaje;
            };
        </script>
    ");
};

Application.Run(window);
```

## API

| Clase | Propósito |
|-------|-----------|
| `WebWindow` | Ventana principal con WebView2 embebido |
| `WebView2Configuration` | Configuración del entorno WebView2 |
| `JavaScriptBridge` | Helper para invocar JS desde C# |
| `WebDesktopException` | Excepción base del framework |

## Proyectos

| Proyecto | Descripción |
|----------|-------------|
| `WebDesktop.Core` | Librería core |
| `TestApp` | App demo / MVP |
| `WebDesktop.Core.Tests` | Tests unitarios (NUnit) |

## Licencia

MIT
