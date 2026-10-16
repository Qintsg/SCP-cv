# 真实页面缩放与系统动画偏好补验

日期：2026-10-17。范围：013 T013遗留的本机验收，针对已由014重构的当前Ant界面（代码`fb0b5a9`），不倒填旧Naive界面的历史结果。本次不改应用代码或依赖、不推送或部署，不连接D4。

## 浏览器与安全边界

复用当前`frontend/dist`真实生产资源和本机只读内存API；预览仅监听`127.0.0.1:18795`且无上游代理。新建独立Chromium配置目录，不使用用户浏览器、账号或历史。测试拦截非本机网页网络及所有API写入，预览自身也拒绝写操作；最终审计`write_attempts=0`、`physical_transport=false`。

全主要页面包含：仪表盘、媒体、预设、摄像机与音量、窗口1/2、背景音乐、设置、连接、PPT专注页和访客登录。PPT/摄像机只用夹具，不启动引擎或拉取实际RTSP。危险确认只取消，上传只查看/切Tab，不选择或提交文件。

## 真正的200%页面缩放

使用完整Chromium的`chrome://settings/appearance`公开`Page zoom`下拉控件，从100%选择200%；不注入CSS zoom/transform、不设deviceScaleFactor、不用捏合缩放或单纯缩窄视口替代。

| 独立读数 | 100% | 200% |
| --- | --- | --- |
| 原生窗口outerWidth | 1440 | 1440 |
| CSS innerWidth | 1422 | 711 |
| CSS innerHeight | 904 | 452 |
| devicePixelRatio | 1 | 2 |
| visualViewport.scale | 1 | 1 |
| Chrome设置值 / CDP cssVisualViewport.zoom | 1 | 2 |

保持200%再通过浏览器窗口尺寸接口检查1440×1000及1920×1080两个原生窗口，后者页面为951×492 CSS像素。两种主题、11页面，共44组；页面与主内容横向溢出0、控制台错误/警告0、接口写入0。上传抽屉、页签方向键、Escape/焦点恢复、危险确认取消共8组交互通过。PPT四个动画/翻页按钮至少44 CSS像素且完整位于视口内，没有因低高度隐藏主要操作。

## Windows系统偏好

先通过公开`SPI_GETCLIENTAREAANIMATION`读取原值为开；测试时用`SPI_SETCLIENTAREAANIMATION`临时关闭，仅更新内存、不写用户配置、不广播。新浏览器显式传`reduced_motion='null'`，不模拟媒体特征，也不使用force-reduced-motion启动参数。

- 原始Windows动画：`true`；测试中实际读回：`false`；最终独立读回：`true`，原值已恢复。
- 浏览器在100%和200%下的`matchMedia('(prefers-reduced-motion:reduce)')`均为`true`；CDP页面zoom为2，捕获系统状态为动画关闭。
- 1920窗口、暗色、11页面全部通过，实际按钮transitionDuration为`1e-06s`；额外两组抽屉/键盘/确认取消交互通过，控制台错误/警告及接口写入均0。
- 恢复路径在成功与异常分支均执行；不修改系统安全/隐私设置，不改变用户浏览器缩放或显示器DPI。

系统API参数参照[Microsoft官方说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)；Playwright的`'null'`恢复系统媒体偏好语义见[官方API](https://playwright.dev/python/docs/api/class-browsertype#browser-type-launch-persistent-context-option-reduced-motion)。这些参数说明不替代上述实测读数。

## 补充响应式档位

768、1280、1920三档、明暗两套、11页面，共66组；均无页面/主区横向溢出、控制台错误/警告、外部网页请求或接口写入。连同014已有360/390/1024/1440检查，覆盖013原定360/390/768/1280/1920五档；未用响应式档位代替真实200%缩放。

合计新增121组页面检查和10组交互检查。应用代码未改变，因此555项自动化、Web/App和Electron编译沿用014已验证基线，本次没有冒称重新执行这些门禁。

## 原始证据与测试夹具问题

证据与可丢弃脚本在忽略目录`.validation/ant-console-014/`，不提交用户数据或浏览器配置：

- `accessibility-zoom-final.log`及`accessibility/zoom-1792172488171368600/`：44组、8组交互，完整原生缩放截图和逐页指标。
- `accessibility-responsive-final.log`及`accessibility/responsive-1792172498375655700/`：66组，逐页截图与PPT操作区检查。
- `accessibility-motion2.log`及`accessibility/motion-1792172395276059700/`：11组、2组交互、系统原/中/末值与真实媒体查询。
- 每目录`result.json`保存全部指标、异常、请求与截图文件名；脚本`accessibility-matrix.py`按`--phase zoom|responsive|motion`独立重跑。需预先启动同一只读预览，不可改成现场代理。

初次motion验收失败：Win32读回已关闭，浏览器仍显示no-preference。根因为Playwright未指定选项时默认模拟no-preference；只修自有夹具为字面量`'null'`后重跑通过，未修改生产媒体查询或放宽断言。旧失败目录`motion-1792172272017622600`保留，系统恢复仍通过。

另有两个夹具问题：访客路由的测试Cookie需在下一组交互前清除；网页地址标签应使用实际可访问名。真实zoom为2时，Playwright的fullPage按CSS宽裁切会只留下半张物理视口；改用CDP无clip的原生视口截图，1422×904完整图与711×452 CSS指标对应。没有把测试截图裁切误报为产品横向溢出，也未用图像编辑补齐画面。

## 结论与未改变项

013 T013本机缩放/系统偏好缺口已完成。012人工共享清晰度、DSP/扬声器效果、音源安全恢复选择及暂挂摄像机测试仍不变；本轮不把UI假数据、零写请求或Win32偏好读数当实体效果。D4继续保持原部署，两个远端未推送。

收尾实查Windows动画为`true`，无本轮测试Chrome残留；核对命令与创建时间后关闭自有预览80944及上轮无监听但仍存活的预览42044。原PID均已退出，`netstat`确认18793/18794/18795无监听，不凭助手的stopped文字替代OS证据。
