/** 共享网页构建与同源代理；TLS配置仅在服务器读取，构建不读取私钥。SCP-cv 2026-10-11。 */
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, loadEnv } from 'vite';
import { loadFrontendHttps } from './config/https';
import { accessPort, backendTargets, dualProtocolPlugin, protocolProxy } from './config/dualProtocol';

/**
 * Vite 构建配置。
 * 设计要点：
 * 1. envDir 固定指向 frontend 目录本身，使前端拥有独立的 `.env` 文件，避免与
 *    ControlHost/MediaMTX 等后端配置混用。
 * 2. dev 端口仍可通过 `VITE_FRONTEND_PORT` 显式覆盖，便于多实例并行。
 * 3. 别名 `@` 指向 `src`，与 tsconfig.json 的 paths 一致，让组件库与业务模块
 *    使用相同的导入语法。
 * 4. dev server 把 `/api`、`/events`、`/media`、`/static`、`/admin` 反向代理到
 *    ControlHost：让前端与后端共享同一个 origin（http://<host>:5173），从根本上
 *    避免跨 origin + SameSite cookie 导致 csrftoken 不随登录请求发送的问题。
 */
export default defineConfig(({ mode, command }) => {
  const isPackagedApp = mode === 'app';
  const envDir = fileURLToPath(new URL('.', import.meta.url));
  const env = loadEnv(mode, envDir, '');
  const https = command === 'serve' && !isPackagedApp ? loadFrontendHttps(env, envDir) : undefined;
  const frontendPort = accessPort(env.VITE_FRONTEND_PORT, 5173);
  const httpsPort = accessPort(env.SCP_CV_HTTPS_PORT, 5174);

  // 共用代理规则。
  //   - changeOrigin=false：保留浏览器原始 Host 头，让 ControlHost 的 CSRF Origin 校验
  //     拿到与 Origin 头一致的 host（同 origin 验证天然通过），否则 Host 被改为
  //     backend host 后 ControlHost 计算的可信 origin 与浏览器 Origin 不匹配 → 403。
  //   - 每请求选择同协议上游，HTTPS始终正常校验证书，不降级或剥离Secure Cookie。
  const proxyRule = protocolProxy(backendTargets(env));

  return {
    // Electron 与 Capacitor 都消费相对路径的本地资源构建；网页继续部署在站点根路径。
    base: isPackagedApp ? './' : '/',
    envDir,
    plugins: [vue(), tailwindcss(), ...(!isPackagedApp && command === 'serve' ? [dualProtocolPlugin(https, httpsPort)] : [])],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      host: '0.0.0.0',
      port: frontendPort,
      strictPort: true,
      // API跨站策略只由ControlHost精确配置；Vite默认CORS会先吞OPTIONS，使Electron登录预检缺凭据许可。
      cors: false,
      fs: { deny: ['.env', '.env.*', '*.{crt,pem,key}', '**/.git/**'] },
      proxy: {
        '/api': proxyRule,
        // events 是历史 dashboard SSE 路径，与 /api/events/ 共存；统一代理。
        '/events': proxyRule,
        '/media': proxyRule,
        '/static': proxyRule,
        '/admin': proxyRule,
      },
    },
    preview: {
      host: '0.0.0.0',
      port: frontendPort,
      strictPort: true,
      cors: false,
      proxy: {
        '/api': proxyRule,
        '/events': proxyRule,
        '/media': proxyRule,
        '/static': proxyRule,
        '/admin': proxyRule,
      },
    },
    build: {
      outDir: isPackagedApp ? 'dist-app' : 'dist',
      emptyOutDir: true,
    },
  };
});
