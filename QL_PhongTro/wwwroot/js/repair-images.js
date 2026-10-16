(() => {
    const input = document.getElementById('repair-images');
    if (!input) return;
    const preview = document.getElementById('repair-image-preview');
    const error = document.getElementById('repair-image-error');
    let files = [], urls = [];
    function render() {
        urls.forEach(URL.revokeObjectURL); urls = [];
        preview.replaceChildren();
        const transfer = new DataTransfer();
        files.forEach((file, index) => {
            transfer.items.add(file);
            const card = document.createElement('div');
            const image = document.createElement('img');
            image.src = URL.createObjectURL(file); urls.push(image.src);
            image.alt = `Ảnh đã chọn ${index + 1}`;
            const name = document.createElement('span'); name.textContent = file.name;
            const remove = document.createElement('button');
            remove.type = 'button'; remove.textContent = `Bỏ ảnh ${index + 1}`;
            remove.addEventListener('click', () => { files.splice(index, 1); error.textContent = ''; render(); });
            card.append(image, name, remove); preview.append(card);
        });
        input.files = transfer.files;
        document.querySelectorAll('[data-repair-image-count]').forEach(node => node.textContent = files.length + '/3 ảnh');
        input.dispatchEvent(new CustomEvent('repairimageschanged', { bubbles: true }));
    }
    function add(chosen) {
        let message = '';
        if (files.length + chosen.length > 3) message = 'Chỉ được chọn tối đa 3 ảnh. Hãy bỏ ảnh trước khi chọn thêm.';
        else if (chosen.some(f => !/\.(jpe?g|png)$/i.test(f.name))) message = 'Chỉ nhận ảnh JPG/JPEG hoặc PNG.';
        else if (chosen.some(f => f.size === 0 || f.size > 5242880)) message = 'Mỗi ảnh phải có nội dung và không vượt 5 MiB (5.242.880 byte).';
        if (!message) files.push(...chosen);
        error.textContent = message; render();
    }
    input.addEventListener('change', () => add(Array.from(input.files)));
    const zone = document.querySelector('[data-repair-drop]');
    if (zone) {
        zone.addEventListener('dragover', event => { event.preventDefault(); zone.classList.add('is-dragging'); });
        zone.addEventListener('dragleave', () => zone.classList.remove('is-dragging'));
        zone.addEventListener('drop', event => { event.preventDefault(); zone.classList.remove('is-dragging'); add(Array.from(event.dataTransfer.files)); });
    }
    window.addEventListener('pageshow', event => { if (event.persisted) render(); });
    window.addEventListener('pagehide', () => urls.forEach(URL.revokeObjectURL));
})();
