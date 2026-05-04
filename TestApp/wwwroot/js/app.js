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
