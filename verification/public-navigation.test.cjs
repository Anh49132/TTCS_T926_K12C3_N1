const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const script = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/public-navigation.js'), 'utf8');
function fixture() {
    const events = {}, toggleEvents = {}, navigationEvents = {};
    let shown = false, prevented = false;
    const mobile = { matches: true };
    const document = { activeElement: null, addEventListener(name, handler) { events[name] = handler; } };
    const link = { focus() { document.activeElement = link; } };
    const toggle = { focus() { document.activeElement = toggle; }, contains: target => target === toggle,
        addEventListener(name, handler) { toggleEvents[name] = handler; } };
    const navigation = { classList: { contains: () => shown }, contains: target => target === link,
        querySelector: () => link, addEventListener(name, handler) { navigationEvents[name] = handler; } };
    document.getElementById = () => navigation;
    document.querySelector = () => toggle;
    vm.runInNewContext(script, { document, window: { matchMedia: () => mobile, bootstrap: { Collapse: {
        getOrCreateInstance: () => ({ show() { shown = true; navigationEvents['shown.bs.collapse'](); }, hide() { shown = false; navigationEvents['hidden.bs.collapse'](); } })
    } } } });
    return { document, link, toggle, mobile, isShown: () => shown,
        emit(type, key, target = toggle) { (type === 'toggle' ? toggleEvents.keydown : events[type])({ key, target, preventDefault() { prevented = true; } }); },
        prevented: () => prevented };
}
test('ArrowDown opens mobile navigation and focuses its first link', () => {
    const f = fixture(); f.emit('toggle', 'ArrowDown');
    assert.equal(f.isShown(), true); assert.equal(f.document.activeElement, f.link); assert.equal(f.prevented(), true);
});
test('Escape closes navigation and returns focus to the menu button', () => {
    const f = fixture(); f.emit('toggle', 'ArrowDown'); f.emit('keydown', 'Escape', f.link);
    assert.equal(f.isShown(), false); assert.equal(f.document.activeElement, f.toggle);
});
test('Escape outside navigation leaves it alone and desktop ArrowDown is untouched', () => {
    const f = fixture(); f.emit('toggle', 'ArrowDown'); f.emit('keydown', 'Escape', {});
    assert.equal(f.isShown(), true);
    const desktop = fixture(); desktop.mobile.matches = false; desktop.emit('toggle', 'ArrowDown');
    assert.equal(desktop.isShown(), false); assert.equal(desktop.prevented(), false);
});
test('Pointer outside the navigation closes it without moving outside focus', () => {
    const f = fixture(); f.emit('toggle', 'ArrowDown');
    const outside = {}; f.document.activeElement = outside; f.emit('pointerdown', null, outside);
    assert.equal(f.isShown(), false); assert.equal(f.document.activeElement, outside);
});
