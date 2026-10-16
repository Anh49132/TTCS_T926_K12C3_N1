const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const settle = () => new Promise(resolve => setImmediate(resolve));
function fixture(kind, responses) {
    const nodes = new Map(), calls = [];
    const node = id => {
        if (!nodes.has(id)) nodes.set(id, { hidden: true, disabled: false, dataset: {}, handlers: {}, files: [], setAttribute() {},
            addEventListener(name, handler) { this.handlers[name] = handler; }, replaceChildren() {}, append() {} });
        return nodes.get(id);
    };
    const page = node('page');
    page.dataset = { invoiceUrl: '/invoice/42', listUrl: '/invoices', page: '2', detailUrl: '/detail', pageUrl: '/list' };
    const form = node('form');
    form.elements = { namedItem: name => ({ value: name === 'ky' ? '2026-10' : 'QUA_HAN' }) };
    page.querySelector = () => form;
    const prefix = kind === 'list' ? 'tenant-invoice-list' : 'tenant-invoice';
    const success = kind === 'list'
        ? { trang: 2, tongSoHoaDon: 0, hoaDons: [] }
        : { thang: 10, nam: 2026, maPhong: 'P101', chiTiet: [], tongCong: 100, soDaThanhToan: 0, soConPhaiTra: 100, hanThanhToan: '20/10/2026', quaHan: false };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, `../QL_PhongTro/wwwroot/js/tenant-invoice-${kind}.js`), 'utf8'), {
        document: { querySelector: selector => selector === `.tenant-invoice-${kind}` ? page : node(selector), getElementById: node },
        window: { location: { search: '?ky=2026-10&trangThai=QUA_HAN&page=2', origin: 'http://localhost' } },
        URL, URLSearchParams, Intl, console: { error() {} },
        fetch: async (url, options) => {
            calls.push({ url: String(url), options });
            const outcome = responses.shift();
            if (outcome === 'network') throw new Error('offline');
            const status = outcome || 200;
            return { ok: status === 200, status, json: async () => success };
        }
    });
    return { calls, retry: node(`${prefix}-retry`), error: node(`${prefix}-error`), loading: node(`${prefix}-loading`) };
}
for (const kind of ['list', 'detail']) {
    test(`${kind}: retry recovers after network failure and reuses the same request`, async () => {
        const f = fixture(kind, ['network', 200]);
        await settle();
        assert.equal(f.error.hidden, false);
        assert.equal(f.retry.hidden, false);
        assert.equal(f.retry.disabled, false);
        f.retry.handlers.click();
        assert.equal(f.retry.disabled, true);
        await settle();
        assert.equal(f.error.hidden, true);
        assert.equal(f.retry.hidden, true);
        assert.equal(f.retry.disabled, false);
        assert.equal(f.calls.length, 2);
        assert.equal(f.calls[0].url, f.calls[1].url);
        assert.equal(f.calls[1].options.cache, 'no-store');
        assert.equal(f.calls[1].options.credentials, 'same-origin');
    });
}
test('Access-denied invoice does not offer a retry action', async () => {
    const f = fixture('detail', [403]);
    await settle();
    assert.equal(f.error.hidden, false);
    assert.equal(f.retry.hidden, true);
    assert.match(f.error.textContent, /không có quyền/);
});
