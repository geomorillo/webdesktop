using WebDesktop.Core;
using System.Text.Json;

namespace TestApp;

public record TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class Program
{
    private static readonly List<TaskItem> _tasks = new();
    private static int _nextId = 1;

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("Gestor de Tareas", 900, 650);

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            SeedData();
            RegisterHandlers(window);

            window.SetAssetFolder("wwwroot");
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }

    private static void RegisterHandlers(WebWindow window)
    {
        var json = WebWindow.JsonOptions;

        window.Externo.RegisterHandler("tasks.list", (_) =>
            Task.FromResult(JsonSerializer.Serialize(_tasks.OrderByDescending(t => t.CreatedAt), json)));

        window.Externo.RegisterHandler("tasks.create", (jsonArgs) =>
        {
            var task = JsonSerializer.Deserialize<TaskItem>(jsonArgs, json)!;
            task.Id = _nextId++;
            task.CreatedAt = DateTime.Now;
            _tasks.Add(task);
            return Task.FromResult(JsonSerializer.Serialize(task, json));
        });

        window.Externo.RegisterHandler("tasks.update", (jsonArgs) =>
        {
            var updated = JsonSerializer.Deserialize<TaskItem>(jsonArgs, json)!;
            var existing = _tasks.FirstOrDefault(t => t.Id == updated.Id);
            if (existing == null)
                return Task.FromResult(JsonSerializer.Serialize(new { error = "Not found" }, json));
            existing.Title = updated.Title;
            existing.Description = updated.Description;
            existing.Priority = updated.Priority;
            existing.IsCompleted = updated.IsCompleted;
            return Task.FromResult(JsonSerializer.Serialize(existing, json));
        });

        window.Externo.RegisterHandler("tasks.delete", (jsonArgs) =>
        {
            var id = JsonSerializer.Deserialize<JsonElement>(jsonArgs).GetProperty("id").GetInt32();
            _tasks.RemoveAll(t => t.Id == id);
            return Task.FromResult(JsonSerializer.Serialize(new { ok = true }, json));
        });

        window.Externo.RegisterHandler("tasks.toggle", (jsonArgs) =>
        {
            var id = JsonSerializer.Deserialize<JsonElement>(jsonArgs).GetProperty("id").GetInt32();
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task != null) task.IsCompleted = !task.IsCompleted;
            return Task.FromResult(JsonSerializer.Serialize(task ?? new(), json));
        });
    }

    private static void SeedData()
    {
        _tasks.Add(new() { Id = _nextId++, Title = "Configurar WebView2 Runtime", Description = "Instalar y verificar el runtime de WebView2 en el equipo", Priority = "High", IsCompleted = true });
        _tasks.Add(new() { Id = _nextId++, Title = "Implementar bridge C#/JS", Description = "Probar comunicacion bidireccional con postMessage", Priority = "High" });
        _tasks.Add(new() { Id = _nextId++, Title = "Disenar UI del dashboard", Description = "Crear la interfaz principal con HTML y CSS", Priority = "Medium" });
        _tasks.Add(new() { Id = _nextId++, Title = "Escribir tests unitarios", Description = "Cubrir casos de uso del ExternalInvoker", Priority = "Low" });
    }
}
