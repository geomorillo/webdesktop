using System.Text.Json;
using NUnit.Framework;

namespace WebDesktop.Core.Tests
{
    [TestFixture]
    public class ExternalInvokerTests
    {
        private WebWindow.ExternalInvoker _invoker = null!;

        [SetUp]
        public void SetUp()
        {
            _invoker = new WebWindow.ExternalInvoker();
        }

        [TearDown]
        public void TearDown()
        {
            _invoker.Dispose();
        }

        [Test]
        public async Task RegisterHandler_And_Invoke_ReturnsExpectedResult()
        {
            _invoker.RegisterHandler("saludar", (json) =>
                Task.FromResult(JsonSerializer.Serialize(new { mensaje = "Hola!" })));

            var result = await _invoker.InvokeDotNetMethodAsync("saludar", "{}");

            var doc = JsonDocument.Parse(result);
            var mensaje = doc.RootElement.GetProperty("mensaje").GetString();
            Assert.That(mensaje, Is.EqualTo("Hola!"));
        }

        [Test]
        public async Task Invoke_UnregisteredHandler_ReturnsError()
        {
            var result = await _invoker.InvokeDotNetMethodAsync("noExiste", "{}");

            var doc = JsonDocument.Parse(result);
            var error = doc.RootElement.GetProperty("error").GetString();
            Assert.That(error, Is.EqualTo("Handler no encontrado"));
        }

        [Test]
        public async Task RegisterHandler_OverwritesExisting()
        {
            _invoker.RegisterHandler("dup", (json) =>
                Task.FromResult(JsonSerializer.Serialize(new { valor = "original" })));
            _invoker.RegisterHandler("dup", (json) =>
                Task.FromResult(JsonSerializer.Serialize(new { valor = "nuevo" })));

            var result = await _invoker.InvokeDotNetMethodAsync("dup", "{}");

            var doc = JsonDocument.Parse(result);
            var valor = doc.RootElement.GetProperty("valor").GetString();
            Assert.That(valor, Is.EqualTo("nuevo"));
        }

        [Test]
        public async Task Invoke_HandlerPassesArgumentsCorrectly()
        {
            _invoker.RegisterHandler("eco", (json) =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(json);
                var texto = args.GetProperty("texto").GetString() ?? "";
                return Task.FromResult(JsonSerializer.Serialize(new { resultado = texto }));
            });

            var result = await _invoker.InvokeDotNetMethodAsync("eco", "{\"texto\":\"test123\"}");

            var doc = JsonDocument.Parse(result);
            var resultado = doc.RootElement.GetProperty("resultado").GetString();
            Assert.That(resultado, Is.EqualTo("test123"));
        }

        [Test]
        public void Invoke_HandlerThrows_PropagatesException()
        {
            _invoker.RegisterHandler("falla", (json) =>
                throw new InvalidOperationException("error interno"));

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _invoker.InvokeDotNetMethodAsync("falla", "{}"));

            Assert.That(ex!.Message, Is.EqualTo("error interno"));
        }

        [Test]
        public async Task Dispose_ClearsAllHandlers()
        {
            _invoker.RegisterHandler("algo", (json) =>
                Task.FromResult("{}"));

            _invoker.Dispose();

            var result = await _invoker.InvokeDotNetMethodAsync("algo", "{}");

            var doc = JsonDocument.Parse(result);
            var error = doc.RootElement.GetProperty("error").GetString();
            Assert.That(error, Is.EqualTo("Handler no encontrado"));
        }

        [Test]
        public async Task MultipleHandlers_WorkIndependently()
        {
            _invoker.RegisterHandler("a", (json) =>
                Task.FromResult(JsonSerializer.Serialize(new { letra = "A" })));
            _invoker.RegisterHandler("b", (json) =>
                Task.FromResult(JsonSerializer.Serialize(new { letra = "B" })));

            var resultA = await _invoker.InvokeDotNetMethodAsync("a", "{}");
            var resultB = await _invoker.InvokeDotNetMethodAsync("b", "{}");

            var docA = JsonDocument.Parse(resultA);
            var docB = JsonDocument.Parse(resultB);
            Assert.That(docA.RootElement.GetProperty("letra").GetString(), Is.EqualTo("A"));
            Assert.That(docB.RootElement.GetProperty("letra").GetString(), Is.EqualTo("B"));
        }
    }
}
