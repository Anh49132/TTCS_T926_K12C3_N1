// Let the existing MVC unobtrusive validator show errors beside each price field.
// Disabled fields are excluded by both jQuery validation and form submission.
$(function () {
    const sections = [...document.querySelectorAll('[data-utility]')];
    const notice = document.getElementById('utility-effective-period');
    const refreshPeriod = () => {
        if (!notice) return;
        notice.hidden = !sections.some(section => {
            const method = section.querySelector('[data-method]').value;
            const input = section.querySelector('[data-price="' + method + '"] input');
            if (!input || !/^\+?\d+$/.test(input.value.trim())) return false;
            // Compare integer VND exactly, including values beyond JavaScript's safe integer range.
            const price = BigInt(input.value.trim());
            if (price <= 0n || price > 9223372036854775807n) return false;
            const saved = section.dataset.savedPrice;
            return method !== section.dataset.savedMethod || !saved || price !== BigInt(saved);
        });
    };
    document.querySelectorAll('[data-utility]').forEach(section => {
        const method = section.querySelector('[data-method]');
        section.querySelectorAll('[data-price] input').forEach(input => {
            $(input).rules('add', {
                min: 1,
                messages: { min: 'Mức giá phải là số nguyên đồng lớn hơn 0.' }
            });
        });
        const refresh = () => section.querySelectorAll('[data-price]').forEach(group => {
            const active = group.dataset.price === method.value;
            group.hidden = !active;
            group.querySelectorAll('input').forEach(input => {
                input.disabled = !active;
                input.required = active;
                if (!active) {
                    $(input).removeClass('input-validation-error').attr('aria-invalid', 'false');
                    $(section).find('[data-valmsg-for]').filter(function () {
                        return this.dataset.valmsgFor === input.name;
                    }).empty().removeClass('field-validation-error').addClass('field-validation-valid');
                }
            });
        });
        method.addEventListener('change', refresh);
        method.addEventListener('change', refreshPeriod);
        section.querySelectorAll('[data-price] input').forEach(input => {
            input.addEventListener('input', refreshPeriod);
            input.addEventListener('change', refreshPeriod);
        });
        refresh();
    });
    refreshPeriod();
});
