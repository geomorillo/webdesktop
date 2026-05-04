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

            await RenderUI(window);
        };

        Application.Run(window);
    }

    private static void SeedData()
    {
        _tasks.Add(new() { Id = _nextId++, Title = "Configurar WebView2 Runtime", Description = "Instalar y verificar el runtime de WebView2 en el equipo", Priority = "High", IsCompleted = true });
        _tasks.Add(new() { Id = _nextId++, Title = "Implementar bridge C#/JS", Description = "Probar comunicacion bidireccional con postMessage", Priority = "High" });
        _tasks.Add(new() { Id = _nextId++, Title = "Disenar UI del dashboard", Description = "Crear la interfaz principal con HTML y CSS", Priority = "Medium" });
        _tasks.Add(new() { Id = _nextId++, Title = "Escribir tests unitarios", Description = "Cubrir casos de uso del ExternalInvoker", Priority = "Low" });
    }

    private static async Task RenderUI(WebWindow window)
    {
        var html = """
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { font-family: 'Segoe UI', system-ui, sans-serif; background: #f0f2f5; color: #222; padding: 24px; }
                h1 { font-size: 24px; margin-bottom: 20px; color: #1a1a2e; }
                .toolbar { display: flex; gap: 12px; margin-bottom: 20px; align-items: center; flex-wrap: wrap; }
                .toolbar input { flex: 1; min-width: 200px; padding: 8px 12px; border: 1px solid #ccc; border-radius: 6px; font-size: 14px; }
                .toolbar button, .btn { background: #0078d4; color: #fff; border: none; padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; }
                .toolbar button:hover, .btn:hover { background: #106ebe; }
                .btn-sm { padding: 4px 10px; font-size: 12px; }
                .btn-danger { background: #d13438; }.btn-danger:hover { background: #a4262c; }
                .btn-success { background: #0b6a0b; }.btn-success:hover { background: #094509; }
                table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.1); }
                th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #eee; font-size: 14px; }
                th { background: #f8f9fa; font-weight: 600; color: #555; }
                tr:hover { background: #f5f7fa; }
                tr.completed td { text-decoration: line-through; color: #999; }
                .actions { display: flex; gap: 6px; }
                .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; }
                .badge.High { background: #fde7e9; color: #d13438; }
                .badge.Medium { background: #fff3d6; color: #d47a00; }
                .badge.Low { background: #e3f5e3; color: #0b6a0b; }
                .badge.done { background: #d4edda; color: #155724; }
                .modal-overlay { display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,.4); align-items: center; justify-content: center; z-index: 1000; }
                .modal-overlay.show { display: flex; }
                .modal { background: #fff; border-radius: 10px; padding: 24px; width: 480px; max-width: 90%; box-shadow: 0 8px 32px rgba(0,0,0,.2); }
                .modal h2 { margin-bottom: 16px; font-size: 18px; }
                .modal label { display: block; font-size: 13px; font-weight: 600; color: #555; margin: 10px 0 4px; }
                .modal input, .modal textarea, .modal select { width: 100%; padding: 8px 10px; border: 1px solid #ccc; border-radius: 6px; font-size: 14px; font-family: inherit; }
                .modal textarea { min-height: 60px; resize: vertical; }
                .modal-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
                .modal-actions button { padding: 8px 20px; border-radius: 6px; border: none; cursor: pointer; font-size: 14px; }
                .modal-actions .btn-cancel { background: #e0e0e0; color: #333; }.modal-actions .btn-cancel:hover { background: #ccc; }
                .empty { text-align: center; padding: 40px; color: #999; }
                .counter { font-size: 13px; color: #888; }
            </style>

            <h1>Gestor de Tareas</h1>

            <div class="toolbar">
                <input type="text" id="searchInput" placeholder="Buscar tareas..." oninput="renderList()" />
                <button onclick="openCreate()">+ Nueva tarea</button>
                <span class="counter" id="counter"></span>
            </div>

            <div id="taskList"></div>

            <div class="modal-overlay" id="modal">
                <div class="modal">
                    <h2 id="modalTitle">Nueva tarea</h2>
                    <label>Titulo</label>
                    <input type="text" id="taskTitle" />
                    <label>Descripcion</label>
                    <textarea id="taskDescription"></textarea>
                    <label>Prioridad</label>
                    <select id="taskPriority">
                        <option value="Low">Low</option>
                        <option value="Medium" selected>Medium</option>
                        <option value="High">High</option>
                    </select>
                    <div class="modal-actions">
                        <button class="btn-cancel" onclick="closeModal()">Cancelar</button>
                        <button class="btn" onclick="saveTask()">Guardar</button>
                    </div>
                </div>
            </div>

            <div class="modal-overlay" id="confirmModal">
                <div class="modal">
                    <h2>Confirmar</h2>
                    <p id="confirmText">Eliminar esta tarea?</p>
                    <div class="modal-actions">
                        <button class="btn-cancel" onclick="closeConfirm()">Cancelar</button>
                        <button class="btn btn-danger" id="confirmBtn">Eliminar</button>
                    </div>
                </div>
            </div>

            <script>
                var WD = window.WebDesktop;
                var editingId = null;
                var deletingId = null;

                async function loadTasks() {
                    return JSON.parse(await WD.invoke('tasks.list'));
                }

                async function renderList() {
                    var tasks = await loadTasks();
                    var q = (document.getElementById('searchInput').value || '').toLowerCase();
                    if (q) tasks = tasks.filter(function(t) { return t.title.toLowerCase().indexOf(q) >= 0 || (t.description || '').toLowerCase().indexOf(q) >= 0; });

                    var done = 0;
                    for (var i = 0; i < tasks.length; i++) { if (tasks[i].isCompleted) done++; }
                    document.getElementById('counter').textContent = tasks.length + ' tareas (' + done + ' completadas)';

                    if (tasks.length === 0) {
                        document.getElementById('taskList').innerHTML = '<div class="empty">No hay tareas</div>';
                        return;
                    }

                    var html = '<table><thead><tr><th></th><th>Titulo</th><th>Prioridad</th><th>Fecha</th><th></th></tr></thead><tbody>';
                    for (var i = 0; i < tasks.length; i++) {
                        var t = tasks[i];
                        var cls = t.isCompleted ? 'completed' : '';
                        var doneHtml = t.isCompleted
                            ? '<span class="badge done">Hecho</span>'
                            : '<button class="btn btn-success btn-sm" onclick="toggleTask(' + t.id + ')">OK</button>';
                        html += '<tr class="' + cls + '"><td>' + doneHtml + '</td>'
                            + '<td><strong>' + esc(t.title) + '</strong><br><small>' + esc(t.description || '') + '</small></td>'
                            + '<td><span class="badge ' + t.priority + '">' + t.priority + '</span></td>'
                            + '<td>' + new Date(t.createdAt).toLocaleDateString() + '</td>'
                            + '<td class="actions">'
                            + '<button class="btn btn-sm" onclick="openEdit(' + t.id + ')">Editar</button>'
                            + '<button class="btn btn-danger btn-sm" onclick="confirmDelete(' + t.id + ')">Eliminar</button>'
                            + '</td></tr>';
                    }
                    html += '</tbody></table>';
                    document.getElementById('taskList').innerHTML = html;
                }

                function esc(s) {
                    if (!s) return '';
                    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
                }

                function openCreate() {
                    editingId = null;
                    document.getElementById('modalTitle').textContent = 'Nueva tarea';
                    document.getElementById('taskTitle').value = '';
                    document.getElementById('taskDescription').value = '';
                    document.getElementById('taskPriority').value = 'Medium';
                    document.getElementById('modal').classList.add('show');
                    document.getElementById('taskTitle').focus();
                }

                async function openEdit(id) {
                    var tasks = await loadTasks();
                    var t = null;
                    for (var i = 0; i < tasks.length; i++) { if (tasks[i].id === id) { t = tasks[i]; break; } }
                    if (!t) return;
                    editingId = id;
                    document.getElementById('modalTitle').textContent = 'Editar tarea';
                    document.getElementById('taskTitle').value = t.title;
                    document.getElementById('taskDescription').value = t.description;
                    document.getElementById('taskPriority').value = t.priority;
                    document.getElementById('modal').classList.add('show');
                    document.getElementById('taskTitle').focus();
                }

                function closeModal() {
                    document.getElementById('modal').classList.remove('show');
                    editingId = null;
                }

                async function saveTask() {
                    var title = document.getElementById('taskTitle').value.trim();
                    if (!title) { return; }
                    if (editingId) {
                        var tasks = await loadTasks();
                        var t = null;
                        for (var i = 0; i < tasks.length; i++) { if (tasks[i].id === editingId) { t = tasks[i]; break; } }
                        if (t) {
                            t.title = title;
                            t.description = document.getElementById('taskDescription').value.trim();
                            t.priority = document.getElementById('taskPriority').value;
                            await WD.invoke('tasks.update', t);
                        }
                    } else {
                        await WD.invoke('tasks.create', {
                            title: title,
                            description: document.getElementById('taskDescription').value.trim(),
                            priority: document.getElementById('taskPriority').value
                        });
                    }
                    closeModal();
                    await renderList();
                }

                async function toggleTask(id) {
                    await WD.invoke('tasks.toggle', { id: id });
                    await renderList();
                }

                function confirmDelete(id) {
                    deletingId = id;
                    document.getElementById('confirmText').textContent = 'Eliminar esta tarea permanentemente?';
                    document.getElementById('confirmModal').classList.add('show');
                }

                function closeConfirm() {
                    document.getElementById('confirmModal').classList.remove('show');
                    deletingId = null;
                }

                async function deleteTask() {
                    if (deletingId === null) return;
                    await WD.invoke('tasks.delete', { id: deletingId });
                    closeConfirm();
                    await renderList();
                }

                document.getElementById('confirmBtn').onclick = deleteTask;
                renderList();
            </script>
            """;
        await window.NavigateToString(html);
    }
}
