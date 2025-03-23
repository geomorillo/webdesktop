using System.Threading.Tasks;

namespace WebDesktop.Core.Bridge
{
    /// <summary>
    /// Interfaz que abstrae la ejecución de código JavaScript
    /// </summary>
    public interface IJSExecutor
    {
        /// <summary>
        /// Ejecuta código JavaScript de forma asíncrona
        /// </summary>
        /// <param name="script">El script JavaScript a ejecutar</param>
        /// <returns>Una tarea que representa la operación asíncrona</returns>
        Task ExecuteScriptAsync(string script);
    }
}