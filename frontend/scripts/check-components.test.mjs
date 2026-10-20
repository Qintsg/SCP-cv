// 使用与 Vite 相同的 Vue 编译器捕获模板中遗漏导入的组件。
/** 组件显式注册与模板静态合同回归，真实事件另由DOM及浏览器验证。 */
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { compileScript, compileTemplate, parse } from 'vue/compiler-sfc';

const sourceRoot = fileURLToPath(new URL('../src/', import.meta.url));

function vueFiles(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? vueFiles(path) : path.endsWith('.vue') ? [path] : [];
  });
}

test('所有 Ant Design Vue 模板组件必须显式注册，不能遗留旧组件库', () => {
  const unresolved = [];
  for (const filename of vueFiles(sourceRoot)) {
    const { descriptor, errors } = parse(readFileSync(filename, 'utf8'), { filename });
    assert.deepEqual(errors, [], filename);
    if (!descriptor.template || !descriptor.scriptSetup) continue;
    const script = compileScript(descriptor, { id: filename });
    const result = compileTemplate({
      source: descriptor.template.content,
      filename,
      id: filename,
      compilerOptions: { bindingMetadata: script.bindings },
    });
    assert.deepEqual(result.errors, [], filename);
    assert.doesNotMatch(readFileSync(filename, 'utf8'), /from ['"]naive-ui['"]|<n-[a-z]/, filename);
    for (const match of result.code.matchAll(/_resolveComponent\("(a-[^"]+|A[A-Z][^"]*|ActionMenu)"\)/g)) {
      unresolved.push(`${relative(sourceRoot, filename)}: ${match[1]}`);
    }
  }
  assert.deepEqual(unresolved, [], `未注册组件：\n${unresolved.join('\n')}`);
});

test('移动底栏在路由动作之前阻止原生导航，更多只打开抽屉', () => {
  const filename = join(sourceRoot, 'layouts/AppNavigation.vue');
  const { descriptor } = parse(readFileSync(filename, 'utf8'), { filename });
  const bottom = descriptor.template.content.match(/<nav\s+v-else\s+class="app-shell__bottom"[\s\S]*?<\/nav>/)?.[0];
  assert.ok(bottom, '必须存在移动底栏');
  assert.doesNotMatch(bottom, /<RouterLink\b/, '自动导航的 RouterLink 会先访问不存在的 /more');
  assert.match(bottom, /@click\.prevent=/, '导航必须由底栏 handler 显式负责');
});

test('侧边抽屉宽度不能大于手机视口', () => {
  for (const name of ['sources/AddSourceDrawer', 'sources/EditSourceDrawer', 'scenarios/ScenarioEditDrawer', 'scenarios/ScenarioPreviewDrawer']) {
    const source = readFileSync(join(sourceRoot, `features/${name}.vue`), 'utf8');
    const drawer = source.match(/<ControlDrawer\s[\s\S]*?>/)?.[0];
    assert.match(drawer, /width="min\(\d+px, 100vw\)"/, name);
  }
});

test('空闲窗口只展示一次操作提示', () => {
  const filename = join(sourceRoot, 'features/display/PlaybackControl.vue');
  const { descriptor } = parse(readFileSync(filename, 'utf8'), { filename });
  assert.equal([...descriptor.template.content.matchAll(/t\('playback\.noSource'\)/g)].length, 1);
});

test('媒体列表溢出时可横向滚动，导航不会被表格挤窄', () => {
  const sources = readFileSync(join(sourceRoot, 'features/sources/SourcesView.vue'), 'utf8');
  assert.match(sources, /overflowX: 'auto'/);
  assert.match(sources, /<a-table[\s\S]*?:scroll="\{ x: 800 \}"/);
  const shell = readFileSync(join(sourceRoot, 'layouts/AppShell.vue'), 'utf8');
  assert.match(shell, /<a-layout-sider[\s\S]*?:width="228"/);
});

test('共享壳层使用Ant分组侧栏和任务工作区，手机保留安全底栏', () => {
  const shell = readFileSync(join(sourceRoot, 'layouts/AppShell.vue'), 'utf8');
  const navigation = readFileSync(join(sourceRoot, 'layouts/AppNavigation.vue'), 'utf8');
  const topBar = readFileSync(join(sourceRoot, 'layouts/AppTopBar.vue'), 'utf8');
  const stateCss = readFileSync(join(sourceRoot, 'styles/console.css'), 'utf8');

  assert.match(shell, /<a-layout[\s\S]*?console-shell/);
  assert.match(navigation, /现场操作/);
  assert.match(navigation, /内容资源/);
  assert.match(navigation, /<a-menu/);
  assert.match(stateCss, /grid-template-columns:repeat\(5/);
  assert.match(stateCss, /safe-area-inset-bottom/);
  assert.match(topBar, /console-context/);
  assert.match(topBar, /app-shell__bar-meta[^"\n]*flex-wrap/, '窄屏状态标签不能把主题及应急入口挤出视口');
  assert.match(stateCss, /console-surface/);
  assert.match(stateCss, /prefers-reduced-motion/);
});

test('窗口音量与静音只对后端声明支持的源类型开放', async () => {
  const { supportsWindowAudioControls } = await import('../src/features/display/playbackCapabilities.ts');

  for (const sourceType of ['video', 'custom_stream', 'rtsp_stream', 'srt_stream']) {
    assert.equal(supportsWindowAudioControls(sourceType), true, sourceType);
  }
  for (const sourceType of ['', 'ppt', 'audio', 'image', 'web', 'unknown']) {
    assert.equal(supportsWindowAudioControls(sourceType), false, sourceType || 'empty');
  }
});

test('PPT 资源过期请求不能覆盖当前源', async () => {
  const { isCurrentPptResourceRequest } = await import('../src/features/display/pptResourceRequest.ts');

  assert.equal(isCurrentPptResourceRequest(2, 2, 20, 20, true), true);
  assert.equal(isCurrentPptResourceRequest(1, 2, 10, 20, true), false);
  assert.equal(isCurrentPptResourceRequest(1, 3, 10, 10, true), false, 'A→B→A 也必须按序号拒绝旧 A');
  assert.equal(isCurrentPptResourceRequest(2, 2, 20, 20, false), false);
});
