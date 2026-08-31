
var $ = function (id) { return document.getElementById(id); };
var CFG = {};
function post(o) { if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(JSON.stringify(o)); }
window.init = function (cfg, tab) {
  CFG = cfg || {};
  document.body.style.zoom = (CFG.UiZoom || 100) / 100;
  $('sBase').value = CFG.ApiBase || ''; $('sPort').value = CFG.Port || ''; $('sModel').value = CFG.Model || '';
  $('sKey').value = CFG.ApiKey || ''; $('sPrompt').value = CFG.Prompt || ''; $('sFont').value = CFG.FontSize || 15;
  $('sThink').checked = !!CFG.EnableThinking; $('sSel').checked = CFG.EnableSelection !== false; $('sAuto').checked = !!CFG.AutoStart;
  $('sZoom').value = CFG.UiZoom || 100; $('sDelay').value = CFG.ButtonDelay || 0; $('sHot').value = CFG.Hotkey || 'Alt+Q';
  if (tab === 'help') showTab('help'); else showTab('set');
};
function showTab(t) {
  $('tabSet').classList.toggle('on', t === 'set'); $('tabHelp').classList.toggle('on', t === 'help');
  $('paneSet').style.display = t === 'set' ? 'block' : 'none';
  $('paneHelp').style.display = t === 'help' ? 'block' : 'none';
}
$('tabSet').onclick = function () { showTab('set'); };
$('tabHelp').onclick = function () { showTab('help'); };
$('drag').addEventListener('mousedown', function (e) { if (e.button === 0) post({ t: 'drag' }); });
$('close').onclick = function () { post({ t: 'close' }); };
$('cancel').onclick = function () { post({ t: 'close' }); };
$('save').onclick = function () {
  post({
    t: 'save', cfg: {
      ApiBase: $('sBase').value.trim(), Port: $('sPort').value.trim(), Model: $('sModel').value.trim(),
      ApiKey: $('sKey').value.trim(), Prompt: $('sPrompt').value, FontSize: +$('sFont').value || 15,
      EnableThinking: $('sThink').checked, EnableSelection: $('sSel').checked, AutoStart: $('sAuto').checked,
      UiZoom: +$('sZoom').value || 100, ButtonDelay: +$('sDelay').value || 0, Hotkey: $('sHot').value.trim() || 'Alt+Q'
    }
  });
};
/* 快捷键捕获: 点击(聚焦)后清空, 随后每按一个键渐进显示组合 —
   按 Ctrl → "Ctrl", 再按 Shift → "Ctrl+Shift", 再按 Q → "Ctrl+Shift+Q"; Esc 清空重录 */
(function () {
  var el = $('sHot'), listening = false;
  el.addEventListener('focus', function () { listening = true; el.value = ''; });
  el.addEventListener('blur', function () { listening = false; if (!el.value) el.value = CFG.Hotkey || 'Alt+Q'; });
  el.addEventListener('keydown', function (e) {
    if (!listening) return;
    e.preventDefault(); e.stopPropagation();   // 吞掉按键, 防其触发系统/页面行为
    var parts = [];
    if (e.ctrlKey) parts.push('Ctrl');
    if (e.altKey) parts.push('Alt');
    if (e.shiftKey) parts.push('Shift');
    if (e.metaKey) parts.push('Win');
    var k = e.key;
    if (k === 'Control' || k === 'Alt' || k === 'Shift' || k === 'Meta') {
      el.value = parts.join('+');   // 只按了修饰键: 先显示当前修饰组合
      return;
    }
    if (k === 'Escape') { el.value = ''; return; }   // Esc: 清空重录 (stopPropagation 已防止误关窗口)
    if (!k || k === 'Dead') return;
    if (k === ' ') k = 'Space';
    else if (k.length === 1) k = k.toUpperCase();
    parts.push(k);
    el.value = parts.join('+');
  });
})();
document.addEventListener('keydown', function (e) { if (e.key === 'Escape') post({ t: 'close' }); });
window.addEventListener('load', function () { post({ t: 'ready', dpr: window.devicePixelRatio }); });
