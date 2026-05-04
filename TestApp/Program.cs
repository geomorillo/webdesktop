using WebDesktop.Core;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TestApp;

public record TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public bool IsCompleted { get; set; }
    public string CreatedAt { get; set; } = DateTime.Now.ToString("o");
}

public static class Program
{
    private const string ConnString = "Data Source=tasks.db";

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        InitDatabase();

        var window = new WebWindow("Gestor de Tareas", 900, 650);

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            RegisterHandlers(window);

            window.SetAssetFolder("wwwroot");
            await window.NavigateToAsset("index.html");
        };

        Application.Run(window);
    }

    private static void InitDatabase()
    {
        using var db = new SqliteConnection(ConnString);
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                description TEXT DEFAULT '',
                priority TEXT DEFAULT 'Medium',
                is_completed INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now'))
            )
            """;
        cmd.ExecuteNonQuery();

        var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM tasks";
        if ((long)count.ExecuteScalar()! == 0)
        {
            var seed = db.CreateCommand();
            seed.CommandText = """
                INSERT INTO tasks (title, description, priority, is_completed, created_at) VALUES
                ('Configurar WebView2 Runtime', 'Instalar y verificar el runtime de WebView2 en el equipo', 'High', 1, '2026-05-01T10:00:00'),
                ('Implementar bridge C#/JS', 'Probar comunicacion bidireccional con postMessage', 'High', 0, '2026-05-02T14:30:00'),
                ('Disenar UI del dashboard', 'Crear la interfaz principal con HTML y CSS', 'Medium', 0, '2026-05-03T09:00:00'),
                ('Escribir tests unitarios', 'Cubrir casos de uso del ExternalInvoker', 'Low', 0, '2026-05-03T12:00:00')
                """;
            seed.ExecuteNonQuery();
        }
    }

    private static TaskItem ReadTask(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        Title = r.GetString(1),
        Description = r.IsDBNull(2) ? "" : r.GetString(2),
        Priority = r.GetString(3),
        IsCompleted = r.GetInt32(4) == 1,
        CreatedAt = r.GetString(5)
    };

    private static void RegisterHandlers(WebWindow window)
    {
        var json = WebWindow.JsonOptions;

        window.Externo.RegisterHandler("tasks.list", (_) =>
        {
            using var db = new SqliteConnection(ConnString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT * FROM tasks ORDER BY created_at DESC";
            using var reader = cmd.ExecuteReader();
            var tasks = new List<TaskItem>();
            while (reader.Read()) tasks.Add(ReadTask(reader));
            return Task.FromResult(JsonSerializer.Serialize(tasks, json));
        });

        window.Externo.RegisterHandler("tasks.create", (jsonArgs) =>
        {
            var t = JsonSerializer.Deserialize<TaskItem>(jsonArgs, json)!;
            using var db = new SqliteConnection(ConnString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO tasks (title, description, priority) VALUES (@t, @d, @p); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@t", t.Title);
            cmd.Parameters.AddWithValue("@d", t.Description);
            cmd.Parameters.AddWithValue("@p", t.Priority);
            t.Id = Convert.ToInt32(cmd.ExecuteScalar());
            t.CreatedAt = DateTime.Now.ToString("o");
            return Task.FromResult(JsonSerializer.Serialize(t, json));
        });

        window.Externo.RegisterHandler("tasks.update", (jsonArgs) =>
        {
            var t = JsonSerializer.Deserialize<TaskItem>(jsonArgs, json)!;
            using var db = new SqliteConnection(ConnString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET title=@t, description=@d, priority=@p, is_completed=@c WHERE id=@id";
            cmd.Parameters.AddWithValue("@t", t.Title);
            cmd.Parameters.AddWithValue("@d", t.Description);
            cmd.Parameters.AddWithValue("@p", t.Priority);
            cmd.Parameters.AddWithValue("@c", t.IsCompleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", t.Id);
            cmd.ExecuteNonQuery();
            return Task.FromResult(JsonSerializer.Serialize(t, json));
        });

        window.Externo.RegisterHandler("tasks.delete", (jsonArgs) =>
        {
            var id = JsonSerializer.Deserialize<JsonElement>(jsonArgs).GetProperty("id").GetInt32();
            using var db = new SqliteConnection(ConnString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM tasks WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return Task.FromResult(JsonSerializer.Serialize(new { ok = true }, json));
        });

        window.Externo.RegisterHandler("tasks.toggle", (jsonArgs) =>
        {
            var id = JsonSerializer.Deserialize<JsonElement>(jsonArgs).GetProperty("id").GetInt32();
            using var db = new SqliteConnection(ConnString);
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE tasks SET is_completed = CASE WHEN is_completed THEN 0 ELSE 1 END WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return Task.FromResult(JsonSerializer.Serialize(new { ok = true }, json));
        });
    }
}
