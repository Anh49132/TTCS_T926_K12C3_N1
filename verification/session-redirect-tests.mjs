import vm from 'node:vm';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';

export async function run(root) {
    const source = await fs.readFile(root + '/QL_PhongTro/wwwroot/js/auth-interceptor.js', 'utf8');
    const polling = await fs.readFile(root + '/QL_PhongTro/wwwroot/js/session-status.js', 'utf8');
    let passed = 0;
    const tick = () => new Promise(resolve => setTimeout(resolve, 0));
    function harness(fetch, tokens = {}) {
        const redirects = [], events = {}, storage = new Map(Object.entries(tokens));
        const context = {
            fetch, localStorage: { getItem: k => storage.get(k) ?? null, setItem: (k,v) => storage.set(k,v), removeItem: k => storage.delete(k) },
            location: { pathname: '/CongNo', replace: x => redirects.push(x) },
            document: { readyState: 'complete' }, console: { log() {}, warn() {} },
            atob: x => Buffer.from(x, 'base64').toString(), Event: class {},
            setInterval: fn => { events.poll = fn; },
            addEventListener: (name,fn) => { events[name] = fn; }, dispatchEvent() {}
        };
        context.window = context;
        return { context: vm.createContext(context), redirects, events, storage };
    }
    const reply = (status, body) => ({ status, ok: status >= 200 && status < 300, json: async () => body });
    for (const [status, body, shouldRedirect] of [[500,{},false],[503,{},false],[403,{},false],[200,{},false],[200,{authenticated:true},false],[200,{authenticated:false},true],[401,{},true]]) {
        const h = harness(async () => reply(status, body));
        vm.runInContext(polling,h.context); await h.events.poll();
        assert.equal(h.redirects.length, shouldRedirect ? 1 : 0); passed++;
    }
    let release, calls = 0;
    const h = harness(() => { calls++; return new Promise(resolve => { release = resolve; }); });
    vm.runInContext(polling,h.context);
    const pending = h.events.poll(); await h.events.poll(); assert.equal(calls,1);
    release(reply(200,{authenticated:true})); await pending; passed++;
    const expired = 'header.' + Buffer.from(JSON.stringify({exp:1})).toString('base64') + '.signature';
    for (const [refreshStatus, cookie, redirects, retain] of [[401,true,0,false],[401,false,1,false],[503,true,0,true]]) {
        const h = harness(async url => url === '/api/auth/refresh' ? reply(refreshStatus,{}) : reply(200,{authenticated:cookie}), {accessToken:expired,refreshToken:'old-token'});
        vm.runInContext(source,h.context); await tick(); await tick();
        assert.equal(h.redirects.length,redirects);
        assert.equal(h.storage.has('refreshToken'),retain); passed++;
    }
    const offline = harness(async () => { throw new Error('offline'); },{accessToken:expired,refreshToken:'old-token'});
    vm.runInContext(source,offline.context); await tick();
    assert.equal(offline.redirects.length,0);assert.equal(offline.storage.has('refreshToken'),true);passed++;
    // A 401 on an API must not send a valid MVC cookie session to Login.
    const valid = 'header.' + Buffer.from(JSON.stringify({exp:Date.now()/1000+3600})).toString('base64') + '.signature';
    const api = harness(async url => url === '/Account/SessionStatus' ? reply(200,{authenticated:true}) : reply(401,{}),{accessToken:valid,refreshToken:'old-token'});
    vm.runInContext(source,api.context);
    await assert.rejects(api.context.fetch('/api/example'));
    assert.equal(api.redirects.length,0);passed++;
    return {passed};
}
