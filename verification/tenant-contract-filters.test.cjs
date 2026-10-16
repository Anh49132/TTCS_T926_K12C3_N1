// Run the actual Razor page script with a small DOM fixture; no server or database.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/Views/HopDongCuaToi/Index.cshtml'), 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function element(dataset = {}) {
    return {
        dataset, hidden: false, value: '', attributes: {}, listeners: {},
        classList: { toggle() {} },
        setAttribute(key, value) { this.attributes[key] = value; },
        addEventListener(event, callback) { this.listeners[event] = callback; },
        emit(event) { this.listeners[event]?.(); }
    };
}
function fixture(active = []) {
    const ended = [element({ category: 'ended', role: 'Người đứng tên', search: 'hd-cu p101' })];
    const buttons = ['active', 'soon', 'ended', 'all'].map(contractFilter => element({ contractFilter }));
    const ids = Object.fromEntries(['tenant-contract-query', 'tenant-contract-role', 'tenant-ended-contracts', 'tenant-contracts-no-results'].map(id => [id, element()]));
    vm.runInNewContext(script, { document: {
        querySelectorAll(selector) { return ({ '[data-contract-card]': active, '[data-ended-row]': ended, '[data-contract-filter]': buttons })[selector]; },
        getElementById(id) { return ids[id]; }
    } });
    return { ended, buttons, ids, select(filter) { buttons.find(b => b.dataset.contractFilter === filter).emit('click'); } };
}
test('Ended-only contracts remain filterable and do not show a false empty message', () => {
    const f = fixture();
    assert.equal(f.ended[0].hidden, true);
    f.select('ended');
    assert.equal(f.ended[0].hidden, false);
    assert.equal(f.ids['tenant-contracts-no-results'].hidden, true);
    assert.equal(f.ids['tenant-ended-contracts'].hidden, false);
    assert.equal(f.buttons[2].attributes['aria-pressed'], 'true');
    assert.equal(f.buttons[0].attributes['aria-pressed'], 'false');
});
test('Search and role filters hide the ended section when no row matches', () => {
    const f = fixture();
    f.select('all');
    f.ids['tenant-contract-query'].value = 'không khớp';
    f.ids['tenant-contract-query'].emit('input');
    assert.equal(f.ids['tenant-contracts-no-results'].hidden, false);
    assert.equal(f.ids['tenant-ended-contracts'].hidden, true);
    f.ids['tenant-contract-query'].value = 'P101';
    f.ids['tenant-contract-query'].emit('input');
    assert.equal(f.ids['tenant-contracts-no-results'].hidden, true);
    f.ids['tenant-contract-role'].value = 'Người ở cùng';
    f.ids['tenant-contract-role'].emit('change');
    assert.equal(f.ended[0].hidden, true);
});
test('Switching between active and ended contracts keeps the correct rows visible', () => {
    const active = element({ category: 'active', soon: 'true', role: 'Người ở cùng', search: 'hd-moi p202' });
    const f = fixture([active]);
    assert.equal(active.hidden, false);
    f.select('ended');
    assert.equal(active.hidden, true);
    assert.equal(f.ids['tenant-contracts-no-results'].hidden, true);
    f.select('soon');
    assert.equal(active.hidden, false);
    assert.equal(f.ended[0].hidden, true);
});
