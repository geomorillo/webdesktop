# WebDesktop Framework

A lightweight .NET framework for building desktop applications using C# and web technologies (HTML, CSS, and JavaScript) without XAML. This framework provides a seamless bridge between C# backend and web frontend using Microsoft's WebView2 control.

## Features

- Create desktop applications using HTML, CSS, and JavaScript
- Powerful C# backend with full access to .NET features
- Bidirectional communication between C# and JavaScript
- Modern web technologies support
- No XAML required

## Getting Started

1. Create a new .NET Windows Forms application
2. Add a reference to the WebDesktop.Core project
3. Create a new window:

```csharp
using WebDesktop.Core;

var window = new WebWindow("My App", 800, 600);
await window.InitializeAsync();
await window.NavigateToString("<html><body><h1>Hello World</h1></body></html>");
Application.Run(window);
```

## JavaScript Bridge

Communicate between C# and JavaScript easily:

```csharp
var bridge = new JavaScriptBridge(window);
await bridge.InvokeJavaScriptMethod("updateUI", "Hello from C#");
```

## Requirements

- .NET 7.0 or later
- Microsoft Edge WebView2 Runtime

## License

MIT