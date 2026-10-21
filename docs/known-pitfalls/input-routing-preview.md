# 020输入与预览接线边界

## 首帧等待可能来自浏览器积累，不只是设备网络

018同一合成源ffprobe已出帧但浏览器等待约4秒。锁定mpegts.js1.8.2实际源码默认64KiB stash，不是在线其它版本的384KiB；低码率积满本身就慢。关闭多余积累、受控GOP半秒后实际帧p50／p95均下降。不能把首字节或POST200当浏览器首帧，也不为缩时破坏完整GOP预算保护。

## 同流共享不能把第一个页面或别名当总所有者

只合并服务端FFmpeg仍会让同页几十个decoder重复工作；以第一个组件的useRequestContext拥有池又会使其卸载时误停其它画面。020用独立应用认证scope、单video和多canvas。每目标验证租约＋每实际流消费租约，HMAC绑定完整票据与URI。别名独立改修订不误停其它仍有效别名；不能用已删源的消费租约无限续旧URI，也不能由几何更改切chn通道。

## 两个弹层有同名选项，不能点击Ant缓存中的旧菜单

从左侧切到右侧时，旧Popover可能仍保留隐藏DOM。首次测试用全document找“窗口1”，实际点到左侧旧菜单，断言误报右侧没变。触发器用aria-controls关联唯一menu ID，测试只操作对应菜单，仍验证往返布局与最终实际提交。不能删断言或直接调用私有处理器冒充交互。

## 输入后同轮卸载可触发Ant4.2.6空ref异常

新自定义IP输入事件后立即同ID重登，全部功能断言通过却出现异步unhandledRejection。栈定位锁定版本vc-input/Input.js的setValue：受控value分支nextTick直接访问inputRef.value.input，卸载后ref=null。020菜单搜索／IP使用default-value加同步change的非受控输入；本地状态仍受认证失效清除，不改node_modules、不吞全局异常、不用延时掩盖。保留同轮输入／认证回归。

## TCP可达、工程声明、成功下发都不是实体读回

工作站端口发现不代表可解码；原表声明4K不代表当前SPS4K。电视输出1080不等于输入1080。窗口3/4来源65/66可用于已登记组播，并不表示新增两个PlayerWorker。电视610D与大屏50节点序列不同；服务端只记录完整成功下发的逻辑意图。dispatching／uncertain／电源失效不能用旧last_sent去重。

## 新增发送前记账会改变故障注入落点

旧测试“硬件动作后提交失败”曾对每次Save／Commit都抛异常，020新增dispatching预记账后故障提前命中首包前，旧期望不再对应题义。将该测试明确注入第二事务，同时补第一事务失败零帧断言；不删除失败、不放宽生产门禁。提交后回执失败仍不盲目补偿或重放。

## 原生心跳取消和参数必须实际验证

Windows控制台StreamReader.ReadLineAsync(token)不能可靠打断阻塞管道。将截止Timer与读stdin分离，不等待无法取消的读取，独占Job控制自有FFmpeg；真实EOF／心跳／helper强杀／Electron父强杀分别验零残留。RTSP使用专有timeout，不能套RTMP的rw_timeout。

Electron默认CLI的多组独立位置参数曾让探针在用户代码前退出，不能凭退出码、NotSigned或测试报告缺失猜执行策略。最小CJS包、参数增量定位后，把仅探针合成参数放环境变量，正常沙箱与TLS不改。最终以真实44.2 app协议帧和强杀后的进程证据验收，不以Node Request替身代验。

## 部署必须连代理预检一起验，不能只测直接后端或POST

D4直接18443的OPTIONS已精确允许app://scp-cv及凭据，网页443的同一请求却返回无Allow-Origin／Credentials的204；登录POST本身可成功，仍不证明renderer可先通过预检。根因为Vite默认cors在代理前截获所有OPTIONS，即使不允许该Origin也结束请求。dev/preview设cors:false，把/api策略唯一交给ControlHost；回环两模式红绿测试要求真抵达假后端，伪造来源保持403。生产复核同两层请求，不使用cors:true、通配反射或关闭webSecurity。
