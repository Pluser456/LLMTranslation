// 模拟 DOM, 单测快捷键捕获 IIFE 的渐进显示逻辑
var listeners = { focus: [], blur: [], keydown: [] };
var el = {
  value: 'Alt+Q',
  addEventListener: function (t, f) { listeners[t].push(f); }
};
var stub = {
  value: '', checked: false, style: {},
  classList: { toggle: function () {} },
  addEventListener: function () {}
};
global.document = { getElementById: function (id) { return id === 'sHot' ? el : stub; } };
global.window = {};
var $ = function (id) { return document.getElementById(id); };
var CFG = { Hotkey: 'Alt+Q' };
var src = require('fs').readFileSync('tmpcheck/settings_script.js', 'utf8');
var i = src.indexOf('/* 快捷键捕获'), j = src.indexOf('})();', src.indexOf("var el = $('sHot')"));
eval(src.substring(i, j + 5));

listeners.focus[0]();
if (el.value !== '') { console.log('FAIL 聚焦后未清空, got="' + el.value + '"'); process.exit(1); }
console.log('PASS 点击(聚焦)后清空');

function press(mods, key) {
  listeners.keydown[0]({ key: key, ctrlKey: !!mods.c, altKey: !!mods.a, shiftKey: !!mods.s, metaKey: !!mods.m,
    preventDefault: function () {}, stopPropagation: function () {} });
  return el.value;
}
var seq = [
  [press({ c: 1 }, 'Control'), 'Ctrl'],
  [press({ c: 1, s: 1 }, 'Shift'), 'Ctrl+Shift'],
  [press({ c: 1, s: 1 }, 'q'), 'Ctrl+Shift+Q'],
  [press({}, 'Escape'), ''],
  [press({ a: 1 }, 'Alt'), 'Alt'],
  [press({ a: 1 }, 'F5'), 'Alt+F5'],
  [press({ c: 1, a: 1, s: 1 }, 'Meta'), 'Ctrl+Alt+Shift'],
  [press({ c: 1, a: 1, s: 1, m: 1 }, 'Meta'), 'Ctrl+Alt+Shift+Win'],
  [press({ c: 1 }, ' '), 'Ctrl+Space']
];
var ok = true;
for (var k = 0; k < seq.length; k++) {
  if (seq[k][0] !== seq[k][1]) { ok = false; console.log('FAIL got="' + seq[k][0] + '" want="' + seq[k][1] + '"'); }
  else console.log('PASS "' + seq[k][1] + '"');
}
press({}, 'Escape');   // 清空后再失焦, 应恢复旧值
listeners.blur[0]();
if (el.value !== 'Alt+Q') { ok = false; console.log('FAIL blur 恢复 got="' + el.value + '"'); }
else console.log('PASS blur 后恢复旧值 Alt+Q');
process.exit(ok ? 0 : 1);
