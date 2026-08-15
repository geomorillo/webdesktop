using System.Globalization;

namespace CalculatorApp;

/// <summary>
/// Motor de cálculo de la calculadora de ejemplo.
/// Evalúa expresiones aritméticas con +, -, *, /, % y paréntesis,
/// usando un analizador descendente recursivo (recursive descent).
/// </summary>
public static class Calculadora
{
    /// <summary>
    /// Evalúa una expresión aritmética y devuelve su resultado numérico.
    /// </summary>
    /// <param name="expresion">Expresión a evaluar (ej: "12+3*4", "(5+3)/2").</param>
    /// <returns>Resultado de la expresión.</returns>
    /// <exception cref="ArgumentException">Si la expresión está vacía o tiene símbolos no válidos.</exception>
    /// <exception cref="DivideByZeroException">Si la expresión divide entre cero.</exception>
    public static double Evaluar(string expresion)
    {
        if (string.IsNullOrWhiteSpace(expresion))
            throw new ArgumentException("La expresión está vacía");

        var parser = new Parser(expresion);
        var resultado = parser.ParseExpresion();

        if (parser.Actual != null)
            throw new ArgumentException($"Símbolo inesperado: {parser.Actual}");

        return resultado;
    }

    /// <summary>
    /// Formatea un resultado para mostrarlo en la UI: sin ceros decimales
    /// innecesarios y con redondeo a 12 decimales para evitar ruido de punto flotante.
    /// </summary>
    /// <param name="valor">Resultado numérico.</param>
    /// <returns>Representación de texto del resultado.</returns>
    public static string FormatearResultado(double valor)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor))
            throw new InvalidOperationException("Resultado no válido");

        var redondeado = Math.Round(valor, 12);

        if (redondeado == Math.Truncate(redondeado) && Math.Abs(redondeado) < 1e15)
            return ((long)redondeado).ToString(CultureInfo.InvariantCulture);

        return redondeado.ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Analizador descendente recursivo. Gramática:
    /// expresion := termino (('+' | '-') termino)*
    /// termino   := factor (('*' | '/' | '%') factor)*
    /// factor    := ('-' | '+') factor | '(' expresion ')' | número
    /// </summary>
    private sealed class Parser
    {
        private readonly string _texto;
        private int _posicion;
        private string? _actual;

        public Parser(string texto)
        {
            _texto = texto;
            _actual = null;
            Avanzar();
        }

        /// <summary>Token actual, o null si la expresión terminó.</summary>
        public string? Actual => _actual;

        private void Avanzar()
        {
            while (_posicion < _texto.Length && char.IsWhiteSpace(_texto[_posicion]))
                _posicion++;

            if (_posicion >= _texto.Length)
            {
                _actual = null;
                return;
            }

            var c = _texto[_posicion];

            if (char.IsDigit(c) || c == '.')
            {
                var inicio = _posicion;
                while (_posicion < _texto.Length &&
                       (char.IsDigit(_texto[_posicion]) || _texto[_posicion] == '.'))
                    _posicion++;
                _actual = _texto[inicio.._posicion];
            }
            else
            {
                _actual = c.ToString();
                _posicion++;
            }
        }

        public double ParseExpresion()
        {
            var valor = ParseTermino();

            while (_actual == "+" || _actual == "-")
            {
                var operador = _actual;
                Avanzar();
                var derecho = ParseTermino();
                valor = operador == "+" ? valor + derecho : valor - derecho;
            }

            return valor;
        }

        private double ParseTermino()
        {
            var valor = ParseFactor();

            while (_actual == "*" || _actual == "/" || _actual == "%")
            {
                var operador = _actual;
                Avanzar();
                var derecho = ParseFactor();

                if ((operador == "/" || operador == "%") && derecho == 0)
                    throw new DivideByZeroException("División por cero");

                valor = operador switch
                {
                    "*" => valor * derecho,
                    "/" => valor / derecho,
                    _ => valor % derecho
                };
            }

            return valor;
        }

        private double ParseFactor()
        {
            if (_actual == "-" || _actual == "+")
            {
                var signo = _actual;
                Avanzar();
                var valor = ParseFactor();
                return signo == "-" ? -valor : valor;
            }

            if (_actual == "(")
            {
                Avanzar();
                var valor = ParseExpresion();
                if (_actual != ")")
                    throw new ArgumentException("Falta el paréntesis de cierre");
                Avanzar();
                return valor;
            }

            if (_actual == null ||
                !double.TryParse(_actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero))
            {
                throw new ArgumentException($"Número esperado, se encontró: {_actual ?? "fin de expresión"}");
            }

            Avanzar();
            return numero;
        }
    }
}
