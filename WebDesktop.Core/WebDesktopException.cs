namespace WebDesktop.Core
{
    /// <summary>
    /// Excepción base del framework WebDesktop. Se usa para todos los errores controlados
    /// durante la inicialización y operación de WebView2 y componentes relacionados.
    /// </summary>
    public class WebDesktopException : InvalidOperationException
    {
        /// <summary>
        /// Crea una nueva instancia con un mensaje de error.
        /// </summary>
        /// <param name="message">Descripción del error.</param>
        public WebDesktopException(string message) : base(message) { }

        /// <summary>
        /// Crea una nueva instancia con un mensaje de error y una excepción interna.
        /// </summary>
        /// <param name="message">Descripción del error.</param>
        /// <param name="inner">Excepción interna que originó el error.</param>
        public WebDesktopException(string message, Exception inner) : base(message, inner) { }
    }
}
