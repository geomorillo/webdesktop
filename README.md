# WebDesktop

> **Framework híbrido C# + WebView2 para aplicaciones de escritorio Windows modernas**  
> **Hybrid C# + WebView2 framework for modern Windows desktop applications**

**Sin XAML · Without XAML**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
![Windows](https://img.shields.io/badge/Windows-WinForms-0078D6)
![WebView2](https://img.shields.io/badge/WebView2-1.0.3065.39-brightgreen)

---

## 📋 Requisitos / Requirements

- **.NET 9.0** (SDK + Runtime)
- **Microsoft Edge WebView2 Runtime** ([descargar / download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/))
- **Windows** (10 / 11) — usa WinForms como contenedor

---

## 🚀 Inicio rápido / Quick Start

### Español

```csharp
using WebDesktop.Core;
using System.Text.Json;

var window = new WebWindow("Mi App", 1024, 768);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();

    // Menú básico
    window.AddMenu("Archivo");
    var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
    window.AddMenuItem(fileMenu, "Salir", (_, _) => Application.Exit());

    // Handler C# invocable desde JS
    window.Externo.RegisterHandler("saludar", (json) =>
    {
        var args = JsonSerializer.Deserialize<JsonElement>(json);
        var nombre = args.GetProperty("nombre").GetString() ?? "Mundo";
        return Task.FromResult(
            JsonSerializer.Serialize(new { mensaje = $"Hola {nombre}!" }));
    });

    // HTML embebido con JS → C#
    await window.NavigateToString(@"
        <h1>Hola Mundo</h1>
        <input id='nombreInput' placeholder='Tu nombre' />
        <button id='btn'>Saludar</button>
        <p id='output'></p>
        <script>
            async function invocar(method, args) {
                return JSON.parse(await chrome.webview.postMessage(
                    JSON.stringify({ type: 'invoke', id: 1, method: method, args: JSON.stringify(args) })));
            }
            // Alternativa más simple usando window.WebDesktop.invoke:
            document.getElementById('btn').onclick = async () => {
                var r = await window.WebDesktop.invoke('saludar', { nombre: document.getElementById('nombreInput').value });
                document.getElementById('output').textContent = r.mensaje;
            };
        </script>
    ");
};

Application.Run(window);
```

### English

```csharp
using WebDesktop.Core;
using System.Text.Json;

var window = new WebWindow("My App", 1024, 768);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();

    window.AddMenu("File");
    var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
    window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

    window.Externo.RegisterHandler("greet", (json) =>
    {
        var args = JsonSerializer.Deserialize<JsonElement>(json);
        var name = args.GetProperty("name").GetString() ?? "World";
        return Task.FromResult(
            JsonSerializer.Serialize(new { message = $"Hello {name}!" }));
    });

    await window.NavigateToString(@"
        <h1>Hello World</h1>
        <input id='nameInput' placeholder='Your name' />
        <button id='btn'>Greet</button>
        <p id='output'></p>
        <script>
            document.getElementById('btn').onclick = async () => {
                var r = await window.WebDesktop.invoke('greet', { name: document.getElementById('nameInput').value });
                document.getElementById('output').textContent = r.message;
            };
        </script>
    ");
};

Application.Run(window);
```

---

## 📦 Proyectos / Projects

| Proyecto | Descripción | Description |
|----------|-------------|-------------|
| `WebDesktop.Core` | Librería core del framework | Core framework library |
| `TestApp` | App demo: gestor de tareas con SQLite | Demo app: task manager with SQLite |
| `FileCompressor` | App demo: compresor ZIP | Demo app: ZIP compressor |
| `WebDesktop.Core.Tests` | Tests unitarios (NUnit + Moq) | Unit tests (NUnit + Moq) |

---

## 🧱 Arquitectura / Architecture

```
┌──────────────────────────────────────────────┐
│  WebWindow (Form + WebView2)                  │
│  ┌──────────────────────────────────────────┐ │
│  │  HTML / CSS / JS  (UI Render)            │ │
│  │  window.WebDesktop.invoke(method, args)  │ │
│  │      ↕ chrome.webview.postMessage        │ │
│  ├──────────────────────────────────────────┤ │
│  │  ExternalInvoker  (C# Handlers)          │ │
│  │  RegisterHandler → ConcurrentDictionary  │ │
│  ├──────────────────────────────────────────┤ │
│  │  Built-in Handlers:                      │ │
│  │  • __dialog.showMessage  (MessageBox)    │ │
│  │  • __dialog.openFile     (OpenFileDialog) │ │
│  │  • __dialog.saveFile     (SaveFileDialog) │ │
│  │  • __dialog.selectFolder (FolderBrowser)  │ │
│  └──────────────────────────────────────────┘ │
│  ↑ IJSExecutor  ←  JavaScriptBridge (C#→JS)  │
└──────────────────────────────────────────────┘
```

### Comunicación C# ↔ JavaScript

1. **JS → C#**: `window.WebDesktop.invoke(method, args)` → `chrome.webview.postMessage` → `ExternalInvoker.InvokeDotNetMethodAsync`
2. **C# → JS**: `JavaScriptBridge.InvokeJavaScriptMethod(name, args)` → `webView.CoreWebView2.ExecuteScriptAsync`
3. **Eventos JS → C#**: Mensajes que no son tipo "invoke" disparan `WebMessageReceived`
4. **Eventos C# → JS**: Via `OnBridgeReady`, `OnNavigating`, `OnNavigated`, `FormClosingEvent`

---

## 📚 API Reference

### `WebWindow`

Clase principal que representa la ventana de la aplicación. Hereda de `Form` e implementa `IJSExecutor`.

| Miembro | Tipo | Descripción |
|---------|------|-------------|
| `WebWindow(title, width, height)` | Constructor | Crea ventana con configuración predeterminada |
| `WebWindow(config, title, width, height)` | Constructor | Crea ventana con configuración personalizada |
| `JsonOptions` | `static` | Opciones JSON estándar del framework (camelCase) |
| `SharedEnvironment` | `static` | Entorno WebView2 compartido (singleton) |
| `Externo` | Property | Invocador de handlers C# desde JS |
| `DefaultStyles` | Property | CSS predeterminado + estilos globales inyectados |

**Métodos públicos:**

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Inicializa WebView2. **Debe llamarse antes de cualquier navegación** |
| `InitializeAsync(config)` | Inicializa con configuración específica |
| `AddMenu(text)` | Agrega un menú a la barra de menú |
| `AddMenuItem(parent, text, onClick)` | Agrega un elemento a un menú existente |
| `InjectGlobalScript(script)` | Inyecta JS en toda página HTML cargada con `NavigateToString` |
| `InjectGlobalStyle(css)` | Inyecta CSS en toda página HTML cargada con `NavigateToString` |
| `EnableTrayIcon(text, iconFile?)` | Habilita icono en bandeja del sistema |
| `SetAssetFolder(folder, virtualHost?)` | Mapea carpeta local a host virtual |
| `NavigateToAsset(htmlFile?)` | Navega a HTML desde carpeta de assets |
| `NavigateToString(html)` | Navega a HTML embebido (con estilos/scripts inyectados) |
| `ExecuteScriptAsync(script)` | Ejecuta JS en el WebView2 |
| `GetBrowserVersion()` | Obtiene versión del runtime WebView2 |
| `GetAppStatus()` | Obtiene resumen del estado de la aplicación |

**Eventos:**

| Evento | Descripción |
|--------|-------------|
| `OnBridgeReady` | El puente JS `window.WebDesktop.invoke` está listo |
| `WebMessageReceived` | Mensaje JS no procesado recibido |
| `OnNavigating` | El WebView2 comienza a navegar |
| `OnNavigated` | El WebView2 completó la navegación |
| `FormClosingEvent` | La ventana está por cerrarse (cancelable) |

### `ExternalInvoker` (inner class)

Invocador thread-safe de handlers C# desde JavaScript.

| Método | Descripción |
|--------|-------------|
| `RegisterHandler(name, handler)` | Registra handler C# invocable desde JS |
| `InvokeDotNetMethodAsync(name, json)` | Invoca handler registrado (uso interno) |
| `Dispose()` | Limpia todos los handlers |

### `WebView2Configuration`

Configuración del entorno WebView2.

| Propiedad | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| `UserDataFolder` | `string?` | `null` | Carpeta de datos de usuario (cookies, caché) |
| `BrowserExecutableFolder` | `string?` | `null` | Ruta al runtime WebView2 |
| `Language` | `string?` | `null` | Código de idioma (ej: "es", "en-US") |
| `AdditionalBrowserArguments` | `string?` | `null` | Argumentos extra del navegador |
| `AllowDevTools` | `bool` | `true` | Habilita F12/herramientas de desarrollo |
| `AllowContextMenus` | `bool` | `true` | Habilita menús contextuales |
| `IsScriptEnabled` | `bool` | `true` | Habilita JavaScript |

### `JavaScriptBridge`

Puente para invocar JS desde C# y registrar callbacks.

| Método | Descripción |
|--------|-------------|
| `InvokeJavaScriptMethod(name, args)` | Invoca método en `window` |
| `SetProperty(path, value)` | Asigna propiedad en `window` (soporta rutas anidadas) |
| `RegisterCallback(name, handler)` | Registra callback JS → C# |
| `HandleEvent(elementId, eventName, handlerName)` | Conecta evento DOM a callback |

### `WebDesktopException`

Excepción base del framework. Hereda de `InvalidOperationException`.

### `IJSExecutor`

Interfaz que abstrae la ejecución de JavaScript. Implementada por `WebWindow`.

---

## 💼 Ejemplos de uso / Usage Examples

### App con assets locales / Local assets app

```csharp
var window = new WebWindow("Mi App", 1024, 768);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();
    window.SetAssetFolder("wwwroot", "app.local");
    await window.NavigateToAsset("index.html");

    window.Externo.RegisterHandler("getData", async (json) =>
    {
        // Lógica de negocio C#
        return JsonSerializer.Serialize(new { result = "datos desde C#" });
    });
};

Application.Run(window);
```

### Ventana con bandeja de sistema / Tray icon window

```csharp
var window = new WebWindow("App", 800, 600);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();
    window.EnableTrayIcon("Mi App en segundo plano");
};

// La ventana se minimiza a la bandeja automáticamente
Application.Run(window);
```

### Diálogos nativos desde JS / Native dialogs from JS

```javascript
// Mensaje
const r = await window.WebDesktop.invoke('__dialog.showMessage', { text: '¿Guardar cambios?', buttons: 'YesNo', icon: 'Question' });

// Abrir archivo
const file = await window.WebDesktop.invoke('__dialog.openFile', { filter: 'Text files (*.txt)|*.txt', multi: false });

// Guardar archivo
const save = await window.WebDesktop.invoke('__dialog.saveFile', { filter: 'PDF|*.pdf', defaultName: 'reporte.pdf' });

// Seleccionar carpeta
const folder = await window.WebDesktop.invoke('__dialog.selectFolder', {});
```

---

## 🔧 Configuración avanzada / Advanced Configuration

### Personalizar entorno WebView2

```csharp
var config = new WebView2Configuration
{
    UserDataFolder = Path.Combine(AppContext.BaseDirectory, "webview2-data"),
    Language = "es",
    AllowDevTools = false,       // Producción
    AllowContextMenus = false,
    IsScriptEnabled = true
};

var window = new WebWindow(config, "App Segura", 1024, 768);
```

### Entorno compartido / Shared environment

Por defecto, todas las ventanas reusan un mismo `CoreWebView2Environment`. Para forzar entornos separados:

```csharp
WebWindow.SharedEnvironment = null;  // Cada ventana crea su propio entorno
```

### Menús anidados / Nested menus

```csharp
window.AddMenu("Editar");
var editMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
window.AddMenuItem(editMenu, "Deshacer", (_, _) => { /* ... */ });
window.AddMenuItem(editMenu, "Rehacer", (_, _) => { /* ... */ });
```

---

## 🧪 Tests

```bash
dotnet test WebDesktop.Core.Tests/WebDesktop.Core.Tests.csproj
```

Framework: **NUnit** + **Moq**  
Cobertura actual: `JavaScriptBridge` (5 tests unitarios)

---

## 🗺️ Roadmap / Plan de desarrollo

| Fase | Estado |
|------|--------|
| 1. Limpieza y estabilización de la base | ✅ Completado |
| 2. Refactor arquitectónico | ✅ Completado |
| 3. Features para apps de negocio | ✅ Completado |
| 4. Assets desde archivos (DX) | ✅ Completado |
| 5. Calidad y testing | ⏳ Pendiente |
| 6. Documentación | ✅ Completado |
| 7. Preparación para producción | ⏳ Pendiente |

---

## 📄 Licencia / License

MIT
