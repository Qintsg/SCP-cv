# 016 工作区设计验收

final result: passed

日期：2026-10-20。范围：用户十二张标注图对应的本机前端改版。不是D4实体画面确认，也不是现场直播源验收。

## 对照与证据

- source visual truth path：用户提供的`C:/Users/qintsg/AppData/Local/Temp/codex-clipboard-*.png`十二张附件；具体映射在`.validation/console016/compare.py`，Excel两张仅表达矩形框选/合并交互，不复制Excel应用外壳。
- implementation screenshot path：`.validation/console016/{dashboard,sources,scenarios,audio,settings,dev,edit,custom,advanced}-{1440,2560,3840,390}.png`以及`match-*.png`。
- viewport：1440×1000、2560×1260、3840×2160、390×844，DPR=1；另2002×990、DPR=1.25以对照附件密度，共45组。
- density normalization：带浏览器外壳的参考原图2560×1380，裁掉外壳后约2502×1238；实现匹配截图2503×1238。参考DPR=1.25由原侧栏228CSS px对应约285物理px推断，不当作用户提供的测量事实。首轮不同密度不直接比较字号。
- 媒体参考是2188×1221的裁切，匹配实现主内容2218×1150；标题栏/裁切差异排除。自定义与高级区域采用实际组件元素截图，不把页面背景差异当缺陷。
- full-view comparison evidence：`.validation/console016/compare-{media,dashboard,audio,scenarios,edit,settings,dev}.png`；参考与实现置于同一比较输入，使用共同缩放比例，不分别放大制造相似性。
- focused comparison evidence：`compare-custom.png`、`compare-advanced.png`、`match-dashboard-wall-focus.png`、`match-custom-wall-focus.png`、`match-edit-scenario-windows-focus.png`；源图3/5/6与实际网格还独立同输入核对。
- state：虚构的两窗播放、4卡含2预留、目录/媒体及预案列表、音乐空态、设置维护、静态依赖、创建抽屉、矩形显示块和高级机位/音量。无本机桥令牌时显示明确未启用；未配置预览流为普通空态。

## Findings 与修复历史

1. [P1/P2，已修复] 初版Logo未限尺寸覆盖品牌文字；旧Slider扩大高度使滑块离轨，开关被grid拉至132px，Empty图标占位错开。`before-dashboard-1440.png`、`before-audio-1440.png`保留初始证据。限制原PNG为40px contain、恢复Ant几何、开关起点对齐、修空态和图标按钮；最终开关44×22、滑轨/滑块居中，无裁切。
2. [P2，已修复] 四预览卡和32:9区域叠加后，附件密度下应用按钮top=997、超出990CSS px视口。卡片底部合行、模式/来源并排、保留画面比例，快捷布局整页无新增滚动；自定义应用入口粘底。后验按钮位于可见范围，提示文字不被不透明操作条覆盖。
3. [P2，已修复] 手机Radio默认baseline与44px目标高度叠加，圆点/文字中心差11px。统一align-items:center；脚本逐一断言可见Radio中心偏差<2px，45组通过。
4. [P2，已修复] 开发诊断先改宽卡后仍是过长单列。依赖改两列明细，其余API/端口/日志分区，手机单列，不再四个窄长列。
5. [功能回归，已修复] 编辑器首帧open=true未恢复已有预案，误走POST。真实组件红绿测试后，首次加载即初始化、保存PATCH并保留0音量/高级值；关闭或重登后的迟到结果不弹旧预览。它不是通过浏览器模拟用户输入验收的。

最终对照未发现剩余P0/P1/P2视觉项。

## 必须检查的五类表面

- 字体/排版：沿用Segoe UI/微软雅黑与Consolas数据字体，标题24px、正文14px，手机输入16px；长名称截断/换行不挤压操作，Radio与图标垂直对齐。
- 间距/布局：Ant原有边距与令牌统一；文件夹/文件同表、播放状态独立列，预案同类结构；32:9画面与10×5网格实测。连续拖选画布不是50个独立实体命令按钮；选源、菜单及主要手机动作保留44px目标和键盘替代。
- 颜色/令牌：蓝#1677ff、画布#f3f6fb、白表面、边框#e2e8f0、侧栏#111c32；状态有文字，不只依赖颜色。
- 图片/图标：使用原`SSPU AILAB1.png`，源/仓库SHA256均为`2D7984BDEEFD82A4F9F07FCE1B4B61256082F00D0DE3E8FF01E771839E96D933`；未重绘Logo或生成替代资产，图标使用原Fluent库。删除音乐装饰性波形/唱片以让出操作空间。
- 文案/内容：标红副标题、路径行、重复目录卡、旧布局选项/发送详情已移除。窗口3/4、未启用预览、无流和失败各自明确，未宣称实体画面已确认。

## 交互、控制台与边界

- 用户禁用computer use，并明确允许只读无头浏览器；因此未用CUA/IAB、鼠标、键盘自动输入或伪造真人手势。Chromium只做GET、DOM/几何读取、截图；外部地址和写请求被夹具拒绝。
- 45组pageerror/console error为0，页面无水平溢出；背景音乐在三档桌面高度均为内容高度=滚动高度，手机自然滚动；模态与底栏主操作可见。
- 主流程交互使用正式Vue/Ant组件的HappyDOM事件：框选/重叠拒绝/选源/删除、短码输入/更新、Shift方向键、编辑PATCH、取消/重登围栏、目录/批量和已有控制回归。不把内存交互当作浏览器真人操作。
- 预览真实解码另经本机MediaMTX/FFmpeg RTSP/TCP与RTMP验证；本页静态未启用态不替代HTTPS到loopback的权限、真实摄像机或现场4K出画。
- 开发Tab为独立内容夹具；完整设置页另验了页签/布局。背景夹具与附件不相同、空媒体封面/诊断数据不同，均不是源设计漂移。

## Implementation Checklist

- [x] 全视图及聚焦区域与附件同输入比较
- [x] 已修复P0/P1/P2并重新捕获
- [x] 字体、间距、颜色、资产、文案五类检查
- [x] 桌面/手机/4K宽度、主要控制与错误空态检查
- [x] 图像/浏览器/交互的证据边界明确

## Follow-up Polish

未把真实现场流、任意余数裁切、D4系统改分辨率或封装客户端原生预览IPC列为本轮已验证；这些是明确范围边界，不是用占位图冒充已完成。用户需要看外观时保留127.0.0.1:18894的只读本机夹具，不作为实际设备控制入口。
