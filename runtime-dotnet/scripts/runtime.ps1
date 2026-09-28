<#
.SYNOPSIS
  拒绝已退役的独立运行组入口，并提示当前认证生命周期入口。

.DESCRIPTION
  运行组由 ControlHost 分配代次、登记本机管道身份并验证两窗就绪。
  旧脚本的启动、重启、停机和状态动作统一失败，避免操作另一份运行组状态。
  旧参数仅保留解析兼容，以便既有调用收到明确的迁移指引。

.PARAMETER Action
  :param Action: 已退役的旧动作；默认状态查询也必须迁移到认证 API。
.PARAMETER RuntimeRoot
  :param RuntimeRoot: 旧运行时目录参数，仅兼容解析，不用于运行组操作。
.PARAMETER MediaMtxPath
  :param MediaMtxPath: 旧流服务路径参数，仅兼容解析，不用于运行组操作。
#>
[CmdletBinding()]
param(
    [ValidateSet('start', 'stop', 'restart', 'status')]
    [string]$Action = 'status',
    [string]$RuntimeRoot = '',
    [string]$MediaMtxPath = ''
)

$startGuidance = '先用 run-headless.ps1 -DataRoot <绝对数据目录> 和已核对的配置启动 ControlHost；确认 BigScreenOutputs.Window1/Window2 为两块不同的大屏，再通过已认证会话和 CSRF 执行 POST /api/system/restart/。'
$replacement = switch ($Action) {
    'start' { $startGuidance }
    'restart' { $startGuidance }
    'stop' { '先通过 ControlHost 的已认证会话和 CSRF 执行 POST /api/system/shutdown/；确认自有 Worker 和 Office 已退出，再用 run-headless.ps1 -Stop -DataRoot <同一绝对数据目录> 停止控制面。' }
    'status' { '请通过 ControlHost 的已认证会话查询 GET /api/runtime/；写操作继续要求会话和 CSRF。' }
}

throw "runtime.ps1 独立运行组入口已退役（Action=$Action）。$replacement 详见 docs/qa/003-workstation-runbook.md。"
