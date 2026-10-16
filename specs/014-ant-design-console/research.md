# 调研与设计决策

## 参考项目

- [Companion官方界面](https://companion.free/user-guide/v5.0/config/the-user-interface/)：侧栏、主工作区和上下文面板；借鉴控制/配置/日志分离，不复制按钮墙或品牌。
- [Frigate Live View](https://docs.frigate.video/usage/live/)：概览到单路操作、状态跟随对象；借鉴集中云台，不默认拉六路直播、不虚构点击定位。
- [Vben路由菜单](https://doc.vben.pro/guide/essentials/route.html)：导航、页头、面包屑一致性；不引入其monorepo、多标签页或商业数据图表。
- 三项目主仓库均MIT；仅参考交互，代码、品牌及图片不复制。
- [Ant Design Vue官方release](https://github.com/vueComponent/ant-design-vue/releases)及[依赖声明](https://github.com/vueComponent/ant-design-vue/blob/main/package.json)：选择4.2.6正式版，MIT、Vue>=3.2.0；保持现有Vue技术栈。

## 组件与交互

- Decision: 真实Ant组件直接迁移，不为旧N标签做兼容外壳。
- Rationale: 51个Naive引用文件涉及Slot、事件和生命周期，换皮会隐藏逻辑缺陷。
- Alternatives: 不采用React重写或全套Vben模板，避免无关框架迁移和后台噪声。
- Slider的change和afterChange分别接原节流/收尾，保留云台pointer/失焦停止。
- Dropdown通过业务ActionMenu的Ant Menu单次派发，保留disabled/show/children检查。
- Toast沿用业务store，Ant notification context holder接主题，毫秒转秒，0持久；对话框保留危险确认。
- Drawer/Modal使用open；Input直接传原生ARIA；Alert message/description均真实渲染；Select按label筛选。

## 一次应用布局

- Decision: 单动作内部按返回修订预检/发送；保留后端草稿合同但不作为用户操作步骤。
- Rationale: 既有后端具备修订冲突、设备流能力及音量重发，不为删除按钮改设备端事务。
- Alternatives: 不使用“选择即下发”；不绕过expected_revision；隐藏详情不等于伪造实体确认。

## 视觉方向

现场控制台、低噪声蓝色主操作，深蓝侧栏与浅灰工作区，暗色使用Ant算法。色板集中定义：主蓝#1677ff、导航#111c32、画布#f3f6fb、卡片#ffffff、次文本#52627a、边线#e2e8f0。标题使用系统中文黑体/Segoe UI，正文同系统字体，IP/路径用Consolas等宽。特色是“两个输出→一个墙面编排”主区，辅助声音和电源独立窄栏。保留旧语义CSS变量名供样式引用，不保留旧组件或Fluent运行依赖。

布局复核：不采用统计卡、营销hero、荧光配色和满屏组合；手机按任务堆叠并保留底栏。真实浏览器核对密度、焦点、暗色及弹层。
