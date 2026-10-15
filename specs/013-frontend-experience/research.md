# 研究与设计决策

- Decision：沿用清源/Fluent语义令牌与Naive UI。Rationale：项目已有明暗主题、焦点与组件体系，重新换皮会增加不一致。Alternatives：新UI框架、额外字体和装饰性动效均不采用。
- Decision：公共页头与空结果组件。Rationale：当前首页/显控/设置重复品牌式头部，媒体筛选空结果与目录空混用。Alternatives：只加全局CSS无法修复信息结构，复制每页样式难以维护。
- Decision：首页显示两个逻辑窗口真实投影和进入操作的链接。Rationale：已有sessions状态，不新增请求；不能从在线推断出画。Alternatives：大号统计和营销hero不适合现场控制。
- Decision：导航后重置主滚动并把焦点移到主内容，保持同页表单编辑不受影响。Rationale：当前AppShell内容区域独立滚动，路由切换可能保留深滚动。Alternatives：只配置window.scrollTo不能控制该区域。
- Decision：本机开发/隔离后端与只读或仿真动作验证。Rationale：用户本轮先不处理现场阻碍，不能借UX优化发物理动作或改安全策略。
- Decision：按用户指定“说人话”处理用户可见文字，保留数字、阶段、责任主体及限制；不借文案修改协议字段或程序注释。
