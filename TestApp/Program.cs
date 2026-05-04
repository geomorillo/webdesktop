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

        var window = new WebWindow("WebDesktop MVP", 1024, 768);

        window.Shown += async (sender, e) =>
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

            window.Externo.RegisterHandler("hora", (_) =>
            {
                return Task.FromResult(
                    JsonSerializer.Serialize(new
                    {
                        hora = DateTime.Now.ToString("HH:mm:ss"),
                        fecha = DateTime.Now.ToString("dd/MM/yyyy")
                    }));
            });

            await window.NavigateToString(@"
<h1>WebDesktop MVP</h1>
<p>Framework hibrido C# + WebView2</p>

<div style='margin:20px 0'>
    <input type='text' id='nombreInput' placeholder='Tu nombre' />
    <button id='saludarBtn'>Saludar</button>
    <p id='saludarOutput'></p>
</div>

<div style='margin:20px 0'>
    <button id='horaBtn'>Obtener hora desde C#</button>
    <pre id='horaOutput'></pre>
</div>

<script>
    document.getElementById('saludarBtn').onclick = async () => {
        const nombre = document.getElementById('nombreInput').value;
        const result = await window.WebDesktop.invoke('saludar', { nombre });
        document.getElementById('saludarOutput').textContent = JSON.parse(result).mensaje;
    };

    document.getElementById('horaBtn').onclick = async () => {
        const result = await window.WebDesktop.invoke('hora');
        const data = JSON.parse(result);
        document.getElementById('horaOutput').textContent = `Hora: ${data.hora} | Fecha: ${data.fecha}`;
    };
</script>
");
        };

        Application.Run(window);
    }
}
