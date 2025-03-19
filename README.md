# WebDesktop Hybrid Library/Framework

A .NET integration layer for creating modern desktop applications combining C# backend with WebView2 frontend. Provides both ready-to-use components (framework-style) and extensible base classes (library-style) for advanced scenarios. Built on Microsoft's WebView2 control with automatic rendering surface management.

## Features

- Hybrid architecture combining C# backend with WebView2 frontend
- Powerful C# backend with full access to .NET features
- Bidirectional communication between C# and JavaScript
- Dynamic menu system with nested items
- Modal windows with HTML content support
- Modern web technologies support
- No XAML required

## Getting Started

1. Create a new .NET Windows Forms application
2. Add a reference to the WebDesktop.Core project
3. Create a new window:

```csharp
using WebDesktop.Core;

var window = new WebDesktopForm("My App", 800, 600);
await window.InitializeAsync();
await window.NavigateToString("<html><body><h1>Hello World</h1></body></html>");
Application.Run(window);
```

## Window Features

### Dynamic Menus

```csharp
// Add top-level menu
window.AddMenu("File");

// Add menu item with handler
var fileMenu = (ToolStripMenuItem)window.MainMenuStrip.Items[0];
window.AddMenuItem(fileMenu, "Exit", (s, e) => Application.Exit());
```

### Modal Windows

```csharp
var modal = new ModalWindow("<html><body><h1>Content</h1></body></html>", "My Modal");
modal.Owner = parentWindow;
await modal.InitializeAsync();
modal.ShowDialog();
```

## JavaScript Bridge

Communicate between C# and JavaScript easily:

```csharp
var bridge = window.CreateJavaScriptBridge();
await bridge.InvokeJavaScriptMethod("updateUI", "Hello from C#");
```

## Requirements

- .NET 7.0 or later
- Microsoft Edge WebView2 Runtime

## License

MIT