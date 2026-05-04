using Microsoft.Web.WebView2.Core;

namespace WebDesktop.Core
{
    public class WebView2Configuration
    {
        public string? UserDataFolder { get; set; }
        public string? BrowserExecutableFolder { get; set; }
        public string? Language { get; set; }
        public string? AdditionalBrowserArguments { get; set; }
        public bool AllowDevTools { get; set; } = true;
        public bool AllowContextMenus { get; set; } = true;
        public bool IsScriptEnabled { get; set; } = true;

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
