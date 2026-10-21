# Tasks: 播放恢复与摄像机低延迟

**Input**: 本目录spec.md、plan.md、research.md、data-model.md和contracts/behavior.md。

## Phase 1: 当前证据

- [x] T001 读取当前全局规则，在specs/022-playback-preview-recovery/research.md记录当前运行组及音频错误证据。
- [x] T002 在specs/022-playback-preview-recovery/verification.md记录当前合法播放状态、真实媒体文件、当前进程身份及声音基线。

## Phase 2: 真实验证条件

- [x] T003 在runtime-dotnet/src/ScpCv.ControlHost/Ipc/RuntimePipeBroker.cs与ScpCv.Supervisor/Program.cs完善当前角色/消息耗时/退出原因的安全诊断，使用真实运行组复现。
- [x] T004 在specs/022-playback-preview-recovery/verification.md记录真实FFmpeg及成熟格式组件原型、版本和资源释放结果。

## Phase 3: US1 媒体打开（P1）

独立验收：D4视频91与文稿90真实打开、显示进度/页码、切换和关闭至少五轮，运行组持续在线。

- [ ] T005 [US1] 通过实际REST/Named Pipe/播放器在specs/022-playback-preview-recovery/verification.md记录视频/PPT失败及最先退出证据。
- [ ] T006 [US1] 在runtime-dotnet/src/ScpCv.Supervisor/与ScpCv.PlayerWorker/Playback/修复已证实的运行组或媒体执行问题，错误立即明确报告。
- [x] T007 [US1] 运行真实视频/PPT交替、多开和关闭验证，在specs/022-playback-preview-recovery/verification.md记录每次结果和退出资源。

## Phase 4: US2 背景音乐恢复（P1）

独立验收：真实音频打开/停止和正常运行组恢复后状态正确，原声音配置和列表保持。

- [x] T008 [US2] 在specs/022-playback-preview-recovery/verification.md记录当前AudioWorker对正常STOP和实际音频的结果。
- [x] T009 [US2] 在runtime-dotnet/src/ScpCv.AudioWorker/与ScpCv.Infrastructure/Runtime/修复当前身份的恢复观测，验证历史事件不能覆盖当前状态。

## Phase 5: US3 摄像机延迟（P1）

独立验收：相同受控实际编码画面的软件附加延迟降低至少30%，真实摄像机连续60秒和断开恢复可用，按需共享保持。

- [x] T010 [US3] 在specs/022-playback-preview-recovery/verification.md记录真实摄像机/受控画面的首帧、持续缓冲、编码速度及软件延迟基线。
- [x] T011 [P] [US3] 在runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/和frontend/electron/preview/使用成熟容器处理组件，删除相应手工FLV解析。
- [x] T012 [US3] 在frontend/src/features/preview/previewPlayer.ts与runtime-dotnet/src/ScpCv.ControlHost/StreamPreview/PreviewProducer.cs调整受控GOP、实时缓存/追赶和明确错误处理，验证同源共享、持续播放和释放。

## Phase 6: US4 当前开发约定（P2）

独立验收：真实npm构建、Electron目录构建、uv规范检查运行通过；本轮代码具备完整文件头，当前命令一致。

- [x] T013 [P] [US4] 修改package.json、frontend/package.json、.npmrc、frontend/scripts/build-electron.mjs及Android依赖路径，统一实际npm工具链并运行真实构建。
- [x] T014 [P] [US4] 修改.specify/scripts/python/的顶部注释与直接导入，使用uv运行所有辅助入口和规范校验。
- [x] T015 [US4] 更新AGENTS.md、STYLE.md、CONTRIBUTING.md、README.md、.github/PULL_REQUEST_TEMPLATE.md与当前使用/维护文档，确认与实际脚本一致。

## Phase 7: 综合验证

- [x] T016 在specs/022-playback-preview-recovery/verification.md记录全部真实验证、清理、D4当前状态恢复和接口文档检查。

## Dependencies & Execution Order

T001/T002→T003/T004；US1按T005→T006→T007，US2按T008→T009，US3按T010→T011→T012；US4的T013/T014使用不同目录，可独立执行；T015依赖两者，T016依赖全部修改与实际验证。

## Parallel Opportunities

按照任务模板的并行团队执行方式，媒体运行组研究/修复、预览成熟组件原型和npm/辅助工具规范整改可以由不同执行者负责。各执行者遵守固定文件范围，主执行者核验所有合并结果和现场进程。

## Implementation Strategy

每个故事完成根因复现、实现和真实运行验证，再检查其它故事与整体行为。现有用户文件和全局规则原文件保持。需要调整方案时更新对应规范，不使用替身、修改成功判据或隐藏失败来通过测试。

## Phase 8: Convergence

- [ ] T017 [US1] 在runtime-dotnet/src/ScpCv.ControlHost/Ipc/与ScpCv.Supervisor/使用当前角色和消息诊断，结合独立DataRoot的实际进程复现原始IPC超时后整组退出；确定最先停止进展的环节后修复并重复验证，在specs/022-playback-preview-recovery/verification.md记录触发条件、真实退出和当前媒体恢复，完成T005/T006；来源FR-001、FR-005（partial，HIGH）。

## Phase 9: 双仓部署与启动核验

- [x] T018 将已验证提交推送origin和gitlab，在D4的D:/SCP-cv正常快进源码；用独立完整运行包及网页更新服务、冷备数据、重启项目服务，恢复停机前当前合法媒体和声音配置，记录到specs/022-playback-preview-recovery/deployment.md。
- [x] T019 核对D4桌面入口、计划任务、固定脚本和摘要，实际执行一键启动的冷启动及重复启动，比较系统显示模式、输入读回和原进程身份，确认其是否涉及当前故障；记录到specs/022-playback-preview-recovery/deployment.md。
- [x] T020 将本轮部署与启动验证更新到当前维护文档、变更记录及specs/022-playback-preview-recovery/deployment.md，提交并同步两个仓库及D4源码，不重放实体布局或设备电源命令。

## Phase 10: 显示模式回退调查

- [x] T021 只读核验D4的Windows显示模式、显卡/显示器身份、推荐模式、保存配置、GPU超时事件及启动前后结果，在specs/022-playback-preview-recovery/display-diagnosis.md区分确证事实与尚未复现的触发路径；不修改系统显示配置或驱动。
- [ ] T022 取得4K自动切换到1080p瞬间的模式和GPU证据，确认显示描述、驱动恢复或其它具体调用的作用；按明确维护范围完成受控复现及验证后，才能认定分辨率回退原因已经解决。
