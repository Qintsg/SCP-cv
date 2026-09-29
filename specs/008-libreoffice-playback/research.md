# 技术门禁与选择

## 已确认

- 保存点549c57f+checkpoint-before-libreoffice-20260929；实验代码归档SHA4335B0A4…02788，原件/数据库保留。
- 官方cli_uno为旧C++/CLI，.NET10加载RealProxy相关类型TypeLoadException（managed-net10.jsonl，exit2）。
- 纯C#Net48使用官方uno.ini、显式URE_MORE_TYPES/offapi.rdb并连接自有profile：XDesktop/XComponentLoader/XDesktop2可cast，真实context成功（framework-connection-unoini.jsonl）。
- 原型初始未加载offapi导致cast失败、fundamental.ini配置SEH失败保留；不得把连接成功写成纯播放器全部能力通过。
- 007正常Frame两profile10轮/100goto可行；隐藏父窗口组合崩溃；暂停标志不能代表媒体冻结。

## Decision

纯C#Net48侧车+官方现有LibreOffice，主.NET10不加载旧CLI，协议中只交换纯值。用户已接受未支持的统一媒体控制明确禁用；先完成承载/关闭门禁，再实现完整运行组与转换。协议及能力拒绝回归可先并行，不提前启动生产原生宿主。

## Rationale

保留成熟导入/绘制路径、原生故障隔离与两窗独立性，不引入源码维护或Python运行时。

## Alternatives considered

源码fork不在范围；Python桥仅QA；把旧CLI直接放.NET10已证实不兼容；单全局Office实例不满足两窗隔离。

## 必须以实际观测回答的门禁

纯净Frame画面、PNG/PDF导出和准确退出仍需实证。当前统一媒体暂停/恢复/定位明确不支持，能力表和拒绝路径需验证；用户2026-09-29已接受，不改写此前媒体接口失败证据。
