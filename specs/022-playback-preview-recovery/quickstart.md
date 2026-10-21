# 验证流程

1. `uv run --no-project --no-config --offline .specify/scripts/python/validate_specs.py --specs-dir specs`。
2. `npm --prefix frontend run typecheck`、`npm --prefix frontend run build:web`。
3. 使用D4现有视频91/PPT90，完成至少五轮打开、切换、关闭，检查实际来源/代次、帧进度/页码及全部进程持续在线。
4. 使用真实零采样音频或正常音频，执行背景音频打开/停止及组恢复，确认真实状态、保留原声音配置。
5. 相同实际输入和受控真实编码画面分别记录冷首帧及60秒持续播放，比较软件附加延迟、解码进度、CPU和资源释放。
6. 所有测试进程、租约和临时媒体完成清理；当前正常播放意图按最新快照恢复。
