# T013认证音频控制与实际回读（部分）

日期：2026-10-11。基线签名保存点`e9cd94f`，工作区干净；本轮仅本机独立测试数据，没有连接或改动D4。

## 已接线链路

本人登录/CSRF的PATCH→Service同窗口门禁→SQLite事务核原Worker/组、当前OPEN代次及已提交实际后帧→SCREEN_SHARE_AUDIO→领取复核内存授权/原Worker→既有原生桥→已提交Worker报告→内存HTTP/SSE及本人GET实际属性。202提供精确命令ID，不修改普通音量/静音、源代次或实际属性。领取失效则终结命令，不下发控制或receiver_uri。

音频属性必须成对合法；普通源/缺失/畸形/失败报告为null，视频身份/帧可继续有效。本人GET只在当前身份/代次/正帧/在线/receiving与playing相符时合并actual_volume/actual_muted。未知或停止后的null不证明声卡静音。

## 本轮红绿与边界

- 鉴权路由先红1/1，未接入口返回404而不是401；实际字段及两目标受理先红5/5，不以编译失败代替行为红。
- 新测试首编译误用不存在的Offline枚举，核真实领域为Stale后只修夹具。接线后34项首轮32通过/2失败，严格三秒断言发现Deadline从事务内再次取时多出1–6毫秒；改为CreatedAt加三秒，不放宽断言，随后34/34通过。
- 领取专项10/10通过，包含6项新增：匹配、替换Worker、撤销、错误分享、错误代次及缺少装配；使用真实SQLite/正式Dispatcher，不启动Player。
- 另外补失败原生同形报告清旧回执、接收页帧但不匹配已提交Worker身份不能授权两项HTTP回归。首轮全ControlHost414项中413通过/1失败；旧精确字段集合断言缺新增nullable属性，核实际差异后保留精确检查并补普通源null断言。
- 原始TRX保留于`.validation/qa-screen-share-010/t013-http/`，红结果不覆盖；后续完整回归以末节实际执行记录为准。
- 本轮没有新增真实getDisplayMedia、原生WebView/有声音、正常选择器、HTTPS或D4证据，不拿上述纯值Worker报告当声音可听证明。网页专用控制仍未实现，默认接收静音；T013及T014–T018保持未完成。

## API与规范

OpenAPI3.0同步新PATCH、受理回执及actual属性。Redocly实际退出0，0错误/3条既有退役接口410缺2xx告警；不伪造退役入口成功响应。前端仅增加合同类型，无UI/控制行为变化。

## 最终本轮执行记录

完整非Physical命令实际退出0，六份独立TRX的计数分别读取核对，总1427通过、0失败、0跳过：

| 项目 | 通过 |
| --- | ---: |
| Contracts | 325 |
| ControlHost | 414 |
| Domain | 41 |
| Infrastructure | 171 |
| Integration | 279 |
| Windows | 197 |

```powershell
dotnet test runtime-dotnet/ScpCv.sln -c Release --no-restore -m:1 --filter 'Category!=Physical' --logger 'trx;LogFilePrefix=t013-http-full' --results-directory .validation/qa-screen-share-010/t013-http --verbosity quiet
pnpm --pm-on-fail=ignore --prefix frontend run typecheck
pnpm --pm-on-fail=ignore --prefix frontend exec node --test --test-concurrency=2 scripts/screen-sharing.test.mjs scripts/screen-sharing-output.test.mjs scripts/screen-sharing-navigation.test.mjs
pnpm --pm-on-fail=ignore --prefix frontend run build:web
redocly lint docs/openapi.yaml
uv run --offline --no-project python .specify/scripts/python/validate_specs.py --specs-dir specs
git diff --check
```

.NET构建/测试串行，命令进程清空代理，不修改全局配置。前端相关28/28、类型/Web构建退出0；入口1129.01kB/322.37kB gzip的大块警告保留，不提高阈值隐藏。完整前端本轮未执行，留T018；不把过往348项当新结果。Redocly及Spec Kit退出0，diff检查通过。质量清单16/16只读，扩展hooks不存在按skill跳过；本轮编辑代码均不足500行。没有常驻QA服务器或新增依赖，D4稳定009未改动；T013仍未勾选。
