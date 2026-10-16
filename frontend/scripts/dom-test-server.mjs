/*
 * 真实 Vue/Ant Design Vue DOM 回归的共用模块加载器。
 * 保留客户端 render，并隔离 Node 版本的 CJS 推断与开发 WebSocket 副作用。
 */
import { fileURLToPath } from 'node:url';

const frontendRoot = fileURLToPath(new URL('../', import.meta.url));

/** :param key: 浏览器现代按键名。:returns: 同一真实键盘事件应有的旧keyCode，供Ant内部兼容处理。 */
export function browserKeyCode(key) {
  return { ArrowLeft: 37, ArrowUp: 38, ArrowRight: 39, ArrowDown: 40, Enter: 13,
    Escape: 27, ' ': 32, Tab: 9, Home: 36, End: 35 }[key] ?? 0;
}

/** :param text: 原生可见文本。:returns: 忽略Ant为两个汉字插入的展示空格，不改变命令/状态断言。 */
export function visibleText(text) { return (text ?? '').replace(/\s+/g, ''); }
const layoutTracks = new WeakSet();

/**
 * HappyDOM没有排版；Ant按焦点手柄的几何反算值，零尺寸会被误认为0。
 * 只补充公开DOM测量边界，不直接调用组件回调；真实浏览器不用这个辅助函数。
 * :param handle: 公开role=slider手柄。
 * :returns: 有界水平轨道及随真实ARIA值变化的手柄几何。
 */
export function prepareSliderGeometry(handle) {
  const track = handle.closest('.ant-slider');
  if (!track) return;
  if (!layoutTracks.has(track)) {
    if (track.getBoundingClientRect().width !== 0) return;
    track.getBoundingClientRect = () => new window.DOMRect(0, 0, 1000, 20);
    Object.defineProperty(track, 'clientWidth', { configurable: true, get: () => 1000 });
    Object.defineProperty(track, 'offsetWidth', { configurable: true, get: () => 1000 });
    layoutTracks.add(track);
  }
  handle.getBoundingClientRect = () => {
    const low = Number(handle.getAttribute('aria-valuemin'));
    const high = Number(handle.getAttribute('aria-valuemax'));
    const now = Number(handle.getAttribute('aria-valuenow'));
    const position = high === low ? 0 : (now - low) / (high - low) * 1000;
    return new window.DOMRect(position - 5, 0, 10, 20);
  };
}

/**
 * 等待原生Portal/动画挂载完成，避免把两个nextTick当成真实控件已出现。
 * :param predicate: 当前真实DOM条件。
 * :param timeoutMs: 有界等待时间。
 * :returns: 条件满足；超时明确失败。
 */
export async function waitForDom(predicate, timeoutMs = 2000) {
  if (predicate()) return;
  await new Promise((resolve, reject) => {
    const observer = new MutationObserver(() => {
      if (!predicate()) return;
      clearTimeout(timer); observer.disconnect(); resolve();
    });
    const timer = setTimeout(() => {
      observer.disconnect();
      reject(new Error('等待真实控件DOM超时'));
    }, timeoutMs);
    observer.observe(document.body, { subtree: true, childList: true, attributes: true });
  });
}

/**
 * 创建只做模块转换的测试加载器，不监听 HTTP 或 WebSocket 端口。
 * :returns: 可加载真实组件并在结束时关闭的 Vite 服务器。
 */
export async function createDomTestServer() {
  const restoredGlobals = new Map();
  for (const name of ['ShadowRoot', 'HTMLBodyElement', 'HTMLFormElement', 'HTMLButtonElement', 'HTMLAnchorElement', 'MouseEvent', 'KeyboardEvent']) {
    if (globalThis[name] !== undefined || !globalThis.window?.[name]) continue;
    restoredGlobals.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
    Object.defineProperty(globalThis, name, { value: globalThis.window[name], configurable: true, writable: true });
  }
  const [{ createServer }, { compileScript, parse }, ts] = await Promise.all([
    import('vite'), import('vue/compiler-sfc'), import('typescript'),
  ]);
  const server = await createServer({
    configFile: false,
    root: frontendRoot,
    server: { middlewareMode: true, hmr: false, ws: false, watch: null },
    resolve: { alias: { '@': `${frontendRoot.replaceAll('\\', '/')}/src` } },
    optimizeDeps: { noDiscovery: true, include: [] },
    // CJS 重导出在 Node 24.13 无完整命名空间；客户端组件测试由 Vite 加载真实 ESM。
    // Ant与图标使用真实客户端ESM；Vue/Pinia保持外部单实例。
    ssr: { noExternal: ['ant-design-vue', '@ant-design/icons-vue', '@ant-design/icons-svg', '@ant-design/colors', '@ant-design/cssinjs', '@vicons/fluent'], resolve: { conditions: ['browser', 'module', 'development'] } },
    plugins: [{
      name: 'test-client-sfc',
      /**
       * 编译客户端 render，保证测试触发真实原生输入和菜单事件。
       * :param source: Vue 单文件组件源码。
       * :param filename: 当前组件的绝对路径。
       * :returns: 客户端 JavaScript；非 Vue 文件保持原样。
       */
      transform(source, filename) {
        if (!filename.endsWith('.vue')) return;
        const { descriptor } = parse(source, { filename });
        const script = compileScript(descriptor, { id: filename, inlineTemplate: true });
        return ts.default.transpileModule(script.content, {
          compilerOptions: { target: ts.default.ScriptTarget.ESNext, module: ts.default.ModuleKind.ESNext },
        }).outputText;
      },
    }],
  });
  const close = server.close.bind(server);
  server.close = async () => {
    await close();
    for (const [name, descriptor] of restoredGlobals) {
      if (descriptor) Object.defineProperty(globalThis, name, descriptor);
      else delete globalThis[name];
    }
  };
  return server;
}
