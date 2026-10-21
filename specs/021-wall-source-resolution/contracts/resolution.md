# 分辨率合同

公开REST/SSE不变，无新增OpenAPI端点。

`node tools/input-resolution/probe.cjs`只查固定51–74、76、81、84–88的4830，不查询75或输出节点。每连接只发12字节611A常量，最多1500ms及65536字节；解析半包/粘包，核对FCFD/611A/零控制位/N10/FEFF、224.1.1.X:5000和正宽高。回读校验算法未知，保留原两字节，不声称CRC已验。

状态区分matched、timeout、socket错误、closed、budget、invalid_reply；JSON输出stdout，不写配置、不拉视频、不自动重试。

源尺寸仅影响墙裁剪26/28/30/32及电视源宽高14/16；节点目的矩形独立。未知/冲突首包前拒绝，D4四路保留4K；同IP别名共享唯一有效描述。
