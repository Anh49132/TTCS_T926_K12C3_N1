// Exercise the actual room-list renderer without a server, screenshots or database.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/room-management.js'), 'utf8');
const renderer = source.match(/function render\(\)\{[\s\S]*?(?=\r?\nsize\.addEventListener)/)[0];
function fixture(total, current = 1) {
    const children = [], count = {}, empty = {};
    let context;
    const pages = {
        contains(node) { return children.includes(node); },
        replaceChildren() { children.length = 0; },
        append(node) { children.push(node); },
        querySelector() { return children.find(node => node.attributes['aria-current'] === 'page'); }
    };
    context = vm.createContext({ rows: Array.from({ length: total }, () => ({})), page: current, size: { value: '10' }, pages,
        document: {
            activeElement: null,
            getElementById(id) { return id === 'rm-count' ? count : empty; },
            createElement(tag) { return { tag, attributes: {}, handlers: {},
                setAttribute(key, value) { this.attributes[key] = value; },
                addEventListener(key, callback) { this.handlers[key] = callback; },
                focus() { context.document.activeElement = this; }
            }; }
        }
    });
    vm.runInContext(renderer + '\nrender();', context);
    return { context, children, count, empty, render() { vm.runInContext('render();', context); } };
}
test('500 rooms show a bounded set of page buttons and the correct ten rows', () => {
    const f = fixture(500, 25);
    assert.deepEqual(f.children.filter(x => x.tag === 'button').map(x => Number(x.textContent)), [1, 24, 25, 26, 50]);
    assert.equal(f.context.rows.filter(x => !x.hidden).length, 10);
    assert.equal(f.context.rows[240].hidden, false);
    assert.match(f.count.textContent, /241–250/);
});
test('Changing page restores keyboard focus to the new current-page button', () => {
    const f = fixture(30);
    const next = f.children.find(x => x.textContent === 2);
    f.context.document.activeElement = next;
    next.handlers.click();
    assert.equal(f.context.page, 2);
    assert.equal(f.context.document.activeElement.attributes['aria-current'], 'page');
    assert.equal(f.context.document.activeElement.textContent, 2);
});
test('Empty list and a smaller page count clamp pagination safely', () => {
    const f = fixture(0, 10);
    assert.equal(f.context.page, 1);
    assert.equal(f.empty.hidden, false);
    assert.match(f.count.textContent, /0–0/);
    const populated = fixture(21, 3);
    populated.context.size.value = '50';
    populated.render();
    assert.equal(populated.context.page, 1);
    assert.equal(populated.context.rows.filter(x => !x.hidden).length, 21);
});
