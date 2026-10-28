# Feature Specification: 滑动液态玻璃底栏

**Feature Branch**: `main`
**Created**: 2026-10-28
**Status**: Implemented
**Input**: 手机和Pad使用类似Apple的滑动液态玻璃悬浮底栏，参考成熟Android实现进一步优化。

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 连续滑动选择页面 (Priority: P1)

操作人员按住底栏滑动时，玻璃镜片连续跟随手指，随速度轻微拉伸；松手后弹性归位并打开选中的页面。

**Why this priority**: 提供用户明确要求的滑动操作与视觉反馈。
**Independent Test**: 验证连续位置、松手选择次数、取消操作和同页选择。

**Acceptance Scenarios**:

1. **Given** 任意底栏入口，**When** 按住并横向拖动，**Then** 镜片连续移动，页面保持当前内容。
2. **Given** 拖动中的镜片，**When** 松手，**Then** 吸附到最近入口，每次手势最多导航一次。
3. **Given** 被取消的手势或选择当前页面，**When** 操作结束，**Then** 返回实际页面的入口，保持表单与预览。

### User Story 2 - 手机与Pad玻璃表现 (Priority: P1)

手机和Pad横竖屏均显示悬浮底栏、清晰的玻璃边缘和折射，深浅色下图标与文字保持可读。

**Why this priority**: 多种尺寸和模式需要一致的材质与操作。
**Independent Test**: 真实浏览器检查手机、Pad四种尺寸、深浅色、安全区及旋转前后预览。

**Acceptance Scenarios**:

1. **Given** 手机或Pad，**When** 横竖屏切换，**Then** 底栏保持可达，入口尺寸至少44px，内容可以完整显示。
2. **Given** 减弱动态效果或透明效果的系统偏好，**When** 操作底栏，**Then** 保持相同导航语义及可读性。
3. **Given** 底栏动画结束，**When** 页面空闲，**Then** 停止底栏持续动画计算。

### Edge Cases

- 多指操作只由开始手势的指针控制；取消、失去指针捕获和尺寸改变会终止选择。
- 页面返回与二级页面打开时，镜片跟随实际路由；二级入口归属“更多”。
- 键盘可激活各入口；导航异常保留实际页面状态并向既有错误处理报告。

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: MUST 使用单个连续移动的镜片，具备按压、速度形变、弹性归位和玻璃折射表现。
- **FR-002**: MUST 在松手后执行一次导航；同页选择保持已有内容，取消操作不导航。
- **FR-003**: MUST 支持手机及Pad横竖屏、深浅色、安全区、键盘和系统减弱效果偏好。
- **FR-004**: MUST 保持手机精简预览、Pad保留预览及旋转复用行为。
- **FR-005**: MUST 使用可免费分发的开源依赖，停止空闲动画并在卸载时释放动画与监听。
- **FR-006**: MUST 完成真实浏览器验证、构建及签名arm64-v8a APK，将手机和Pad制品放到本机桌面并推送两个仓库。

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 连续滑动跨越五个入口期间导航次数为零，松手后最多一次，取消后为零。
- **SC-002**: 目标手机、Pad尺寸及深浅色下无横向溢出，五个入口均满足44px触控尺寸。
- **SC-003**: 动画结束后镜片停止更新；卸载后不继续更新位置或选择页面。
- **SC-004**: 手机不加载非必要预览，Pad旋转保留同源预览节点；APK通过签名和包内资源检查。

## Assumptions

- 复用现有五个入口及Vue、Capacitor客户端；桌面侧栏保持既有功能。
- 文稿底部的上一步、下一步保留动作按钮语义，可共用玻璃材质。
- 本轮为客户端界面更新，不操作D4显示器、设备与播放服务。
