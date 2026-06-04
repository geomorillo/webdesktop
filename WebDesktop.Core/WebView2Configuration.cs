using Microsoft.Web.WebView2.Core;

namespace WebDesktop.Core
{
    /// <summary>
    /// Configuración del entorno WebView2. Controla carpetas de datos, opciones del navegador,
    /// y comportamiento del runtime antes de la inicialización del control.
    /// </summary>
    public class WebView2Configuration
    {
        /// <summary>
        /// Carpeta donde WebView2 almacena los datos de usuario (cookies, caché, etc.).
        /// Si es null, se usa la ubicación predeterminada del runtime.
        /// </summary>
        public string? UserDataFolder { get; set; }

        /// <summary>
        /// Ruta a la carpeta que contiene el binario del runtime WebView2.
        /// Si es null, se usa el runtime instalado en el sistema.
        /// </summary>
        public string? BrowserExecutableFolder { get; set; }

        /// <summary>
        /// Código de idioma para la interfaz del navegador (ej: "es", "en-US").
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Argumentos adicionales de línea de comandos para el proceso del navegador.
        /// </summary>
        public string? AdditionalBrowserArguments { get; set; }

        /// <summary>
        /// Habilita o deshabilita las herramientas de desarrollo (F12).
        /// Valor predeterminado: true.
        /// </summary>
        public bool AllowDevTools { get; set; } = true;

        /// <summary>
        /// Habilita o deshabilita los menús contextuales del navegador.
        /// Valor predeterminado: true.
        /// </summary>
        public bool AllowContextMenus { get; set; } = true;

        /// <summary>
        /// Habilita o deshabilita la ejecución de JavaScript.
        /// Valor predeterminado: true.
        /// </summary>
        public bool IsScriptEnabled { get; set; } = true;

        /// <summary>
        /// Convierte esta configuración a un objeto <see cref="CoreWebView2EnvironmentOptions"/>
        /// para usar en <c>CoreWebView2Environment.CreateAsync</c>.
        /// </summary>
        /// <returns>Opciones de entorno listas para usar.</returns>
        public CoreWebView2EnvironmentOptions ToEnvironmentOptions()
        {
            var options = new CoreWebView2EnvironmentOptions();
            if (!string.IsNullOrEmpty(Language))
                options.Language = Language;
            if (!string.IsNullOrEmpty(AdditionalBrowserArguments))
                options.AdditionalBrowserArguments = AdditionalBrowserArguments;
            return options;
        }
    }
}
