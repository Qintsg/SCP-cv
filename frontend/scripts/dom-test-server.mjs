/*
 * 真实 Vue/Naive UI DOM 回归的共用模块加载器。
 * 保留客户端 render，并隔离 Node 版本的 CJS 推断与开发 WebSocket 副作用。
 */
import { fileURLToPath } from 'node:url';

const frontendRoot = fileURLToPath(new URL('../', import.meta.url));

/**
 * 创建只做模块转换的测试加载器，不监听 HTTP 或 WebSocket 端口。
 * :returns: 可加载真实组件并在结束时关闭的 Vite 服务器。
 */
export async function createDomTestServer() {
  const [{ createServer }, { compileScript, parse }, ts] = await Promise.all([
    import('vite'), import('vue/compiler-sfc'), import('typescript'),
  ]);
  return createServer({
    configFile: false,
    root: frontendRoot,
    server: { middlewareMode: true, hmr: false, ws: false, watch: null },
    resolve: { alias: { '@': `${frontendRoot.replaceAll('\\', '/')}/src` } },
    optimizeDeps: { noDiscovery: true, include: [] },
    // 这两个包的 CJS 重导出在 Node 24.13 无完整命名空间；让 Vite 解析其真实 ESM 入口。
    ssr: { noExternal: ['naive-ui', 'vueuc'] },
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
}
