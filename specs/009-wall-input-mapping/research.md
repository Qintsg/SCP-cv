# Research: 受限墙面接入

## 输入与拓扑

- Decision：1/2/laptop绑定55/56/81，现场单播、组播、5000端口和3840×2160；节点地址保持既有列优先45/46交换，协议C另按行优先。
- Rationale：用户确认IP81，现场目录与原算法拓扑一致。
- Alternatives considered：硬编码每种组合，无法正确处理标识和配置。

## 几何与兼容

- Decision：full裁剪384×432、半屏768×432；每区域独立Sign/Level，A=1/2，B每窗累计，C=行列索引。默认配置旧预设保留400包。
- Rationale：片尺寸可4整除，不依赖未知余数/拼缝分支。
- Alternatives considered：将固定9/1/1扩展到所有布局，违背原业务来源。

## 编码与状态

- Decision：显式SendType/CutSync/IGMPv3/新协议；6102/6210/6160/6120独立codec，同节点多帧有序；可选DES-ECB零填充，配置失败零发包。有界回读解析不产生verified，查询触发未证实。
- Rationale：第四版原代码向量明确6120仅校验2..39；回读不携带操作版本，61FA非窗口ACK。
- Alternatives considered：固定6102、整包求和或任意回读当确认，会假报成功。

## 证据

主代理与speckit-plan只读研究代理核对04/03/09章节、site-inputs和独立模型；未运行覆盖证据脚本或访问设备。原算法13组、配置9组已有向量可复用，本机软件测试不代替现场出画。
