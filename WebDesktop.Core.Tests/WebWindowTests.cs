using Xunit;
using Moq;
using WebDesktop.Core;
using Microsoft.Web.WebView2.Core;
using System.Windows.Forms;

namespace WebDesktop.Core.Tests
{
    public class WebWindowTests
    {
        private readonly Mock<CoreWebView2> _webViewMock;
        private readonly WebWindow _window;

        public WebWindowTests()
        {
            _webViewMock = new Mock<CoreWebView2>();
            
            var webView2Control = new WebView2();
            webView2Control.CoreWebView2 = _webViewMock.Object;
            
            _window = new WebWindow();
            _window.Controls.Add(webView2Control);
        }

        [Fact]
        public void InitializeAsync_CreatesSharedEnvironment()
        {
            // Act
            _window.InitializeAsync().Wait();

            // Assert
            Assert.NotNull(WebWindow.SharedEnvironment);
        }

        [Fact]
        public void AddMenu_CreatesMenuStrip()
        {
            // Act
            _window.AddMenu("Test Menu");

            // Assert
            Assert.NotNull(_window.MainMenuStrip);
            Assert.Single(_window.MainMenuStrip.Items);
        }

        [Fact]
        public void NavigateToString_ExecutesOnWebView()
        {
            // Arrange
            const string testHtml = "<html></html>";

            // Act
            _window.NavigateToString(testHtml).Wait();

            // Assert
            _webViewMock.Verify(w => w.NavigateToString(testHtml), Times.Once);
        }

        [Fact]
        public void WebMessageReceived_EventTriggers()
        {
            // Arrange
            var eventTriggered = false;
            _window.WebMessageReceived += (s, e) => eventTriggered = true;

            // Act
            _webViewMock.Raise(w => w.WebMessageReceived += null, 
                new CoreWebView2WebMessageReceivedEventArgs(_webViewMock.Object, "{}"));

            // Assert
            Assert.True(eventTriggered);
        }
    }
}