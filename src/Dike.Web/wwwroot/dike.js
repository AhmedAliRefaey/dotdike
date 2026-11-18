(() => {
    const sidebar = document.querySelector('.sidebar');
    const toggle = document.querySelector('.nav-toggle');
    if (sidebar && toggle) {
        document.documentElement.classList.add('has-navigation');
        toggle.hidden = false;
        const setOpen = (open) => {
            sidebar.dataset.navOpen = String(open);
            toggle.setAttribute('aria-expanded', String(open));
        };
        toggle.addEventListener('click', () => setOpen(toggle.getAttribute('aria-expanded') !== 'true'));
        sidebar.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && toggle.getAttribute('aria-expanded') === 'true') {
                setOpen(false);
                toggle.focus();
            }
        });
    }
    const method = document.querySelector('[data-auth-method]');
    if (method) {
        const updateFields = () => {
            document.querySelectorAll('[data-auth-fields]').forEach((fieldset) => {
                fieldset.hidden = fieldset.dataset.authFields !== method.value;
            });
        };
        method.addEventListener('change', updateFields);
        updateFields();
    }
    document.querySelectorAll('[data-odoo-period-form]').forEach((form) => {
        const mode = form.querySelector('[data-odoo-period-mode]');
        if (!mode) return;
        const updatePeriod = () => {
            form.querySelectorAll('[data-odoo-period-fields]').forEach((group) => {
                const active = group.dataset.odooPeriodFields === mode.value;
                group.hidden = !active;
                group.querySelectorAll('input').forEach((input) => { input.disabled = !active; });
            });
        };
        mode.addEventListener('change', updatePeriod);
        updatePeriod();
    });
})();
