/** 仪表盘精简与品牌导航静态合同；几何与颜色另经只读浏览器验证。 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { parse } from 'vue/compiler-sfc';
/** :param path: src内相对路径。:returns: 正式代码，不启动设备或浏览器。 */
function source(path) { return readFileSync(new URL(`../src/${path}`,import.meta.url),'utf8'); }
test('仪表盘不重复介绍四窗，框选操作由网格本身标注',()=>{
  const dashboard=source('features/dashboard/DashboardView.vue');
  assert.doesNotMatch(dashboard,/选择窗口，管理当前媒体与播放|console-section-heading/);
  assert.match(dashboard,/<DashboardWindows\s*\/>/);
  const grid=source('features/runtime/wall/WallGridEditor.vue');
  assert.doesNotMatch(grid,/在空白区域拖选矩形/);
  assert.match(grid,/aria-label="自定义大屏，10列5行/);
});
test('顶部完整品牌区提供仪表盘链接，不执行播放或设备命令',()=>{
  const brand=source('layouts/AppBrand.vue'),{descriptor,errors}=parse(brand);
  assert.deepEqual(errors,[]);assert.match(descriptor.template.content,/<RouterLink[^>]*to="\/dashboard"[^>]*aria-label="返回仪表盘"/);
  assert.match(brand,/sspu-ailab-transparent\.png/);
  assert.doesNotMatch(brand,/fetch\(|services\/|stores\/|@click/);
  assert.match(source('layouts/AppShell.vue'),/<AppBrand :collapsed="!isLargeUp"/);
});
test('菜单跟随有效主题，样式保留矮屏滚动和可见焦点',()=>{
  assert.match(source('layouts/AppNavigation.vue'),/:theme="dark \? 'dark' : 'light'"/);
  const css=source('styles/console.css');
  assert.match(css,/--console-nav:#e8eef7/);assert.match(css,/--console-nav:#23364b/);
  assert.match(css,/console-brand:focus-visible/);assert.match(css,/console-navigation[^}]*overflow:auto/s);
  assert.match(css,/max-height:800px/);
});
