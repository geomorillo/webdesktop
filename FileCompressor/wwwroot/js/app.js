async function invoke(method, args) {
  var raw = await window.WebDesktop.invoke(method, args);
  return JSON.parse(raw);
}

async function browseDialog(title, filter, multiSelect) {
  var r = await invoke('__dialog.openFile', {
    title: title || 'Seleccionar archivo',
    filter: filter || 'Todos (*.*)|*.*',
    multiSelect: multiSelect || false
  });
  return r;
}

async function saveDialog(title, filter, defaultName) {
  var r = await invoke('__dialog.saveFile', {
    title: title || 'Guardar como',
    filter: filter || 'ZIP (*.zip)|*.zip',
    defaultFileName: defaultName || 'archive.zip'
  });
  if (r && r.file) return r.file;
  return null;
}

async function folderDialog(title) {
  var r = await invoke('__dialog.selectFolder', {
    title: title || 'Seleccionar carpeta'
  });
  if (r && r.path) return r.path;
  return null;
}

async function browseCompressSource() {
  var r = await invoke('__dialog.openFile', {
    title: 'Seleccionar archivo o carpeta',
    filter: 'Todos los archivos (*.*)|*.*'
  });
  if (r && r.file) {
    document.getElementById('compressSource').value = r.file;
    var info = await invoke('getFileInfo', { path: r.file });
    if (info && info.success && !info.isDirectory) {
      var fi = info;
      var dir = r.file.substring(0, r.file.lastIndexOf('\\'));
      var name = fi.name;
      var dotIdx = name.lastIndexOf('.');
      var baseName = dotIdx > 0 ? name.substring(0, dotIdx) : name;
      document.getElementById('compressDest').value = dir + '\\' + baseName + '.zip';
    }
  }
}

async function browseCompressDest() {
  var src = document.getElementById('compressSource').value;
  var defaultName = 'archive.zip';
  if (src) {
    var parts = src.split('\\');
    var name = parts[parts.length - 1];
    var dotIdx = name.lastIndexOf('.');
    var base = dotIdx > 0 ? name.substring(0, dotIdx) : name;
    defaultName = base + '.zip';
  }
  var dest = await saveDialog('Guardar ZIP como', 'ZIP (*.zip)|*.zip', defaultName);
  if (dest) document.getElementById('compressDest').value = dest;
}

async function doCompress() {
  var source = document.getElementById('compressSource').value;
  var dest = document.getElementById('compressDest').value;
  var btn = document.getElementById('btnCompress');
  var resultDiv = document.getElementById('compressResult');

  if (!source) { showResult(resultDiv, 'Selecciona un archivo o carpeta', false); return; }
  if (!dest) { showResult(resultDiv, 'Selecciona el destino del ZIP', false); return; }

  btn.disabled = true;
  btn.textContent = 'Comprimiendo...';
  resultDiv.style.display = 'none';

  var r = await invoke('compress', { sourcePath: source, destinationZip: dest });

  if (r.success) {
    var msg = r.message + ' (' + formatSize(r.size) + ')';
    showResult(resultDiv, msg, true);
  } else {
    showResult(resultDiv, 'Error: ' + r.error, false);
  }

  btn.disabled = false;
  btn.textContent = 'Comprimir';
}

async function browseDecompressSource() {
  var r = await browseDialog('Seleccionar archivo ZIP', 'ZIP (*.zip)|*.zip', false);
  if (r && r.file) {
    document.getElementById('decompressSource').value = r.file;
    var dir = r.file.substring(0, r.file.lastIndexOf('\\'));
    var name = r.file.split('\\').pop().replace('.zip', '');
    document.getElementById('decompressDest').value = dir + '\\' + name;
  }
}

async function browseDecompressDest() {
  var folder = await folderDialog('Seleccionar carpeta de destino');
  if (folder) document.getElementById('decompressDest').value = folder;
}

async function doDecompress() {
  var source = document.getElementById('decompressSource').value;
  var dest = document.getElementById('decompressDest').value;
  var btn = document.getElementById('btnDecompress');
  var resultDiv = document.getElementById('decompressResult');

  if (!source) { showResult(resultDiv, 'Selecciona un archivo ZIP', false); return; }
  if (!dest) { showResult(resultDiv, 'Selecciona la carpeta de destino', false); return; }

  btn.disabled = true;
  btn.textContent = 'Descomprimiendo...';
  resultDiv.style.display = 'none';

  var r = await invoke('decompress', { sourceZip: source, destinationFolder: dest });

  if (r.success) {
    showResult(resultDiv, r.message, true);
  } else {
    showResult(resultDiv, 'Error: ' + r.error, false);
  }

  btn.disabled = false;
  btn.textContent = 'Descomprimir';
}

async function browseExploreZip() {
  var r = await browseDialog('Seleccionar archivo ZIP', 'ZIP (*.zip)|*.zip', false);
  if (r && r.file) {
    document.getElementById('exploreZip').value = r.file;
    doExplore();
  }
}

async function doExplore() {
  var zipPath = document.getElementById('exploreZip').value;
  var btn = document.getElementById('btnExplore');
  var resultDiv = document.getElementById('exploreResult');
  var tableDiv = document.getElementById('exploreTable');
  var tbody = document.getElementById('exploreBody');

  if (!zipPath) { showResult(resultDiv, 'Selecciona un archivo ZIP', false); return; }

  btn.disabled = true;
  btn.textContent = 'Explorando...';
  resultDiv.style.display = 'none';
  tableDiv.style.display = 'none';

  var r = await invoke('listZipContents', { zipPath: zipPath });

  if (r.success) {
    tbody.innerHTML = '';
    var files = 0, dirs = 0, totalSize = 0, totalCompressed = 0;

    r.entries.forEach(function(e) {
      var tr = document.createElement('tr');
      if (e.isDirectory) {
        tr.innerHTML = '<td><span style="color:#888;">' + esc(e.name) + '</span></td><td>—</td><td>—</td><td>' + e.lastModified + '</td>';
        dirs++;
      } else {
        tr.innerHTML = '<td>' + esc(e.name) + '</td><td>' + formatSize(e.size) + '</td><td>' + formatSize(e.compressedSize) + '</td><td>' + e.lastModified + '</td>';
        files++;
        totalSize += e.size;
        totalCompressed += e.compressedSize;
      }
      tbody.appendChild(tr);
    });

    var ratio = totalSize > 0 ? ((1 - totalCompressed / totalSize) * 100).toFixed(1) : '0.0';
    document.getElementById('exploreSummary').textContent =
      files + ' archivos, ' + dirs + ' carpetas — ' +
      formatSize(totalSize) + ' → ' + formatSize(totalCompressed) + ' (comprimido ' + ratio + '%)';

    tableDiv.style.display = 'block';
    resultDiv.style.display = 'none';
  } else {
    showResult(resultDiv, 'Error: ' + r.error, false);
  }

  btn.disabled = false;
  btn.textContent = 'Explorar';
}

function showResult(el, msg, isSuccess) {
  el.textContent = msg;
  el.className = 'result ' + (isSuccess ? 'success' : 'error');
  el.style.display = 'block';
}

function formatSize(bytes) {
  if (bytes === 0) return '0 B';
  var units = ['B', 'KB', 'MB', 'GB'];
  var i = Math.floor(Math.log(bytes) / Math.log(1024));
  return (bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0) + ' ' + units[i];
}

function esc(s) {
  var d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}

document.querySelectorAll('.tab').forEach(function(tab) {
  tab.addEventListener('click', function() {
    document.querySelectorAll('.tab').forEach(function(t) { t.classList.remove('active'); });
    document.querySelectorAll('.tab-content').forEach(function(tc) { tc.classList.remove('active'); });
    tab.classList.add('active');
    document.getElementById('tab-' + tab.dataset.tab).classList.add('active');
  });
});
