# D4升级依据

## 系统与安装媒体

- 已确认D4为专业版1909 x64，原始安装语言0804（zh-CN），C盘和独立D盘健康、空间充足，C盘未启用BitLocker。
- 采用Windows 10 22H2专业版同语言就地升级，必须保留应用、文件和设置。媒体摘要、镜像版本、语言和专业版索引在使用前实际校验。
- 官方下载页：[Windows 10](https://www.microsoft.com/zh-cn/software-download/windows10ISO)。微软列出的简体中文x64 ISO SHA256为`D485D370406CBCB68959718817BD12ED87E537A14C885F84962E07136FC4A049`；实际媒体版本与对应摘要一并核对。
- 正式安装参数及扫描退出码依据：[Windows Setup命令行](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/windows-setup-command-line-options)。不使用忽略兼容性阻止或只保留文件选项。
- 官方产品目录`https://go.microsoft.com/fwlink/?LinkId=841361`提供22H2专业版、zh-CN、x64消费版ESD。原文件为4185785021字节，SHA1为`B4C440E96BD81EFC6245D5FCC4682440D738E51A`；本机下载实际SHA256为`96B5FA73E7B155309159DC64D88900A52E564AA93611B6BC0250E1AB629275A9`。
- 安装目录由微软DISM读取实际索引、应用原始Setup Media并导出原始引导和专业版图像。正式Setup必须通过保留应用的兼容性检查。`/ResizeRecoveryPartition Disable`保持既有恢复分区布局。

## 系统卷恢复资料

- 系统映像明确包含C盘和579MB系统保留卷；D盘保持原分区和文件，项目DataRoot及私有配置另外逐文件SHA256冷备。
- 备份通过显式目标的`wbadmin get versions`和`get items`验证时间、卷GUID、大小及卷恢复能力。恢复范围为原有磁盘和分区布局保持时的系统卷回退；不宣称已验证新磁盘重建。
- 原始磁盘签名、扇区、分区偏移、卷GUID、引导配置与WinRE位置作为恢复依据保存。官方依据：[VSS恢复条件](https://learn.microsoft.com/en-us/windows/win32/vss/using-vss-automated-system-recovery-for-disaster-recovery)。

## 显卡驱动

- 使用582.78企业版DCH WHQL安装包，签名、版本和A4000支持条目均核对。
- 采用解压后的`setup.exe -s -n Display.Driver`；不使用清除设置选项；保留现有HD Audio组件及音频配置。
- 原生退出码0表示成功，1表示成功但需要重启，其它结果作为失败处理。
- 官方依据：[NVIDIA Windows安装说明](https://docs.nvidia.com/datacenter/tesla/driver-installation-guide/windows.html)、[582.78发布说明](https://us.download.nvidia.com/Windows/Quadro_Certified/582.78/582.78-win10-win11-nvidia-rtx-quadro-release-notes.pdf)。

## 自动更新

- `HKLM/SOFTWARE/Policies/Microsoft/Windows/WindowsUpdate/AU/NoAutoUpdate`为DWORD 1。
- `HKLM/SOFTWARE/Policies/Microsoft/Windows/WindowsUpdate/ExcludeWUDriversInQualityUpdate`为DWORD 1。
- Windows Update的人工更新能力保留，不删除更新任务或修改系统服务权限。
- 本次Setup明确关闭Dynamic Update，避免安装过程中更新驱动。
- 官方依据：[Windows Update设置](https://learn.microsoft.com/en-us/windows/deployment/update/waas-wu-settings)、[更新策略](https://learn.microsoft.com/en-us/windows/deployment/update/waas-configure-wufb)。
