# 前端滑块在途与目标身份（2026-10-04）

## 缺陷与修复

T072第三切片：真实PlaybackControl/Naive滑块通过焦点与键盘输入，首HTTP挂起、第二输入进入缓冲。
窗口音量缺isActive/scope，实际类型失效或同类型换源后仍补发第二PATCH；
视频定位已有请求源身份，但不检查实际适配器，失效后仍补发第二SEEK。
三项公开DOM业务红均为写请求2而应只有已发送的1，不模拟组件内部回调。

窗口音量配置在线/类型可操作检查，音量及定位共用playbackSliderScope：
window/source/URI、actual源/适配器、desired/observed代次按值组成稳定身份。
复用既有useThrottledSlider失效机制，不另写队列；失效时仅丢弃未发缓冲并隔离旧finally/错误，
不承诺撤销已受理HTTP。普通心跳、音量/位置变化不改变目标身份，不丢合法最终输入。
同源重开使旧值失效，新用户输入仍可发送；不自动重放旧值或延长节流预算。

新增6DOM：三红绿、音量/定位同源新代次、新域新输入、普通心跳保留合法49→48。
相关25/25、全前端232/232、0跳过；类型/Web构建通过，主包1105.39kB/gzip315.14kB分块告警保持。
PlaybackControl481行，规则集中在既有能力模块，未越500行继续堆积。
日志位于`.validation/008-playback-output/slider-*-red.log`、`slider-*-green.log`、
`slider-related-green.log`、`slider-full-frontend.log`及`slider-build.log`。

## 浏览器复核

use-my-browser路径使用自有IAB模拟页20887、真实共享组件/Pinia/Naive。
选video并确认新源，暂挂首写响应，音量键盘左两次：显示本地98，只有PATCH volume99已发。
实际适配器改image再释放原响应：写请求仍1、没有volume98补发，滑块回到远端100并禁用。
这是有意提供的不对应观测，仍显示服务端原playback_state；不以该Playing证明有效播出。
本切片不改完整标题/状态能力选择，不把模拟画面当D4实体证据。

390×844实测scrollWidth380，1440×900为1440；浏览器warn/error0。
原PNG为`.validation/008-output-browser/slider-inactive-mobile.png`及`slider-inactive-desktop.png`，
人工核提示、禁用状态与仅一条请求，手机堆叠/桌面行布局无横溢。
没有修改已保存的生产站点笔记：一次性QA不在其标题适用范围，也没有新的通用规律。
视口恢复、自有页关闭；Vite PID28564/start UTC12:27:45.429583按精确身份退出，监听0。
自有Vite配置加载的esbuild解析告警保留，不因此安装新依赖或提高告警阈值。

## 边界

仅两大屏控制条的音量/视频定位；系统音量与独立背景音频滑块不是本次已验证范围。
实际来源模式、同VLC细项、SourcePicker、背景音频REST晚到/SSE和整个混合长稳继续检查。
Spec/diff、UTF-8/LF通过。本轮使用测试驱动与小模块接口保留真实入口验证，“说人话”只整理汇报。
