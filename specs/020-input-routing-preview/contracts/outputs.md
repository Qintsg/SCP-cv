# 输入与输出合同增量

GET /api/inputs/：inputs数组，字段id/name/group/ip_suffix/preview_input/source_id?/availability/checked_at；group workstation/camera/site。POST /api/inputs/refresh/有界、合并和TTL，只探受信31地址的服务端口，不拉媒体；模拟无网络。

GET /api/display-routing/：各输出last_sent及逻辑状态，不声称实体实时读回。PUT /api/display-routing/televisions/{target}/：target仅tv_left/tv_right，body{input:{kind,ip_address?},force:true}；先完整校验，单路，受限协议，返回结果区分sent_unconfirmed/simulated/unchanged/uncertain。

advanced.televisions={tv_left:{state:'keep'|'set',input?},tv_right:...}，缺省keep。预案在同一操作门禁内判等，几何/来源参数不同仍下发；同源manual force不跳过；失败/未知/进行中不得判未变。电源不走此显示接口。
