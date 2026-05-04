namespace WebDesktop.Core
{
    public class WebDesktopException : InvalidOperationException
    {
        public WebDesktopException(string message) : base(message) { }
        public WebDesktopException(string message, Exception inner) : base(message, inner) { }
    }
}
