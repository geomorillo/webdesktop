// Calculadora de ejemplo: la UI web arma la expresión y delega el cálculo
// al motor C# registrado como handler "calc.evaluate" en el puente WebDesktop.
var WD = window.WebDesktop;

// Asegura que la página reciba las teclas: el body debe ser enfocable.
document.body.setAttribute('tabindex', '-1');
window.focus();
document.body.focus();

var display = document.getElementById('display');
var expression = '';
var justEvaluated = false;

function render() {
  var txt = expression.split('*').join('\u00d7').split('/').join('\u00f7');
  display.textContent = txt === '' ? '0' : txt;
  display.classList.remove('error');
}

function isOperator(ch) { return ch !== undefined && '+-*/'.indexOf(ch) >= 0; }
function isDigit(ch) { return ch !== undefined && ch >= '0' && ch <= '9'; }

function inputDigit(d) {
  if (justEvaluated) { expression = ''; justEvaluated = false; }
  if (expression.length >= 120) return;
  var last = expression[expression.length - 1];
  if (last === ')') expression += '*'; // multiplicación implícita: (2+3)4
  expression += d;
  render();
}

function inputDot() {
  if (justEvaluated) { expression = ''; justEvaluated = false; }
  if (expression.length >= 120) return;
  var last = expression[expression.length - 1];
  if (last === ')') return; // punto tras un paréntesis no tiene sentido
  if (expression === '') expression = '0';
  if (isOperator(last) || last === '(') expression += '0';

  // buscar el último número y comprobar si ya tiene punto
  var i = expression.length - 1;
  while (i >= 0 && (isDigit(expression[i]) || expression[i] === '.')) i--;
  var numero = expression.slice(i + 1);
  if (numero.indexOf('.') >= 0) return;

  expression += '.';
  render();
}

function inputOperator(op) {
  if (justEvaluated) justEvaluated = false;
  if (expression === '') expression = '0';
  if (expression.length >= 120) return;
  var last = expression[expression.length - 1];

  if (last === '(') {
    if (op !== '-') return; // tras "(" solo se admite el menos unario
    expression += op;
    render();
    return;
  }

  if (isOperator(last)) expression = expression.slice(0, -1);
  expression += op;
  render();
}

function inputParen(p) {
  if (justEvaluated) { expression = ''; justEvaluated = false; }
  if (expression.length >= 120) return;
  var last = expression[expression.length - 1];

  if (p === '(') {
    if (isDigit(last) || last === '.' || last === ')') expression += '*'; // implícito
    expression += '(';
  } else {
    var abiertos = 0, cerrados = 0, i;
    for (i = 0; i < expression.length; i++) {
      if (expression[i] === '(') abiertos++;
      else if (expression[i] === ')') cerrados++;
    }
    if (abiertos <= cerrados) return;
    if (last === '(' || isOperator(last)) return;
    expression += ')';
  }
  render();
}

function toggleSign() {
  if (justEvaluated) {
    if (expression !== '' && expression !== '0') {
      expression = expression.charAt(0) === '-' ? expression.slice(1) : '-' + expression;
      justEvaluated = false;
    }
    render();
    return;
  }

  if (expression === '') { expression = '-0'; render(); return; }

  // localizar el último número de la expresión
  var i = expression.length - 1;
  while (i >= 0 && (isDigit(expression[i]) || expression[i] === '.')) i--;
  var inicio = i + 1;
  var numero = expression.slice(inicio);
  if (numero === '') return;

  var antes = expression.slice(0, inicio);
  var ultimoAntes = antes[antes.length - 1];
  var numeroNegado = '-' + numero;

  if (antes === '') {
    expression = numeroNegado;
  } else if (isOperator(ultimoAntes) || ultimoAntes === '(') {
    // tras un operador se envuelve en paréntesis: 5-3 -> 5-(-3)
    expression = antes + '(' + numeroNegado + ')';
  } else {
    expression = antes + numeroNegado;
  }
  render();
}

function backspace() {
  if (justEvaluated) { clearAll(); return; }
  expression = expression.slice(0, -1);
  render();
}

function clearAll() {
  expression = '';
  justEvaluated = false;
  render();
}

async function evaluar() {
  var expr = expression;
  if (justEvaluated) {
    // Tras un resultado, "=" re-evalúa el propio resultado (idempotente):
    justEvaluated = false;
  }
  if (isOperator(expr[expr.length - 1])) expr = expr.slice(0, -1); // operador colgante
  if (expr === '') return;

  try {
    var raw = await WD.invoke('calc.evaluate', { expression: expr });
    var res = JSON.parse(raw);

    if (res.success) {
      expression = res.result;
      justEvaluated = true;
      render();
    } else {
      showError(res.error);
    }
  } catch (err) {
    showError('Error de comunicación con C#');
  }
}

function showError(msg) {
  display.textContent = msg;
  display.classList.add('error');
  expression = '';
  justEvaluated = false;
}

// Soporte de teclado
document.addEventListener('keydown', function (e) {
  var k = e.key;

  if (k >= '0' && k <= '9') { inputDigit(k); return; }
  if (k === '.' || k === ',') { inputDot(); return; }
  if (k === '+' || k === '-') { inputOperator(k); return; }
  if (k === '*' || k === 'x' || k === 'X' || k === '\u00d7') { inputOperator('*'); return; }
  if (k === '/' || k === '\u00f7') { inputOperator('/'); return; }
  if (k === '(' || k === ')') { inputParen(k); return; }
  if (k === 'Enter' || k === '=') { e.preventDefault(); evaluar(); return; }
  if (k === 'Backspace') { e.preventDefault(); backspace(); return; }
  if (k === 'Escape' || k === 'Delete' || k === 'c' || k === 'C') { clearAll(); return; }
});
