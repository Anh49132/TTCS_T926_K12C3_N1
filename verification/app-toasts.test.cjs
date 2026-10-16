const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/site.js'), 'utf8');
// Normalize Windows line endings before extracting the actual toast controller.
const normalized = source.replaceAll('\r\n', '\n');
const script = normalized.slice(normalized.indexOf("(() => {\n    document.querySelectorAll('[data-app-toast]')"));
const controller = script.slice(0, script.indexOf('\n})();') + '\n})();'.length);
function fixture(error = false) {
    const timers = new Map(), handlers = {}, close = {};
    let nextId = 0;
    const toast = {
        removed: false,
        classList: { contains: name => error && name === 'app-toast-error' },
        contains: target => target === close,
        addEventListener(name, callback) { handlers[name] = callback; },
        querySelector() { return { addEventListener(name, callback) { close[name] = callback; } }; },
        remove() { this.removed = true; }
    };
    vm.runInNewContext(controller, {
        document: { querySelectorAll: () => [toast] },
        window: {
            setTimeout(callback, duration) { timers.set(++nextId, { callback, duration }); return nextId; },
            clearTimeout(id) { timers.delete(id); }
        }
    });
    return { toast, timers, close, emit(name, event = {}) { handlers[name](event); } };
}
test('Error messages stay until explicitly dismissed', () => {
    const f = fixture(true);
    assert.equal(f.timers.size, 0);
    f.emit('mouseenter'); f.emit('mouseleave');
    assert.equal(f.timers.size, 0);
    f.close.click();
    assert.equal(f.toast.removed, true);
});
test('Success messages wait six seconds and pause while hovered or focused', () => {
    const f = fixture();
    assert.equal([...f.timers.values()][0].duration, 6000);
    f.emit('mouseenter');
    assert.equal(f.timers.size, 0);
    f.emit('focusin'); f.emit('mouseleave');
    assert.equal(f.timers.size, 0);
    f.emit('focusout', { relatedTarget: null });
    assert.equal(f.timers.size, 1);
    [...f.timers.values()][0].callback();
    assert.equal(f.toast.removed, true);
});
test('Moving focus inside a toast keeps it visible and closing clears the timer', () => {
    const f = fixture();
    f.emit('focusin'); f.emit('focusout', { relatedTarget: f.close });
    assert.equal(f.timers.size, 0);
    f.emit('focusout', { relatedTarget: null });
    f.close.click();
    assert.equal(f.timers.size, 0);
    assert.equal(f.toast.removed, true);
});
