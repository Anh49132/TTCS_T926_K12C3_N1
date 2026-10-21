// S2-08 lát 2: cảnh báo lịch hẹn trùng trong khoảng ±30 phút.
// Cảnh báo chỉ thông tin; chủ nhà vẫn xác nhận được.
(function () {
    const picker = document.querySelector('[data-role="lich-hen"]');
    const datePicker = document.querySelector('[data-role="lich-hen-ngay"]');
    const timePicker = document.querySelector('[data-role="lich-hen-gio"]');
    const warning = document.querySelector('[data-role="canh-bao-trung"]');
    if (!picker || !datePicker || !timePicker || !warning) return;

    const yeuCauId = warning.dataset.yeuCauId;
    if (!yeuCauId) return;

    const defaultText = warning.textContent;
    let lastChecked = null;

    function normalizeTime() {
        const value = timePicker.value.trim();
        const compact = value.match(/^([01]\d|2[0-3])([0-5]\d)$/);
        const short = value.match(/^([0-9]):([0-5]\d)$/);
        if (compact) timePicker.value = `${compact[1]}:${compact[2]}`;
        else if (short) timePicker.value = `0${short[1]}:${short[2]}`;
    }

    function syncValue() {
        normalizeTime();
        const match = timePicker.value.trim().match(/^([01]\d|2[0-3]):([0-5]\d)$/);
        timePicker.setCustomValidity(match || !timePicker.value ? '' : 'Nhập giờ theo định dạng HH:mm, ví dụ 19:30.');
        if (!datePicker.value || !match) {
            picker.value = '';
            return '';
        }
        const time = `${match[1].padStart(2, '0')}:${match[2]}`;
        timePicker.value = time;
        picker.value = `${datePicker.value}T${time}`;
        return picker.value;
    }

    function render(list) {
        warning.classList.remove('text-danger');
        if (!list || list.length === 0) {
            warning.textContent = defaultText;
            return;
        }
        const items = list.map(x => `${x.lichHenHienThoi} — ${x.tenPhong}, khách ${x.tenKhach} (${x.maYeuCau})`);
        warning.classList.add('text-danger');
        warning.textContent = `Cảnh báo trùng lịch: phòng đã có ${list.length} lịch hẹn khác trong khoảng ±30 phút: `
            + items.join('; ') + '. Bạn vẫn có thể xác nhận.';
    }

    async function check() {
        const value = syncValue();
        if (!value) { render([]); return; }
        if (value === lastChecked) return;
        lastChecked = value;
        try {
            const url = `/LichHen/LichTrung?id=${encodeURIComponent(yeuCauId)}`
                + `&lichHen=${encodeURIComponent(value)}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!response.ok) { render([]); return; }
            render(await response.json());
        } catch {
            // A failed advisory lookup must never block confirming the slot.
            render([]);
        }
    }

    datePicker.addEventListener('change', check);
    timePicker.addEventListener('input', () => {
        normalizeTime();
        syncValue();
    });
    timePicker.addEventListener('change', check);
    picker.form.addEventListener('submit', syncValue);
    check();
})();

// S2-08 lát 3: AC2 yêu cầu "lý do khác" phải kèm ghi chú. Giao diện hiện/ẩn ô ghi chú và bắt
// nhập 5 đến 500 ký tự để người dùng thấy lỗi ngay. LichHenService vẫn kiểm lại ở server và là
// nơi quyết định, nên gửi form bằng tay cũng không tạo được yêu cầu thiếu ghi chú.
(function () {
    const lyDo = document.querySelector('[data-role="ly-do-tu-choi"]');
    const vung = document.querySelector('[data-role="vung-ghi-chu"]');
    const oGhiChu = document.querySelector('[data-role="ghi-chu-tu-choi"]');
    const loi = document.querySelector('[data-role="loi-ghi-chu"]');
    if (!lyDo || !vung || !oGhiChu || !loi) return;

    const LY_DO_KHAC = 'LY_DO_KHAC';
    const TOI_DA = 500;
    const TOI_THIEU = 5;
    const form = oGhiChu.closest('form');

    function chuanHoa(value) { return (value || '').trim(); }

    function hienThi() {
        vung.classList.toggle('d-none', lyDo.value !== LY_DO_KHAC);
        if (lyDo.value !== LY_DO_KHAC) loi.textContent = '';
    }

    function kiemTra() {
        if (lyDo.value !== LY_DO_KHAC) return true;
        const ghiChu = chuanHoa(oGhiChu.value);
        loi.classList.remove('text-danger');
        if (ghiChu.length < TOI_THIEU) {
            loi.classList.add('text-danger');
            loi.textContent = 'Vui lòng nhập ghi chú từ 5 đến 500 ký tự.';
            return false;
        }
        loi.textContent = '';
        return true;
    }

    lyDo.addEventListener('change', hienThi);
    oGhiChu.addEventListener('input', () => {
        if (oGhiChu.dataset.doiKiemTra === '1') kiemTra();
    });
    form.addEventListener('submit', (event) => {
        if (!kiemTra()) {
            event.preventDefault();
            vung.classList.remove('d-none');
            oGhiChu.focus();
        }
    });

    hienThi();
})();
