using System.Text.Json;
using NUnit.Framework;
using Moq;
using WebDesktop.Core.Bridge;
using Microsoft.JSInterop;

namespace WebDesktop.Core.Tests
{
    [TestFixture]
    public class JavaScriptBridgeTests
{
    private string capturedIdentifier;
    private List<object> capturedArgs;
    private CancellationToken capturedCancellationToken;
    private string expectedScript;
    
    private JavaScriptBridge _bridge = null!;
        private Mock<IJSRuntime> _jsRuntimeMock = null!;

        [SetUp]
        public void SetUp()
        {
            capturedIdentifier = null;
            capturedArgs = new List<object>();
            capturedCancellationToken = CancellationToken.None;
            var jsExecutorMock = new Mock<IJSExecutor>();
            jsExecutorMock.Setup(x => x.ExecuteScriptAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .ReturnsAsync((object)null);
            _bridge = new JavaScriptBridge(jsExecutorMock.Object, _jsRuntimeMock.Object);
        }

        [Test]
        public async Task RegisterCallback_AddsToDictionary()
        {
            // Arrange
            const string functionName = "testFunction";
            
            // Act
            await _bridge.RegisterCallback(functionName, (json) => Task.CompletedTask);

            // Assert
            Assert.That(_bridge.Callbacks.Keys, Does.Contain(functionName));
        }

        [Test]
        public async Task CallJSFunction_InvokesJSRuntime()
        {
            // Arrange
            const string functionName = "alert";
            var args = new object[] { "Test message" };
            // Configurar el mock para capturar cualquier script
            var capturedScript = "";
            _jsRuntimeMock.Setup(x => x.InvokeAsync<ValueTask>(
                    It.IsAny<string>(),
                    It.IsAny<object[]>())
                )
                .Callback<string, object[]>((identifier, args) => {
                    capturedScript = identifier;
                })
                .ReturnsAsync(new ValueTask());
            
            _jsRuntimeMock.Setup(x => x.InvokeAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
                .Callback<string, CancellationToken, object[]>((identifier, token, arguments) => {
                    capturedIdentifier = identifier;
                    capturedArgs.Add(arguments);
                    capturedCancellationToken = token;
                })
                .ReturnsAsync((object)null);

            expectedScript = $"window.{functionName}.apply(null, {JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false })});";
            // Act
            await _bridge.InvokeJavaScriptMethod(functionName, args);

            // Assert
            // Verificar que se ejecutó el script esperado
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>(
                "eval",
                It.Is<CancellationToken>(ct => ct == CancellationToken.None), It.Is<object[]>(args => args[0].ToString() == expectedScript)
            ), Times.Once);

        }

        [Test]
        public async Task EventHandler_TriggersRegisteredFunction()
        {
            // Arrange
            await _bridge.RegisterCallback("testEvent", (json) => Task.CompletedTask);

            // Act
            await _bridge.HandleEvent("testElement", "click", "testHandler");

            // Assert
            var expectedScript = $"document.getElementById('testElement').addEventListener('click', (e) => {{ window.testHandler(e); }});";
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>(
                "eval",
                It.Is<CancellationToken>(ct => ct == CancellationToken.None), It.Is<object[]>(args => args[0].ToString() == expectedScript)
            ), Times.Once);
        }

        [Test]
        public async Task RegisterCallback_AddsToHandlers()
        {
            // Act
            await _bridge.RegisterCallback("testFunction", (json) => Task.CompletedTask);

            // Assert
            Assert.That(_bridge.Callbacks.Keys, Does.Contain("testFunction"));
        }

        [Test]
        public async Task HandleEvent_ExecutesCorrectScript()
        {
            // Arrange
            await _bridge.RegisterCallback("testHandler", (json) => Task.CompletedTask);

            // Act
            await _bridge.HandleEvent("testElement", "click", "testHandler");

            // Assert
            var expectedScript = $"document.getElementById('testElement').addEventListener('click', (e) => {{ window.testHandler(e); }});";
            _jsRuntimeMock.Verify(x => x.InvokeAsync<object>(
                "eval",
                It.Is<CancellationToken>(ct => ct == CancellationToken.None), It.Is<object[]>(args => args[0].ToString() == expectedScript)
            ), Times.Once);
        }
    }
}