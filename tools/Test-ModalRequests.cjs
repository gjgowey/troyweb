// Run: node tools/Test-ModalRequests.cjs
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const documentEvents = {}, modalEvents = {}, requests = [];
const content = { innerHTML: '', querySelector: () => null, prepend() {} };
const element = { addEventListener: (name, fn) => modalEvents[name] = fn };
const document = {
    getElementById: id => id === 'editorModal' ? element : content,
    addEventListener: (name, fn) => documentEvents[name] = fn,
    createElement: () => ({ setAttribute() {} })
};
const scope = {
    document, AbortController,
    bootstrap: { Modal: { getOrCreateInstance: () => ({ show() {}, hide() {} }) } },
    window: { location: { assign() {}, reload() {} } },
    FormData: class {},
    fetch: (url, options) => new Promise(resolve => requests.push({ url, options, resolve }))
};
vm.runInNewContext(readFileSync(join(__dirname, '../TroyWebProperty.Web/wwwroot/js/site.js'), 'utf8'), scope);
const click = url => documentEvents.click({ target: { closest: () => ({ dataset: { modalUrl: url } }) }, preventDefault() {} });
const ok = html => ({ ok: true, text: async () => html });
(async () => {
    const first = click('/first');
    const second = click('/second');
    assert.equal(requests[0].options.signal.aborted, true);
    requests[1].resolve(ok('new form')); await second;
    requests[0].resolve(ok('stale form')); await first;
    assert.equal(content.innerHTML, 'new form');
    const third = click('/third');
    modalEvents['hide.bs.modal']();
    assert.equal(requests[2].options.signal.aborted, true);
    requests[2].resolve(ok('closed form')); await third;
    assert.notEqual(content.innerHTML, 'closed form');
    const button = { disabled: false };
    const attributes = {};
    const status = { textContent: '' };
    const close = { disabled: false };
    const form = { dataset: {}, action: '/save',
        setAttribute: (name, value) => attributes[name] = value,
        removeAttribute: name => delete attributes[name],
        querySelector: selector => selector === '.modal-save-status' ? status : button,
        querySelectorAll: () => [close] };
    const submitEvent = { target: { closest: () => form }, preventDefault() {} };
    const saving = documentEvents.submit(submitEvent);
    assert.equal(attributes['aria-busy'], 'true');
    assert.equal(status.textContent, 'Saving…');
    assert.equal(close.disabled, true);
    let prevented = false;
    modalEvents['hide.bs.modal']({ preventDefault: () => prevented = true });
    assert.equal(prevented, true);
    await documentEvents.submit(submitEvent);
    assert.equal(requests.filter(x => x.options.method === 'POST').length, 1);
    requests[3].resolve({ redirected: true, url: '/done' }); await saving;
    assert.equal(button.disabled, false);
    assert.equal(close.disabled, false);
    assert.equal(attributes['aria-busy'], undefined);
    assert.equal(status.textContent, '');
    assert.equal(form.dataset.submitting, undefined);
    const failed = documentEvents.submit(submitEvent);
    requests[4].resolve({ ok: false }); await failed;
    assert.equal(button.disabled, false);
    assert.equal(close.disabled, false);
    assert.equal(form.dataset.submitting, undefined);
    const invalid = documentEvents.submit(submitEvent);
    requests[5].resolve({ status: 422, text: async () => 'validation errors' }); await invalid;
    assert.equal(content.innerHTML, 'validation errors');
    assert.equal(close.disabled, false);
    assert.equal(form.dataset.submitting, undefined);
    console.log('PASS: stale GET ignored; closed GET aborted; duplicate POST suppressed; submit state restored.');
})().catch(error => { console.error(error); process.exitCode = 1; });
