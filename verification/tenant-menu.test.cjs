const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/site.js'), 'utf8').replaceAll('\r\n', '\n');
const start = source.indexOf("(() => {\n    const dropdowns = [...document.querySelectorAll('.tenant-header details')]");
const script = source.slice(start, source.indexOf('\n})();', start) + '\n})();'.length);
function fixture() {
    const events = {};
    const document = { activeElement: null, addEventListener(name, handler) { events[name] = handler; } };
    const menus = [0, 1].map(() => {
        const link = {}, summary = { focus() { document.activeElement = summary; } };
        return { open: false, link, summary, handlers: {}, contains: node => node === link || node === summary,
            querySelector: () => summary, addEventListener(name, handler) { this.handlers[name] = handler; } };
    });
    document.querySelectorAll = () => menus;
    vm.runInNewContext(script, { document });
    return { menus, document, emit(type, target, key) {
        let prevented = false;
        events[type]({ target, key, preventDefault() { prevented = true; } });
        return prevented;
    } };
}
test('Escape returns keyboard focus from a menu item to its summary', () => {
    const f = fixture(); f.menus[0].open = true; f.document.activeElement = f.menus[0].link;
    assert.equal(f.emit('keydown', f.menus[0].link, 'Escape'), true);
    assert.equal(f.menus[0].open, false);
    assert.equal(f.document.activeElement, f.menus[0].summary);
});
test('Tabbing outside closes the menu without moving focus', () => {
    const f = fixture(); const outside = {};
    f.menus[0].open = true; f.document.activeElement = outside;
    f.emit('focusin', outside);
    assert.equal(f.menus[0].open, false); assert.equal(f.document.activeElement, outside);
});
test('Focusing inside keeps the menu open and opening another closes the first', () => {
    const f = fixture(); f.menus[0].open = true;
    f.emit('focusin', f.menus[0].link);
    assert.equal(f.menus[0].open, true);
    f.menus[1].open = true; f.menus[1].handlers.toggle();
    assert.equal(f.menus[0].open, false); assert.equal(f.menus[1].open, true);
});
