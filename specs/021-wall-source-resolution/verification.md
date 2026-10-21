# 021验收记录（2026-10-21）

## 结论与边界

本机实现完成，未推送/部署、未下发实体布局。D4切4K是用户手动操作，不是本轮脚本修改。当前4K输入与既有4K默认已一致；修复重点是其它输入的工程统一4K错误，以及同IP别名的配置冲突。

## 现场只读证据

1. 首次D4 API显示DISPLAY2–5为1920×1080，611A同样回读63–66为1080。63被动SPS为Baseline66、宏块119/67、frame_only=1、bottom_crop=4，算得1920×1080。
2. 用户随后告知已改4K。07:01UTC重新读取API：DISPLAY2–5全部3840×2160；主611A回读63–66也全部3840×2160。更早1080记录不继续作为当前值。
3. 再次被动读取63的SPS：Baseline66、宏块239/134、frame_only=1、cropping=0，独立得到3840×2160。仅流头，没有保存画面；ffprobe width=0不作为成功，FFmpeg trace_headers最后因无画面可封装退出非0，只使用实际解析的SPS字段。
4. 最新正式只读工具07:10:00–07:10:01UTC在D4运行，31路中30路匹配对应224.1.1.X:5000，84超时。18路4K、12路1080，详见[维护表](../../docs/input-resolution-021.md)。响应结构/连接IP/组播/端口均核对；未知CRC未伪称验证。
5. 63最新被动接收797包/1128297字节取得参数集；64–66各最多4秒无数据，不冒称SPS已验证。首次其它抽样组播也无包，不触发关键帧/配置/强制拉流。
6. 正式工具进程结束归零；最终查询/被动探测进程均0。D4原Host16956/会话1/运行包6c528ad保持；源仓仍9960725且clean。旧SSH失联后正常重连，两个自有SSH helper均关闭，未动项目服务。

忽略证据根`.validation/resolution021/`：initial记录见`.validation/input020/resolution-initial-d4.json`；切换前query-results/passive-results.json，切换后requery-stdout.jsonl、query-results-b.json、passive-results-b.json、63-trace.txt、sps-resolution.json；正式工具production-tool-current.json/summary、remote-final-state.json。无凭据或画面入Git。

## 自动化验证

| 检查 | 结果 |
| --- | --- |
| 首次新增C#回归（未修） | 34项中16失败/18通过，暴露混合尺寸、84默认和别名冲突；保留red-021.trx |
| Node工具初始（未实现模块） | 缺protocol模块而失败；实现后14/14通过，含TCP半包/超时/关闭、错误身份、接收预算 |
| 墙/电视/目录重点测试 | 115通过；5个需本机网卡别名的Physical用例明确跳过，未接触真实墙节点 |
| Host Hardware/Simulation装配与显式尺寸覆盖 | 3/3通过；只是解析装配，没有向节点发送 |
| Release完整构建 | 0警告、0错误，保留warnings-as-errors |
| 完整后端`Category!=Physical`串行 | 1793/1793通过，无跳过、无失败 |
| Spec Kit全仓校验、git diff --check | 通过 |

完整后端分组：Contracts346、ControlHost520、Domain50、Infrastructure290、Integration315、Windows272。命令与TRX在`.validation/resolution021/{build.txt,full-tests.txt,test-results/}`，未修改断言阈值或禁用安全机制。

新增包级回归覆盖63–66的4K及显式1080全屏/左右/自定义、完整源面积与边界、独立1920×1080目的矩形、校验和、同源重复块、4K与1080混合、未知拒发、配置覆盖、同IP冲突、电视源宽高及尺寸变更后的两类指纹失配。旧D2黄金包保留并通过。

## 未执行项目

- 没有发送实体墙/电视布局、设备配置、电源、云台或音量测试；不将CPU正确帧或成功回读当成实体画面确认。
- 50墙节点及电视输出尺寸只有工程证据，不是本轮实时输出模式核验；不能盲改4K。
- 除63外没有实际组播SPS/视频解码覆盖；84离线未知，不猜参数。
- 本轮无前端变更，未重跑前端/浏览器或公开客户端安装验收。
- 默认目录是本轮快照，不是实时热更新。信号再变需重新核验并显式配置、重启；工具不自动更新配置。
