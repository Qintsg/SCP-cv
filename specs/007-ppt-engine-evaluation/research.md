# 初始研究与验证选择

- Decision：先评估，不修改正式默认PPT页图路径。
- Rationale：内容兼容和退出高风险，D4任务仍暂停。
- Alternatives considered：付费库排除；图片/PDF不是动画播放证据。

## 已核对边界

- pptx-viewer声明动画/媒体，也明确效果降级及编码限制，须实测。
- LibreOffice有放映/动画步进接口，但.NET绑定与承载不是现成控件。
- 本机已有LibreOffice26.8.0.3，不需改系统安装/关联。
- 两文稿覆盖静态复杂排版、点击动画/嵌入媒体，没有transition。

## 待实验回答（不是待用户澄清）

真实内容、动画顺序、媒体时钟、窗口/状态控制、重复生命周期、双目标隔离，结果进入verification.md。
