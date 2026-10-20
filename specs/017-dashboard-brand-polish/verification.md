# 017 验证记录

日期：2026-10-20。本机开发；无D4连接、推送、部署、系统设置变更或实体操作。

## 需求与结果

- US1／FR-001至003：删除重复介绍行与默认拖选hint；空态footer不渲染。选择范围、覆盖错误、键盘网格、短码和应用门禁保留。2002×990自定义main原scrollHeight978／可见920，修改后不溢出。
- US2／FR-004至006：浅色雾蓝、深色蓝灰；菜单随有效主题。品牌为RouterLink `/dashboard`，包括折叠态，无业务请求。
- US3／FR-007：文档区分D4播放、普通网页视频与016可选本机桥。用户追加指定服务端FLV网关，018承接；017不冒称该新架构已完成。
- FR-008：无computer use，浏览器仅GET／HEAD、截图、只读DOM；网关和设备写请求被夹具拒绝。

## 分层验证

- 回归红灯：新增3条静态合同与1条网格空态断言在修改前失败，4条既有网格行为通过；修改后8/8。
- 完整前端：`node --test --test-concurrency=2 --test-reporter=tap scripts/*.test.mjs`，**1087/1087**，0失败／跳过，172733ms。证据`.validation/console017/frontend-tests.tap`。
- `node --run typecheck`、`build:web`、`build:app`、`build:electron-main`均通过。Vite既有native config扩展名提醒保留，未关闭警告。
- 只读Chromium初次48组、最终48组均0错误／水平溢出：8视口×2主题×3模式。桌面1440×900、1920×1080、2002×990、2560×1260全部main／navigation的scrollHeight不超过clientHeight+1px。
- 补充1366×768、1024×768折叠、390×844手机、1440×500矮屏：内容可滚动而未裁切；侧栏超矮时仍能访问设置。未要求手机或任意矮屏强行一屏。
- 完整截图及几何：`.validation/console017/browser-result.json`、`*-{width}x{height}-{light,dark}.png`；已目视检查2002×990浅深自定义、菜单选中态及品牌。原附件按文字标注作删减参考，不复制浏览器外壳。
- `git diff --check`及Spec Kit验证通过；未改REST／OpenAPI或.NET，未重新运行后端1653项，也未将旧结果当本轮结果。

## Logo资产与图像工具

- 原文件`frontend/src/assets/sspu-ailab.png`及用户原图不变。
- 新资产`frontend/src/assets/sspu-ailab-transparent.png`，使用内置image_gen编辑，未用脚本执行背景删除；3轮中选择第3轮。
- 最终提示摘要：仅外部透明，完整蓝拱门内部及A/B封闭字孔保持白色；蓝框下缘以外、字间白底透明；不增加文字／装饰，保持SSPU#和AILAB形状。第3轮以第2轮作为修正目标，去掉残留的字间白条。
- 检查RGBA：外角alpha=0；蓝框内部及字孔白色alpha≥250，截图可辨认；深色CSS反色不改变alpha。生成资产不是逐像素同一原图，已核对小尺寸字形及两主题的实际呈现。
- 源输出保留在Codex generated_images目录，项目引用独立仓库PNG，不依赖外部路径。

## 一致性复核

12任务与8项FR全部有实现或证据归属；旧016预览仍可选，新018未完成项未混写为完成。窄屏／错误展开保留真实滚动退路，不用overflow:hidden掩盖高度问题。仅本地提交，不改变部署。
