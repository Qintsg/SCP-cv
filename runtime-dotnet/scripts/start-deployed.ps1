#Requires -Version 7.2
#Requires -PSEdition Core
# 文件描述：核验并启动当前部署任务，支持仅检查模式。
# 项目：SCP-cv；文件：start-deployed.ps1；作者：Qintsg；日期：2026-10-22。
<#
.SYNOPSIS
  启动已部署的SCP-cv任务；重复运行复用现有服务，不切墙或开设备电源。
.PARAMETER ConfigurationPath
  :param ConfigurationPath: 部署阶段生成的本机配置，不含口令，仅引用既有受限凭据文件。
.PARAMETER CheckOnly
  :param CheckOnly: 仅核对主机、任务动作和文件，不启动服务或访问业务API。
.PARAMETER NoPause
  :param NoPause: 自动验收时不等待用户关闭控制台。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ConfigurationPath,
    [switch]$CheckOnly,
    [switch]$NoPause
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$startupExit = 1
$startupMutex = $null
$startupLocked = $false
$startupApi = $null

# :param Value: 原始字符串。:returns: UTF8摘要，仅用于固定任务动作身份。
function Get-StartupDigest([string]$Value) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-', '') }
    finally { $algorithm.Dispose() }
}

# :param Path: 固定API路径。:param Method: 请求动作。:returns: 已认证JSON，不输出Cookie或凭据。
function Invoke-StartupApi([string]$Path, [string]$Method='Get') {
    $parameters = @{UseBasicParsing=$true; Uri=($startupConfig.api_base+$Path); Method=$Method;
        WebSession=$startupApi; Headers=$startupHeaders; TimeoutSec=90}
    if ($Method -eq 'Post') { $parameters.ContentType='application/json'; $parameters.Body='{}' }
    (Invoke-WebRequest @parameters).Content | ConvertFrom-Json
}

# :returns: 固定部署目录中的运行组成员；任何异目录同名成员都拒绝，不杀进程。
function Get-StartupMembers {
    $members = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('ScpCv.Supervisor.exe','ScpCv.PlayerWorker.exe','ScpCv.AudioWorker.exe')
    })
    foreach ($member in $members) {
        $expected = Join-Path $startupConfig.runtime_root $member.Name
        if ($member.ExecutablePath -ne $expected -or $member.SessionId -ne $startupConfig.session_id) {
            throw '发现不属于当前部署或登录会话的运行组，请维护者先核对；未重启或终止它。'
        }
    }
    return $members
}

try {
    $startupPath = [IO.Path]::GetFullPath($ConfigurationPath)
    $startupConfig = Get-Content -LiteralPath $startupPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $startupConfig.runtime_root = [IO.Path]::GetFullPath($startupConfig.runtime_root)
    if ($env:COMPUTERNAME -ne $startupConfig.computer_name) { throw '此启动配置不属于本机。' }
    if ((Get-ExecutionPolicy) -ne 'RemoteSigned') { throw '执行策略不是部署时核准的RemoteSigned；本脚本不修改或绕过策略。' }
    $startupUri = [Uri]$startupConfig.api_base
    if (-not $startupUri.IsLoopback -or $startupUri.Scheme -notin @('http','https') -or $startupUri.UserInfo) {
        throw '启动API必须是无凭据的本机回环地址。'
    }
    foreach ($required in @($startupConfig.password_file, (Join-Path $startupConfig.runtime_root 'ScpCv.ControlHost.exe'))) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "部署依赖不存在：$required" }
    }
    if (@($startupConfig.tasks).Count -ne 2) { throw '部署配置必须恰有后端与网页两个任务。' }
    $startupWasRunning = @{}
    foreach ($definition in $startupConfig.tasks) {
        if ($definition.name -notmatch '^ScpCv(Runtime|Frontend)-') { throw '拒绝非本项目任务。' }
        $task = Get-ScheduledTask -TaskName $definition.name
        $startupWasRunning[$definition.name] = ($task.State -eq 'Running')
        $identity = (@($task.Actions | ForEach-Object { $_.Execute+'|'+$_.Arguments+'|'+$_.WorkingDirectory }) -join "`n")
        if ((Get-StartupDigest $identity) -ne $definition.action_sha256 -or $task.State -eq 'Disabled') {
            throw "任务已变化或被禁用：$($definition.name)；请核对部署记录。"
        }
        if ((Get-FileHash -LiteralPath $definition.script -Algorithm SHA256).Hash -ne $definition.script_sha256) {
            throw '任务启动脚本摘要变化，请维护者核对。'
        }
    }
    if ($CheckOnly) { Write-Host '配置、依赖与任务身份检查通过；未启动服务。'; $startupExit=0; return }
    $startupMutex = New-Object Threading.Mutex($false, ('Global\ScpCvStartup-'+(Get-StartupDigest $startupPath)))
    try { $startupLocked = $startupMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $startupLocked=$true }
    if (-not $startupLocked) { throw '另一次启动正在执行，请等待其结果；没有重复启动。' }
    foreach ($definition in $startupConfig.tasks) {
        $task = Get-ScheduledTask -TaskName $definition.name
        if ($task.State -ne 'Running') { Start-ScheduledTask -TaskName $definition.name }
    }
    $startupDeadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    do {
        try { $response = Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 ($startupConfig.api_base+'/health/ready'); $ready=($response.StatusCode -eq 200 -and $response.Content -eq 'Healthy') }
        catch { $ready=$false }
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    } while (-not $ready -and [DateTimeOffset]::UtcNow -lt $startupDeadline)
    if (-not $ready) { throw '后端未就绪，请检查数据目录中的control-host.err.log；未伪报启动成功。' }
    $startupHosts = @(Get-CimInstance Win32_Process -Filter "Name='ScpCv.ControlHost.exe'")
    if ($startupHosts.Count -ne 1 -or $startupHosts[0].ExecutablePath -ne (Join-Path $startupConfig.runtime_root 'ScpCv.ControlHost.exe') -or
        $startupHosts[0].SessionId -ne $startupConfig.session_id) { throw '后端进程身份或数量不符。' }
    $startupApi = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $startupHeaders = @{Origin=$startupConfig.api_base}
    $startupSecret = [IO.File]::ReadAllText($startupConfig.password_file).TrimEnd("`r", "`n")
    try {
        $null = Invoke-WebRequest -UseBasicParsing -TimeoutSec 15 -Method Post ($startupConfig.api_base+'/api/auth/login/') -WebSession $startupApi -Headers $startupHeaders -ContentType 'application/json' -Body (@{username=$startupConfig.username;password=$startupSecret}|ConvertTo-Json)
    } finally { $startupSecret=$null }
    $startupHeaders['X-CSRFToken'] = (Invoke-StartupApi '/api/auth/csrf/').csrfToken
    $startupMembers = @(Get-StartupMembers)
    $startupSessions = @((Invoke-StartupApi '/api/sessions/').sessions)
    $startupRuntimeTask = @($startupConfig.tasks | Where-Object name -like 'ScpCvRuntime-*')[0]
    $startupRuntimeInfo = Get-ScheduledTaskInfo -TaskName $startupRuntimeTask.name
    $startupCanResume = ($startupWasRunning[$startupRuntimeTask.name] -and
        ([DateTime]::Now-$startupRuntimeInfo.LastRunTime).TotalSeconds -gt 120)
    if ($startupCanResume -and $startupMembers.Count -eq 0 -and @($startupSessions | Where-Object player_online).Count -eq 0) {
        # 只恢复已停止的空运行组；在线或部分存活的组绝不借一键启动重启。
        $startupReply = Invoke-StartupApi '/api/system/restart/' 'Post'
        if (-not $startupReply.success) { throw '运行组启动未受理。' }
    }
    $startupDeadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    do {
        $startupMembers = @(Get-StartupMembers)
        $startupSessions = @((Invoke-StartupApi '/api/sessions/').sessions)
        $startupOnline = ($startupMembers.Count -eq 4 -and @($startupMembers | Where-Object Name -eq 'ScpCv.PlayerWorker.exe').Count -eq 2 -and
            $startupSessions.Count -eq 2 -and @($startupSessions | Where-Object { -not $_.player_online }).Count -eq 0)
        if (-not $startupOnline) { Start-Sleep -Milliseconds 500 }
    } while (-not $startupOnline -and [DateTimeOffset]::UtcNow -lt $startupDeadline)
    if (-not $startupOnline) { throw '两播放器未就绪；已有部分组未自动重启，请检查运行组状态与日志。' }
    $startupWeb = Invoke-WebRequest -UseBasicParsing -TimeoutSec 10 $startupConfig.web_url
    if ($startupWeb.StatusCode -ne 200 -or $startupWeb.Content -notmatch '<html') { throw '网页未通过正常TLS访问检查。' }
    Write-Host "SCP-cv已就绪：$($startupConfig.web_url)；两播放器在线。重复启动未重启已有组。" -ForegroundColor Green
    $startupExit=0
} catch { Write-Host ('启动失败：'+$_.Exception.Message) -ForegroundColor Red }
finally {
    if ($startupApi -and $startupHeaders['X-CSRFToken']) {
        try { $null=Invoke-StartupApi '/api/auth/logout/' 'Post' } catch { Write-Warning '启动验收会话注销失败，将按会话期限失效。' }
    }
    if ($startupLocked) { $startupMutex.ReleaseMutex() }
    if ($startupMutex) { $startupMutex.Dispose() }
    if (-not $NoPause) { $null=Read-Host '按回车关闭' }
}
exit $startupExit
