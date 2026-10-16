const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const script = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/form-feedback.js'), 'utf8');
function field(name, description = '', type = 'text') {
    return { name, type, attributes: { 'aria-describedby': description },
        getAttribute(key) { return this.attributes[key]; },
        setAttribute(key, value) { this.attributes[key] = value; },
        removeAttribute(key) { delete this.attributes[key]; } };
}
function fixture(fields, messages, proxies = []) {
    const observers = [];
    const form = { querySelectorAll: selector => selector === '[data-valmsg-for]' ? messages : selector === '[data-validation-for]' ? proxies : fields };
    vm.runInNewContext(script, {
        document: { querySelectorAll: () => [form], getElementById: id => id === 'form-feedback-1' ? {} : null },
        MutationObserver: class { constructor(callback) { this.callback = callback; } observe(message) { observers.push(() => this.callback(message)); } }
    });
    return observers;
}
const message = (name, text, id = '') => ({ id, textContent: text, getAttribute: () => name });
test('Field errors keep existing help text and avoid duplicate IDs', () => {
    const input = field('GiaThue', 'rent-help'); const error = message('GiaThue', 'Giá không hợp lệ');
    fixture([input], [error]);
    assert.equal(error.id, 'form-feedback-2');
    assert.equal(input.attributes['aria-describedby'], 'rent-help form-feedback-2');
    assert.equal(input.attributes['aria-invalid'], 'true');
});
test('Updating or clearing a message updates field accessibility state', () => {
    const input = field('Email'); const error = message('Email', '');
    const [update] = fixture([input], [error]);
    assert.equal(input.attributes['aria-invalid'], undefined);
    error.textContent = 'Email không hợp lệ'; update();
    assert.equal(input.attributes['aria-invalid'], 'true');
    error.textContent = ''; update();
    assert.equal(input.attributes['aria-invalid'], undefined);
});
test('Same-name radio controls share an error while hidden fields are untouched', () => {
    const first = field('TrangThai', 'state-help', 'radio');
    const second = field('TrangThai', '', 'radio');
    const hidden = field('TrangThai', '', 'hidden');
    fixture([first, second, hidden], [message('TrangThai', 'Chọn trạng thái', 'state-error')]);
    assert.equal(first.attributes['aria-describedby'], 'state-help state-error');
    assert.equal(second.attributes['aria-invalid'], 'true');
    assert.equal(hidden.attributes['aria-invalid'], undefined);
});
test('Custom select button receives the error description and tracks its native field', () => {
    const select = field('ToaNhaId', 'building-help', 'select-one');
    const button = field('', 'picker-help', 'button');
    button.attributes['data-validation-for'] = 'ToaNhaId';
    const error = message('ToaNhaId', 'Chọn cơ sở', 'building-error');
    const [update] = fixture([select], [error], [button]);
    assert.equal(button.attributes['aria-describedby'], 'picker-help building-error');
    assert.equal(button.attributes['aria-invalid'], 'true');
    assert.equal(select.attributes['aria-invalid'], 'true');
    error.textContent = ''; update();
    assert.equal(button.attributes['aria-invalid'], undefined);
    assert.equal(select.attributes['aria-invalid'], undefined);
});
test('Feedback for another field does not mark a custom select button', () => {
    const input = field('MaPhong');
    const button = field('', 'picker-help', 'button');
    button.attributes['data-validation-for'] = 'ToaNhaId';
    fixture([input], [message('MaPhong', 'Mã phòng đã tồn tại')], [button]);
    assert.equal(button.attributes['aria-describedby'], 'picker-help');
    assert.equal(button.attributes['aria-invalid'], undefined);
});
