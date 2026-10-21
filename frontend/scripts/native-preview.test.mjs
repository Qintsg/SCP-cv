/** 原生预览默认回归：仅内存描述符/字节流，不启动设备、FFmpeg或Electron。 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { PassThrough } from 'node:stream';
import { readFileSync, existsSync } from 'node:fs';
import { registerHooks } from 'node:module';
import ts from 'typescript';

// Electron正式使用编译JS；默认测试只读转译同一源码，不依赖遗留electron-dist产物。
const hooks = registerHooks({
  resolve(specifier, context, next) {
    if (specifier.endsWith('.js') && context.parentURL?.includes('/electron/preview/')) {
      const candidate = new URL(specifier.replace(/\.js$/, '.ts'), context.parentURL);
      if (existsSync(candidate)) return { url: candidate.href, shortCircuit: true };
    }
    return next(specifier, context);
  },
  load(url, context, next) {
    if (url.includes('/electron/preview/') && url.endsWith('.ts')) return { format: 'module', shortCircuit: true,
      source: ts.transpileModule(readFileSync(new URL(url), 'utf8'), { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText };
    return next(url, context);
  },
});
const { NativePreviewRegistry } = await import('../electron/preview/registry.ts');
const { validateNativeDescriptor, validateNativeRequest } = await import('../electron/preview/contracts.ts');
hooks.deregister();

/** :returns: 测试自己的内存生产者和当前认证围栏。 */
function fixture() {
  let owner = 'main:cookie-a', count = 0, stops = 0, now = Date.now();
  const key = 'a'.repeat(64), uris = new Map();
  const registry = new NativePreviewRegistry({ owner: async () => owner, now: () => now,
    resolve: async request => ({ uri: uris.get(request.source_id) ?? 'rtmp://127.0.0.1:19350/test', sharing_key: key,
      source_id: request.source_id ?? null, source_revision: 1, expires_at: new Date(now + 30000).toISOString(), quality: request.quality }),
    produce() { count++; const output = new PassThrough(); return { output, completed: Promise.resolve(), async stop() { stops++; output.end(); } }; },
  });
  return { registry, uris, key, counts: () => ({ count, stops }), setOwner: value => { owner = value; }, advance: value => { now += value; } };
}
test('native输入不接受原始地址、程序参数、跨网段和混合来源', () => {
  for (const input of [{ uri: 'rtsp://localhost/', quality: 'low' }, { source_id: 1, quality: 'low', argv: [] },
    { source_id: 1, input: { kind: 'window_1' }, quality: 'low' }, { input: { kind: 'ip_stream', ip_address: '127.0.0.1' }, quality: 'low' }]) assert.throws(() => validateNativeRequest(input));
  assert.deepEqual(validateNativeRequest({ input: { kind: 'window_4' }, quality: 'high' }), { input: { kind: 'window_4' }, quality: 'high' });
  assert.throws(() => validateNativeDescriptor({ uri: 'file:///secret', sharing_key: 'a'.repeat(64) }));
});
test('acquire不拉流，多租约同源GET仅启动一个进程且按引用释放', async () => {
  const fixtureValue = fixture(), { registry } = fixtureValue;
  try {
    const one = await registry.acquire({ source_id: 1, quality: 'low' }), two = await registry.acquire({ source_id: 2, quality: 'low' });
    assert.equal(fixtureValue.counts().count, 0); assert.equal(one.sharingKey, two.sharingKey);
    const responseOne = await registry.stream(new Request(one.streamUrl)), responseTwo = await registry.stream(new Request(two.streamUrl));
    assert.equal(responseOne.status, 200); assert.equal(responseTwo.status, 200); assert.equal(fixtureValue.counts().count, 1);
    await registry.release(one.id); assert.equal(fixtureValue.counts().stops, 0);
    await registry.release(two.id); assert.equal(fixtureValue.counts().stops, 1);
  } finally { await registry.dispose(); }
});
test('完整票据改变、伪造nonce、重复GET均拒绝，旧票据不能复活新账号', async () => {
  const own = fixture();
  try {
    const lease = await own.registry.acquire({ source_id: 1, quality: 'low' });
    assert.equal((await own.registry.stream(new Request(lease.streamUrl))).status, 200);
    assert.equal((await own.registry.stream(new Request(lease.streamUrl))).status, 403);
    assert.equal((await own.registry.stream(new Request('app://scp-cv/__preview/' + '0'.repeat(64) + '/stream.flv'))).status, 403);
    own.setOwner('main:cookie-b');
    await assert.rejects(own.registry.heartbeat(lease.id), /stale_owner/);
    assert.equal((await own.registry.stream(new Request(lease.streamUrl))).status, 403);
    await own.registry.invalidate(); own.setOwner('main:cookie-a');
    await assert.rejects(own.registry.heartbeat(lease.id), /stale_owner/);
  } finally { await own.registry.dispose(); }
});
test('原码兼容切换先确认退出；同租约URL可重新读取且只能回退一次', async () => {
  const own = fixture();
  try {
    const lease = await own.registry.acquire({ source_id: 1, quality: 'high' });
    await own.registry.stream(new Request(lease.streamUrl));
    await own.registry.fallback(lease.id); assert.deepEqual(own.counts(), { count: 1, stops: 1 });
    assert.equal((await own.registry.stream(new Request(lease.streamUrl))).status, 200);
    assert.equal(own.counts().count, 2);
    await assert.rejects(own.registry.fallback(lease.id), /fallback_unavailable/);
  } finally { await own.registry.dispose(); }
});
test('消费源被改后只允许仍同URI且复核有效的alias维持；跨owner/新URI不能偷续旧源', async () => {
  const own = fixture();
  try {
    const stream = await own.registry.acquire({ source_id: 1, quality: 'low' }), alias = await own.registry.acquire({ source_id: 2, quality: 'low' });
    own.uris.set(1, 'rtmp://127.0.0.1:19350/changed');
    await own.registry.heartbeat(stream.id, [alias.id]);
    own.uris.set(2, 'rtmp://127.0.0.1:19350/changed');
    await assert.rejects(own.registry.heartbeat(stream.id, [alias.id]), /source_changed/);
  } finally { await own.registry.dispose(); }
});
test('迟到描述符在invalidate后不能创建租约或启动进程', async () => {
  let resolveDescriptor;
  const registry = new NativePreviewRegistry({ owner: async () => 'current',
    resolve: async () => new Promise(resolve => { resolveDescriptor = resolve; }), produce() { throw new Error('不得启动'); } });
  try {
    const pending = registry.acquire({ source_id: 1, quality: 'low' });
    while (!resolveDescriptor) await new Promise(resolve => setImmediate(resolve));
    await registry.invalidate();
    resolveDescriptor({ uri: 'rtmp://127.0.0.1/test', sharing_key: 'a'.repeat(64), source_id: 1, source_revision: 0, quality: 'low', expires_at: new Date(Date.now() + 30000).toISOString() });
    await assert.rejects(pending, /stale_owner/);
  } finally { await registry.dispose(); }
});
test('native租约过期拒绝使用，不向renderer返回凭据URI', async () => {
  const own = fixture();
  try {
    own.uris.set(1, 'rtsp://test-user:test-pass@127.0.0.1:8554/live');
    const lease = await own.registry.acquire({ source_id: 1, quality: 'low' });
    assert.doesNotMatch(JSON.stringify(lease), /test-user|test-pass|rtsp:/);
    own.advance(30001); await assert.rejects(own.registry.heartbeat(lease.id), /stale_owner/);
  } finally { await own.registry.dispose(); }
});
test('描述符完成后的Cookie复核本身发生invalidate时不能创建迟到租约', async () => {
  let ownerReads = 0, resolveOwner;
  const registry = new NativePreviewRegistry({ owner: async () => ++ownerReads === 1 ? 'same-owner' : new Promise(resolve => { resolveOwner = resolve; }),
    resolve: async () => ({ uri: 'rtmp://127.0.0.1/test', sharing_key: 'a'.repeat(64), source_id: 1, source_revision: 0, quality: 'low', expires_at: new Date(Date.now() + 30000).toISOString() }),
    produce() { throw new Error('不得启动'); } });
  try {
    const pending = registry.acquire({ source_id: 1, quality: 'low' });
    while (!resolveOwner) await new Promise(resolve => setImmediate(resolve));
    await registry.invalidate(); resolveOwner('same-owner');
    await assert.rejects(pending, /stale_owner/);
  } finally { await registry.dispose(); }
});
test('129并发acquire包含在途描述符容量，最多128份租约且不启动上游', async () => {
  const resolvers = [];
  const registry = new NativePreviewRegistry({ owner: async () => 'current',
    resolve: request => new Promise(resolve => { resolvers.push(() => resolve({ uri: 'rtmp://127.0.0.1/test', sharing_key: 'a'.repeat(64), source_id: request.source_id,
      source_revision: 0, quality: 'low', expires_at: new Date(Date.now() + 30000).toISOString() })); }), produce() { throw new Error('不应启动'); } });
  try {
    const requests = Array.from({ length: 129 }, (_, index) => registry.acquire({ source_id: index + 1, quality: 'low' }).then(value => ({ value }), error => ({ error })));
    while (resolvers.length < 128) await new Promise(resolve => setImmediate(resolve));
    for (const resolve of resolvers) resolve();
    const results = await Promise.all(requests);
    assert.equal(results.filter(item => item.value).length, 128);
    assert.equal(results.filter(item => item.error?.code === 'native_preview_capacity').length, 1);
    assert.equal(new Set(results.filter(item => item.value).map(item => item.value.id)).size, 128);
  } finally { await registry.dispose(); }
});
test('已取消协议GET不创建媒体进程', async () => {
  const own = fixture();
  try {
    const lease = await own.registry.acquire({ source_id: 1, quality: 'low' }), abort = new AbortController(); abort.abort();
    assert.equal((await own.registry.stream(new Request(lease.streamUrl, { signal: abort.signal }))).status, 403);
    assert.equal(own.counts().count, 0);
  } finally { await own.registry.dispose(); }
});
