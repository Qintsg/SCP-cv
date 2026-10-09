# Verification: 大屏组合与协议接入

日期：2026-10-09。本轮只在本机独立SQLite/Simulation与回环TCP验证，不连接D4、不操作墙面或电源。008未提交工作区保留；本轮没有提交、推送或部署。

## 实现与证据映射

| 需求 | 实现与验证 |
| --- | --- |
| FR-001/002/004 | Planner及输入55/56/81配置；12完整组合、6单侧、重复窗口独立Sign/Level、A/B/C和逐片裁剪；旧两预设合计400黄金样本保持 |
| FR-003/009 | 四种codec与原业务6102/6210/6160/6120向量；Type3同节点序列，公开合成DES向量；无效配置/未登记IP清屏前拒绝；增量窗口回读和真实回环收集 |
| FR-005/006 | 阶段屏障、重试/取消、控制器排他；新增跨Runtime实例共享门禁；发送失败与持久失败只报未确认/不确定，不自动重放 |
| FR-007/008 | 独立草稿/发送布局与修订；重建读取、旧预案替换、手动快照unset、静音、旧版本409、无效版本400；重启恢复和失败保留 |
| FR-010 | OpenAPI、运行指南、README、使用/维护、CHANGELOG及pitfalls同步，005历史门禁注明由009接续 |
| SC-001..004 | 以下包/HTTP/SQLite、DOM及真实浏览器分层结果；没有实体出画证明 |

## 自动化结果

最后完整.NET命令（显式清空HTTP_PROXY/HTTPS_PROXY/ALL_PROXY，只影响该命令进程）：

```powershell
dotnet test runtime-dotnet/ScpCv.sln --no-restore --filter 'Category!=Physical' --logger 'trx;LogFilePrefix=final009b' --results-directory .validation/qa-wall-009/results --verbosity quiet
```

1202通过、0失败、0跳过：Domain41、Infrastructure164、Integration262、ControlHost286、Contracts322、Windows127。6份原始TRX文件匹配`final009b_net10.0_20261009132*.trx`，位于忽略的`.validation/qa-wall-009/results/`；以原件为准，不以本摘要代替原件。

相关子集另外通过：Infrastructure VideoWall52（之后补4条原单播/扩展向量）；ControlHost VideoWall9；Integration VideoWall/ScenarioVolume33（之后补发送前提交失败回归，最后VideoWall子集26通过）；DOM大屏14。全量已包含这些测试，数字不重复相加。

```powershell
pnpm --prefix frontend run test
pnpm --prefix frontend run typecheck
pnpm --prefix frontend run build:web
redocly lint docs/openapi.yaml
python .specify/scripts/python/validate_specs.py --specs-dir specs
git diff --check
```

- 前端全套288通过、0失败/取消/跳过，两轮通过（首次约350秒，后续约104秒）。
- 类型检查、Web构建通过。保留现有单包超过500kB的构建告警，未以提高阈值隐藏。
- Redocly通过、0错误；3条现有已退役PowerPoint/PPT重置入口只有410、无2xx的告警保留，不为通过校验伪造可用入口。
- Spec Kit全目录校验通过；diff whitespace检查无输出。

## 真实浏览器与视觉

使用webapp-testing与Playwright实践：先查看实际DOM，使用真实ControlHost/Vite与认证/CSRF，不模拟业务API。服务仅绑定回环18943/5199，自有随机账号和GUID DataRoot，SafetyMode=Simulation，不启动Supervisor/播放器。

- 桌面1440×1000，UI逐项选择、保存、应用12完整组合，核对200响应、草稿修订和实际返回映射；明确显示“模拟结果：未向实体墙面发包”。
- 另验证6单侧布局；未映射区域由后端全节点清屏规划覆盖，不据浏览器证明物理清除。
- 刷新保持IP81布局；修改未保存禁用；另一客户端改稿返回真实409，读取新状态但保留当前表单；未登记239.1.2.3草稿可存但应用禁用。
- 390×844窄屏检查无横向溢出。原图`desktop.png`、`desktop-viewport.png`、`mobile.png`、`mobile-inputs.png`、`mobile-result.png`、`unsupported.png`保留在`.validation/qa-wall-009/`；人工查看原图，左右预览、输入和结果换行正常。窄屏全元素长截图可能带固定顶/底栏，不能把截图中固定栏位置当布局错位。
- 两轮12组合真实浏览器通过，最后一轮同时通过12完整组合和6单侧布局。页面异常0；控制台只有主动制造旧版本产生的1条409资源错误，无额外脚本/控制台错误。
- 本轮启动的自有Host/Vite由finally精确结束；不全局结束node/dotnet，不操作远端服务。

## 失败与修复记录

1. 编译先暴露DES安全分析、返回类型/常量数组/token参数位置等项目分析器错误，按限定协议兼容处理；DES抑制仅在显式兼容方法，不全局关闭，不提交现场密钥。
2. 新Simulation未知模式提示与旧断言不一致，恢复旧错误文案；新HTTP测试集合语法修正。不更改黄金包或放宽正确性断言。
3. 全套运行期间启动追加测试的交叉编译，产生DLL/Manifest锁：追加编译失败，Host5项IOException。停止交叉编译后完整重跑1197通过，增加恢复边界回归后最终1202通过；原失败保留于`wall009_*.trx`。
4. 重启中断状态先红后绿；非法JSON版本类型已补400回归。手动布局Capture先红后绿，旧预案不保存custom或误切单/双屏。
5. 数据库连uncertain标记也无法写入时，原提交失败被标记异常遮盖：新增故障注入先1失败/2通过，修复后3通过。随后增加初始发送标记提交回执失败回归：0次设备调用，状态标为uncertain并明确尚未发送。4项恢复回归均纳入最终全套。保留原外层错误，数据库恢复/下次启动可将dispatching转uncertain，不重放墙面。
6. 浏览器首轮在重负载下自有服务45秒启动超时，收尾后重跑成功；不更改生产预算。原生radio隐藏input不适合check，改普通可见标签点击再核对checked；Naive菜单离场且保留隐藏DOM，等待可见弹出层退出，不强制点击、不固定sleep。
7. 真实页面发现旧“两预设可用”文案，同步更正；手动布局显控按逻辑窗口号标记，PPT专注页标题也不绑定物理左右。

## 剩余实体边界

未部署D4、未发送50节点新组合、未现场抓包、未确认输入IP81真实信号和尺寸。6102窗口回读仅被动诊断，原软件查询触发/操作版本关联未证实，不自动标verified。旧黄金包/原程序模拟向量不替代设备固件行为或画面。

异形/混尺寸墙、拼缝、不可整除裁剪、特殊型号6150/6301/6304、任意未登记IP流以及KVM/固件维护不在本轮范围。电源与两电视toggle保留独立；本轮软件验证不表示已开机、关机或恢复现场布局。
