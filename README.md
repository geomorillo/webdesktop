# WebDesktop

> **Framework híbrido C# + WebView2 para aplicaciones de escritorio Windows modernas.**  
> Sin XAML. Sin Electron. Sin Node.

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
![Windows](https://img.shields.io/badge/Windows-WinForms-0078D6)
![WebView2](https://img.shields.io/badge/WebView2-✓-brightgreen)

---

## ✨ Features

- **Sin XAML** — toda la UI en HTML + CSS + JS
- **Comunicación bidireccional** C# ↔ JavaScript nativa, sin bridge intermedio
- **Diálogos nativos** desde JS: MessageBox, OpenFile, SaveFile, FolderBrowser
- **Assets locales** — sirve archivos desde el sistema de archivos sin servidor HTTP
- **Menús** de ventana y **bandeja del sistema** desde C#
- **Un solo Runtime** — WebView2 compartido entre todas las ventanas

---

## 📦 Instalación

```bash
dotnet add reference WebDesktop.Core/WebDesktop.Core.csproj
```

Requiere .NET 9.0 y [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

---

## 🚀 Uso

```csharp
using WebDesktop.Core;

var window = new WebWindow("Mi App", 1024, 768);

window.Shown += async (_, _) =>
{
    await window.InitializeAsync();

    // Handler C# invocable desde JavaScript
    window.Externo.RegisterHandler("saludar", (json) =>
    {
        var args = JsonSerializer.Deserialize<JsonElement>(json);
        var nombre = args.GetProperty("nombre").GetString() ?? "Mundo";
        return Task.FromResult(
            JsonSerializer.Serialize(new { mensaje = $"Hola {nombre}!" }));
    });

    await window.NavigateToString(@"
        <h1>Hola Mundo</h1>
        <input id='nombreInput' placeholder='Tu nombre' />
        <button id='btn'>Saludar</button>
        <p id='output'></p>
        <script>
            document.getElementById('btn').onclick = async () => {
                var r = await window.WebDesktop.invoke('saludar', { nombre: document.getElementById('nombreInput').value });
                document.getElementById('output').textContent = r.mensaje;
            };
        </script>
    ");
};

Application.Run(window);
```

---

## 🧱 ¿Por qué WebDesktop?

| Alternativa | Problema |
|-------------|----------|
| **WPF / WinForms** | Boilerplate excesivo (XAML, INotifyPropertyChanged, DataTemplates, IValueConverter) para una app LOB simple |
| **Electron** | 150 MB+ por app, consume 500 MB de RAM, usa Node y Chromium completos |
| **MAUI** | Multiplataforma forzado cuando solo necesitas Windows, tooling aún inmaduro |
| **Blazor Hybrid** | Dependencia de ASP.NET Core, generación de código, curva de aprendizaje |

**WebDesktop** da justo lo necesario: una ventana con WebView2, puente de comunicación, diálogos nativos y assets locales. Nada más.

---

## 🏗️ Arquitectura

```
┌─────────────────────────────────────────────┐
│  WebWindow (Form + WebView2)                 │
│  ┌─────────────────────────────────────────┐ │
│  │  HTML / CSS / JS  (UI)                  │ │
│  │  window.WebDesktop.invoke(method, args) │ │
│  │      ↕ chrome.webview.postMessage       │ │
│  ├─────────────────────────────────────────┤ │
│  │  ExternalInvoker  (Handlers C#)         │ │
│  │  RegisterHandler → ConcurrentDictionary │ │
│  ├─────────────────────────────────────────┤ │
│  │  Built-in: MessageBox, OpenFile,        │ │
│  │           SaveFile, FolderBrowser        │ │
│  └─────────────────────────────────────────┘ │
│  ↑ IJSExecutor  ←  JavaScriptBridge (C#→JS)  │
└─────────────────────────────────────────────┘
```

Cada llamada JS → C# usa un sistema request/response con IDs, thread-safe vía `ConcurrentDictionary`.

---

## 📚 API

| Clase | Propósito |
|-------|-----------|
| `WebWindow` | Ventana principal con WebView2 embebido |
| `WebView2Configuration` | Configuración del entorno WebView2 |
| `ExternalInvoker` | Registro de handlers C# invocables desde JS |
| `JavaScriptBridge` | Helper para invocar JS desde C# |
| `WebDesktopException` | Excepción base del framework |

### WebWindow

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Inicializa WebView2 (obligatorio antes de navegar) |
| `NavigateToString(html)` | Navega a HTML inline con estilos/scripts inyectados |
| `AddMenu(text)` | Agrega menú a la barra de menú |
| `AddMenuItem(parent, text, onClick)` | Agrega elemento a un menú |
| `InjectGlobalScript(script)` | Inyecta JS en toda página HTML |
| `InjectGlobalStyle(css)` | Inyecta CSS en toda página HTML |
| `EnableTrayIcon(text, iconFile?)` | Habilita bandeja del sistema |
| `SetAssetFolder(folder, virtualHost?)` | Mapea carpeta local a host virtual |
| `NavigateToAsset(htmlFile?)` | Navega a HTML desde carpeta de assets |
| `ExecuteScriptAsync(script)` | Ejecuta JS en el WebView2 |
| `GetBrowserVersion()` | Versión del runtime WebView2 |

| Evento | Descripción |
|--------|-------------|
| `OnBridgeReady` | El puente JS `window.WebDesktop.invoke` está listo |
| `WebMessageReceived` | Mensaje JS no procesado recibido |
| `OnNavigating` / `OnNavigated` | Inicio / fin de navegación |
| `FormClosingEvent` | Ventana por cerrarse (cancelable) |

---

## 💡 Ejemplos

### Assets locales
```csharp
window.SetAssetFolder("wwwroot", "app.local");
await window.NavigateToAsset("index.html");
```

### Configuración personalizada
```csharp
var config = new WebView2Configuration
{
    Language = "es",
    AllowDevTools = false,
    UserDataFolder = "my-app-data"
};
var window = new WebWindow(config, "App", 1024, 768);
```

### Diálogos nativos desde JS
```javascript
await window.WebDesktop.invoke('__dialog.showMessage', {
    text: '¿Guardar cambios?',
    buttons: 'YesNo',
    icon: 'Question'
});
```

---

## 🗂️ Estructura del proyecto

```
WebDesktop/
├── WebDesktop.Core/          ← Librería core
│   ├── WebWindow.cs
│   ├── WebView2Configuration.cs
│   ├── WebDesktopException.cs
│   └── Bridge/
│       ├── IJSExecutor.cs
│       └── JavaScriptBridge.cs
├── TestApp/                  ← App demo: gestor de tareas SQLite
├── FileCompressor/           ← App demo: compresor ZIP
└── WebDesktop.Core.Tests/    ← Tests unitarios (NUnit)
```

---

## 🧪 Tests

```bash
dotnet test WebDesktop.Core.Tests/WebDesktop.Core.Tests.csproj
```

---

## 📄 Licencia

MIT
