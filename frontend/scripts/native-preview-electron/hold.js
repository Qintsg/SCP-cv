/** 无输入事件的父死亡探针：只打开本人app租约，读取FLV头后等待被测试编排强杀。 */
void (async () => {
  try {
    const lease = await window.scpCvElectron.preview.acquire({ input: { kind: 'window_1' }, quality: 'high' });
    const response = await fetch(lease.streamUrl), reader = response.body.getReader();
    const header = await reader.read();
    if (header.value?.length !== 13) throw new Error('missing-private-header');
    window.nativePreviewHold = { lease, reader };
    console.log('NATIVE_HOLD_READY');
  } catch { console.log('NATIVE_RESULT:{"success":false,"reason":"hold-stream-failed"}'); }
})();
