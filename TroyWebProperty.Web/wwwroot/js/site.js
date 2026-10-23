(() => {
    const element = document.getElementById('editorModal');
    if (!element) return;
    const modal = bootstrap.Modal.getOrCreateInstance(element);
    const content = document.getElementById('modalContent');

    function parseValidation() {
        if (window.jQuery?.validator?.unobtrusive) $.validator.unobtrusive.parse(content);
    }

    function showError(message) {
        const alert = document.createElement('div');
        alert.className = 'alert alert-danger m-3';
        alert.setAttribute('role', 'alert');
        alert.textContent = message;
        content.prepend(alert);
    }

    const focusForm = () => {
        const invalid = content.querySelector('[aria-invalid="true"], .input-validation-error');
        const field = content.querySelector('input:not([type="hidden"]):not([disabled]):not([readonly]), select:not([disabled]), textarea:not([disabled])');
        (invalid || field || content.querySelector('button'))?.focus();
    };
    let opener;
    let loadRequest;
    let savingForm;
    let loadGeneration = 0;
    element.addEventListener('hide.bs.modal', event => {
        if (savingForm) {
            event.preventDefault();
            return;
        }
        loadGeneration++;
        loadRequest?.abort();
    });
    element.addEventListener('shown.bs.modal', focusForm);
    element.addEventListener('hidden.bs.modal', () => {
        if (opener?.isConnected) opener.focus(); else document.getElementById('main-content')?.focus();
    });
    document.addEventListener('click', async event => {
        const button = event.target.closest('[data-modal-url]');
        if (!button) return;
        event.preventDefault();
        opener = button;
        loadRequest?.abort();
        const request = new AbortController();
        loadRequest = request;
        const generation = ++loadGeneration;
        content.innerHTML = '<div class="p-5 text-center" role="status"><span class="spinner-border text-primary mb-3" aria-hidden="true"></span><h2 id="modalTitle" class="h6">Loading form…</h2></div>';
        modal.show();
        try {
            const response = await fetch(button.dataset.modalUrl, {
                signal: request.signal,
                headers: {'X-Requested-With': 'XMLHttpRequest'}
            });
            if (request.signal.aborted || generation !== loadGeneration) return;
            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }
            if (!response.ok) throw new Error(await response.text());
            const html = await response.text();
            if (request.signal.aborted || generation !== loadGeneration) return;
            content.innerHTML = html;
            parseValidation();
            modal.handleUpdate?.();
            focusForm();
        } catch (error) {
            if (request.signal.aborted || generation !== loadGeneration) return;
            content.innerHTML = '<div class="modal-header"><h2 class="modal-title h5" id="modalTitle">Unable to open form</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>';
            showError('Unable to open this form. Please close it and try again.');
        }
    });
    document.addEventListener('submit', async event => {
        const form = event.target.closest('[data-modal-form]');
        if (!form) return;
        event.preventDefault();
        if (form.dataset.submitting === 'true') return;
        if (window.jQuery?.validator && !$(form).valid()) return;
        form.dataset.submitting = 'true';
        savingForm = form;
        form.setAttribute('aria-busy', 'true');
        const status = form.querySelector('.modal-save-status');
        if (status) status.textContent = 'Saving…';
        const dismiss = [...form.querySelectorAll('[data-bs-dismiss="modal"]')];
        dismiss.forEach(button => button.disabled = true);
        const submit = form.querySelector('button[type="submit"]');
        if (submit) submit.disabled = true;
        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: {'X-Requested-With': 'XMLHttpRequest'}
            });
            if (response.redirected) {
                window.location.assign(response.url);
                return;
            }
            if (response.status === 422) {
                content.innerHTML = await response.text();
                parseValidation();
                modal.handleUpdate?.();
                focusForm();
                return;
            }
            if (!response.ok) throw new Error('Unable to save. The record may have changed; close this form and reload.');
            const result = await response.json();
            if (!result.success) throw new Error('Unable to save.');
            const refresh = await fetch(result.refreshUrl || window.location.href);
            if (refresh.redirected) {
                window.location.assign(refresh.url);
                return;
            }
            if (!refresh.ok) throw new Error('Saved, but the page could not refresh. Reload the page.');
            const html = new DOMParser().parseFromString(await refresh.text(), 'text/html');
            const selector = result.target || '#page-content';
            const updated = html.querySelector(selector), current = document.querySelector(selector);
            if (!updated || !current) {
                window.location.reload();
                return;
            }
            current.replaceWith(updated);
            savingForm = null;
            modal.hide();
        } catch (error) {
            showError(error.message);
        } finally {
            savingForm = null;
            delete form.dataset.submitting;
            form.removeAttribute('aria-busy');
            if (status) status.textContent = '';
            dismiss.forEach(button => button.disabled = false);
            if (submit) submit.disabled = false;
        }
    });
})();
