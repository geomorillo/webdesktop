using System.Text.Json;
using NUnit.Framework;
using Moq;
using WebDesktop.Core.Bridge;

namespace WebDesktop.Core.Tests
{
    [TestFixture]
    public class JavaScriptBridgeTests
    {
        private JavaScriptBridge _bridge = null!;
        private Mock<IJSExecutor> _jsExecutorMock = null!;
        private List<string> _executedScripts = null!;

        [SetUp]
        public void SetUp()
        {
            _executedScripts = new List<string>();
            _jsExecutorMock = new Mock<IJSExecutor>();
            _jsExecutorMock
                .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>()))
                .Callback<string>(script => _executedScripts.Add(script))
                .Returns(Task.CompletedTask);

            _bridge = new JavaScriptBridge(_jsExecutorMock.Object);
        }

        [Test]
        public async Task RegisterCallback_AddsToDictionary()
        {
            const string functionName = "testFunction";

            await _bridge.RegisterCallback(functionName, (json) => Task.CompletedTask);

            Assert.That(_bridge.Callbacks.Keys, Does.Contain(functionName));
        }

        [Test]
        public async Task RegisterCallback_ExecutesScriptWithCorrectName()
        {
            const string functionName = "myHandler";

            await _bridge.RegisterCallback(functionName, (json) => Task.CompletedTask);

            Assert.That(_executedScripts[0], Does.Contain(functionName));
        }

        [Test]
        public async Task InvokeJavaScriptMethod_ExecutesExpectedScript()
        {
            const string functionName = "alert";
            var args = new object[] { "Test message" };
            var expectedScript = $"window.{functionName}.apply(null, {JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false })});";

            await _bridge.InvokeJavaScriptMethod(functionName, args);

            Assert.That(_executedScripts[0], Is.EqualTo(expectedScript));
        }

        [Test]
        public async Task HandleEvent_ExecutesCorrectScript()
        {
            await _bridge.RegisterCallback("testHandler", (json) => Task.CompletedTask);
            _executedScripts.Clear();

            await _bridge.HandleEvent("testElement", "click", "testHandler");

            var expectedScript = $"document.getElementById('testElement').addEventListener('click', (e) => {{ window.testHandler(e); }});";
            Assert.That(_executedScripts[0], Is.EqualTo(expectedScript));
        }

        [Test]
        public async Task SetProperty_ExecutesCorrectScript()
        {
            await _bridge.SetProperty("myApp.config.theme", "dark");

            Assert.That(_executedScripts[0], Is.EqualTo("window.myApp.config.theme = \"dark\";"));
        }
    }
}
