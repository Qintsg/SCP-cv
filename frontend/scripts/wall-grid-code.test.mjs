/** 50格布局与无库短码回归，不访问网络/设备。 */
import assert from 'node:assert/strict';
import test, { before, after } from 'node:test';
import { createDomTestServer } from './dom-test-server.mjs';
let server, model, codec;
before(async () => {
  server = await createDomTestServer();
  model = await server.ssrLoadModule('/src/features/runtime/wall/layoutModel.ts');
  codec = await server.ssrLoadModule('/src/features/runtime/wall/layoutCode.ts');
});
after(async () => { await server?.close(); });
const block = (column, row, columns, rows, kind = 'window_1', ip_address = '') =>
  ({ region: 'grid', rect: { column, row, columns, rows }, input: { kind, ip_address } });
test('矩形顺逆选择一致，越界和占用拒绝', () => {
  assert.deepEqual(model.selectionRect(23, 0), { column: 0, row: 0, columns: 4, rows: 3 });
  assert.deepEqual(model.selectionRect(0, 23), model.selectionRect(23, 0));
  assert.throws(() => model.validateBlocks([block(9, 4, 2, 1)]));
  assert.throws(() => model.validateBlocks([block(0, 0, 3, 3), block(2, 2, 2, 2)]));
  assert.doesNotThrow(() => model.validateBlocks([block(0, 0, 5, 5), block(5, 0, 5, 5)]));
});
test('1–50块和IPv4/预留输入完整往返，编辑顺序不影响码', () => {
  for (let count = 0; count <= 50; count++) {
    const value = Array.from({ length: count }, (_, i) => block(i % 10, Math.floor(i / 10), 1, 1,
      ['window_1', 'window_2', 'laptop', 'window_3', 'window_4', 'ip_stream'][i % 6], i % 6 === 5 ? `192.0.2.${i}` : ''));
    const code = codec.encodeLayout(value);
    assert.match(code, /^[A-Za-z0-9]+$/);
    assert.equal(codec.encodeLayout([...value].reverse()), code);
    assert.deepEqual(codec.decodeLayout(code), model.validateBlocks(value));
  }
});
test('损坏、超长、非法字符、越界以及凭据不允许进入代码', () => {
  const good = codec.encodeLayout([block(0, 0, 10, 5)]);
  assert.throws(() => codec.decodeLayout(good.slice(0, -1) + (good.endsWith('A') ? 'B' : 'A')));
  for (const bad of ['', 'a'.repeat(1000), '0!0', '000']) assert.throws(() => codec.decodeLayout(bad));
  assert.throws(() => codec.encodeLayout([block(0, 0, 1, 1, 'ip_stream', 'rtsp://user:secret@192.0.2.1')]));
});
test('简单布局短码长度明确受控，复杂布局不有损缩短', () => {
  assert.ok(codec.encodeLayout([block(0, 0, 10, 5)]).length <= 12);
  assert.ok(codec.encodeLayout([block(0, 0, 5, 5), block(5, 0, 5, 5, 'window_2')]).length <= 17);
});
