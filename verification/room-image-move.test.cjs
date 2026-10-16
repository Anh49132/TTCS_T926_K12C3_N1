// Test the actual image-move handler with a DOM fixture; no files or requests.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../QL_PhongTro/wwwroot/js/room-images.js'), 'utf8');
const handler = source.match(/    function moveImage\(card, direction\) \{[\s\S]*?(?=\r?\n    grid\.addEventListener\('click')/)[0];
function fixture() {
    const cards = [1, 2, 3].map(id => ({ id, focus() { this.focused = true; } }));
    const original = [...cards];
    for (const card of cards) Object.defineProperty(card, 'nextSibling', { get() { return cards[cards.indexOf(card) + 1] || null; } });
    const saved = [];
    const context = vm.createContext({
        isSavingOrder: false, isDeletingImage: false,
        imageCards: () => [...cards], imageIds: () => cards.map(card => card.id),
        grid: { insertBefore(card, target) { cards.splice(cards.indexOf(card), 1); cards.splice(target ? cards.indexOf(target) : cards.length, 0, card); } },
        saveCurrentOrder(previous) { saved.push(Array.from(previous)); }
    });
    vm.runInContext(handler, context);
    return { cards, original, saved, context, move(card, direction) { context.moveImage(card, direction); } };
}
test('Moving forward and backward keeps all images and sends the old order for rollback', () => {
    const f = fixture();
    f.move(f.original[0], 1);
    assert.deepEqual(f.cards.map(x => x.id), [2, 1, 3]);
    assert.deepEqual(f.saved[0], [1, 2, 3]);
    assert.equal(f.original[0].focused, true);
    f.move(f.original[2], -1);
    assert.deepEqual(f.cards.map(x => x.id), [2, 3, 1]);
    assert.deepEqual(f.saved[1], [2, 1, 3]);
});
test('Moving outside the list or in an invalid direction does not save', () => {
    const f = fixture();
    f.move(f.original[0], -1);
    f.move(f.original[2], 1);
    f.move(f.original[1], 5);
    assert.equal(f.saved.length, 0);
    assert.deepEqual(f.cards.map(x => x.id), [1, 2, 3]);
});
test('Saving or deleting blocks another move', () => {
    const f = fixture();
    f.context.isSavingOrder = true;
    f.move(f.original[0], 1);
    f.context.isSavingOrder = false;
    f.context.isDeletingImage = true;
    f.move(f.original[0], 1);
    assert.equal(f.saved.length, 0);
    assert.deepEqual(f.cards.map(x => x.id), [1, 2, 3]);
});
