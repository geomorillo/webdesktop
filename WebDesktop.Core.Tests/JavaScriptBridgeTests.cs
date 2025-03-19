using Xunit;
using Moq;
using WebDesktop.Core.Bridge;
using Microsoft.JSInterop;
using System.Collections.Generic;

namespace WebDesktop.Core.Tests
{
    public class JavaScriptBridgeTests
    {
        private readonly JavaScriptBridge _bridge;
        private readonly Mock<IJSRuntime> _jsRuntimeMock;

        public JavaScriptBridgeTests()
        {
            _jsRuntimeMock = new Mock<IJSRuntime>();
            _bridge = new JavaScriptBridge(_jsRuntimeMock.Object);
        }

        [Fact]
        public void RegisterFunction_AddsToDictionary()
        {
            // Arrange
            const string functionName = "testFunction";
            
            // Act
            _bridge.RegisterFunction(functionName, () => { });

            // Assert
            Assert.Contains(functionName, _bridge.GetRegisteredFunctions());
        }

        [Fact]
        public async Task CallJSFunction_InvokesJSRuntime()
        {
            // Arrange
            const string functionName = "alert";
            var args = new object[] { "Test message" };

            // Act
            await _bridge.CallJSFunction(functionName, args);

            // Assert
            _jsRuntimeMock.Verify(x => 
                x.InvokeAsync<IJSVoid>("invokeDotNetFunction", functionName, args), 
                Times.Once);
        }

        [Fact]
        public void EventHandler_TriggersRegisteredFunction()
        {
            // Arrange
            var eventTriggered = false;
            _bridge.RegisterFunction("testEvent", () => eventTriggered = true);

            // Act
            _bridge.HandleEvent("testEvent", "{}");

            // Assert
            Assert.True(eventTriggered);
        }
    }
}