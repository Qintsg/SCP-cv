# 研究决策

采用官方PowerShell7.6.7 win-x64 ZIP，SHA256为D2F952EBDBF24386CB8923553AA58E41A846DB35A6155EA04C58142A4B16DBB7，并存安装。D4系统已结束官方支持，实际版本、CIM、ScheduledTasks及DisplayConfig检查通过后才更新入口，不把已有.NET10运行当作官方兼容承诺。

来源：[发行页](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.7)、[安装说明](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows?view=powershell-7.6)、[支持政策](https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle?view=powershell-7.6)。

当前桌面CMD两层解释器、release任务和SSH工具明确选择pwsh；更新任务后同步startup.json动作摘要。start-runtime在同一个解释器调用固定包run-headless，没有Detach，固定包及manifest无需修改。Framework侧车仍由C#直接启动，保持net48及官方UNO引用。

SSH会话0不能读取交互路径，使用原会话1的隐藏、无登录触发器临时任务。显示器1保持0,0和1920×1200，2至5依次1920,0、5760,0、9600,0、13440,0，桌面及信号均4K；位置维护保持原时序。只序列化有限纯值；维护子进程设提交内存和时间上限，关闭Job时结束自有成员。
