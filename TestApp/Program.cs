using WebDesktop.Core;
using System.Text.Json;

namespace TestApp;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var window = new WebWindow("WebDesktop App", 1024, 768);

        // Optional: minimize to tray
        // window.EnableTrayIcon("WebDesktop App");

        window.OnBridgeReady += (_, _) => Console.WriteLine("Bridge listo");

        window.Shown += async (_, _) =>
        {
            await window.InitializeAsync();

            window.AddMenu("File");
            var fileMenu = (ToolStripMenuItem)window.MainMenuStrip!.Items[0]!;
            window.AddMenuItem(fileMenu, "Exit", (_, _) => Application.Exit());

            window.Externo.RegisterHandler("saludar", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var nombre = args.GetProperty("nombre").GetString() ?? "Mundo";
                return Task.FromResult(
                    JsonSerializer.Serialize(new { mensaje = $"Hola {nombre}! desde C#" }));
            });

            await window.NavigateToString(@"
<h1>WebDesktop App</h1>
<p>Framework híbrido C# + WebView2</p>

<h3>Bridge C#/JS</h3>
<div>
    <input type='text' id='nombreInput' placeholder='Tu nombre' />
    <button id='saludarBtn'>Saludar</button>
    <p id='saludarOutput'></p>
</div>

<h3>Diálogos nativos (desde JS)</h3>
<div>
    <button id='msgBtn'>MessageBox</button>
    <button id='openBtn'>Abrir archivo</button>
    <button id='saveBtn'>Guardar archivo</button>
    <button id='folderBtn'>Seleccionar carpeta</button>
    <pre id='dialogOutput'></pre>
</div>

<script>
    const WD = window.WebDesktop;

    document.getElementById('saludarBtn').onclick = async () => {
        const nombre = document.getElementById('nombreInput').value;
        const result = JSON.parse(await WD.invoke('saludar', { nombre }));
        document.getElementById('saludarOutput').textContent = result.mensaje;
    };

    document.getElementById('msgBtn').onclick = async () => {
        const r = JSON.parse(await WD.invoke('__dialog.showMessage', {
            text: 'Hola desde JavaScript!',
            caption: 'Mensaje',
            buttons: 'OKCancel',
            icon: 'Info'
        }));
        document.getElementById('dialogOutput').textContent = 'Resultado: ' + r.result;
    };

    document.getElementById('openBtn').onclick = async () => {
        const r = JSON.parse(await WD.invoke('__dialog.openFile', {
            filter: 'Text files (*.txt)|*.txt|All files (*.*)|*.*',
            multi: false
        }));
        document.getElementById('dialogOutput').textContent = r.ok ? 'Archivo: ' + r.file : 'Cancelado';
    };

    document.getElementById('saveBtn').onclick = async () => {
        const r = JSON.parse(await WD.invoke('__dialog.saveFile', {
            filter: 'Text files (*.txt)|*.txt',
            defaultName: 'documento.txt'
        }));
        document.getElementById('dialogOutput').textContent = r.ok ? 'Guardar en: ' + r.file : 'Cancelado';
    };

    document.getElementById('folderBtn').onclick = async () => {
        const r = JSON.parse(await WD.invoke('__dialog.selectFolder', {}));
        document.getElementById('dialogOutput').textContent = r.ok ? 'Carpeta: ' + r.path : 'Cancelado';
    };
</script>
");
        };

        Application.Run(window);
    }
}
