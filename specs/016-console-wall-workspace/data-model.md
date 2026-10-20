# 数据模型

- WallRect: column0–9、row0–4、columns1–10、rows1–5；不越界/不重叠。
- WallMapping: region兼容fullscreen/left/right，新增grid与rect；input新增window_3/window_4（规划专用，不可应用）。
- LayoutCode: 规范排序后的矩形/输入无损编码；版本、校验和、字母数字；解析整体成功后替换，不执行设备动作。
- PreviewSession: 仅本机保存上游地址、随机会话令牌、最后帧、订阅/到期状态；来源许可，最大并发、超时、禁音；不持久凭据。
- Scenario: advanced增加wall_layout_code可选字段（复用现有JSON列），保留旧三态；对应YAML文件根据目录保存，ID为稳定身份；音量null保持/0有效，机位0归一null。

状态：选择→预览→显式应用→既有全量预检→按修订下发→音量配置；失败不补发。窗口3/4或未知裁切在预检阶段拒绝。
