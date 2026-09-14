<#
.SYNOPSIS
  以无头方式运行 ControlHost（隐藏窗口 + 日志落盘），可选一并拉起受管 Worker。

.DESCRIPTION
  “无头”指不在交互桌面上弹出控制台窗口：ControlHost 以隐藏窗口启动，
  stdout/stderr 分别写入 DataRoot 下的 control-host.out.log / control-host.err.log。
  注意：PlayerWorker 的四块播放窗口本身就是播放输出，属于产品功能，不会被隐藏。
#>
[CmdletBinding(DefaultParameterSetName = 'Start')]
param(
    [Parameter(ParameterSetName = 'Stop')][switch]$Stop,
    [string]$RuntimeRoot = '',
    [string]$DataRoot = '',
    [string]$ListenUrls = 'https://localhost:18443',
    [string]$AllowedOrigins = 'https://localhost',
    [ValidateSet('Simulation', 'Hardware')][string]$SafetyMode = 'Simulation',
    [Nullable[bool]]$CrossSiteCookies = $null,
    [string]$ControlHostPath = '',
    [string]$MediaMtxPath = '',
    [string]$SupervisorExecutable = '',
    [string]$DevelopmentUsername = 'qa-admin',
    [string]$DevelopmentPassword = '',
    [string]$DevelopmentPasswordFile = '',
    [switch]$StartWorkers,
    [switch]$Detach,
    [int]$ReadyTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'

function Get-AllowedOriginList {
    param([string]$Value)
    $items = @($Value -split '[,;]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($items.Count -eq 0) { throw '必须至少配置一个 -AllowedOrigins。' }
    return @($items | Select-Object -Unique)
}

function Resolve-DevelopmentPassword {
    param(
        [string]$Explicit,
        [string]$FilePath
    )
    $value = ''
    if (-not [string]::IsNullOrWhiteSpace($FilePath)) {
        $resolved = [System.IO.Path]::GetFullPath($FilePath)
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "开发账号口令文件不存在：$resolved"
        }
        $value = [System.IO.File]::ReadAllText($resolved).TrimEnd("`r", "`n")
    }
    elseif (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        $value = $Explicit
    }
    else {
        $value = [Environment]::GetEnvironmentVariable('SCP_CV_DEVELOPMENT_PASSWORD')
    }
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw '未提供开发账号口令：请使用 -DevelopmentPasswordFile，或设置 SCP_CV_DEVELOPMENT_PASSWORD。'
    }
    return $value
}

function Write-GeneratedPasswordFile {
    param(
        [string]$Path,
        [string]$Password
    )
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $parent = Split-Path -Parent $fullPath
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    [System.IO.File]::WriteAllText($fullPath, $Password + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false)))
    $identity = "$env:USERDOMAIN\$env:USERNAME"
    & icacls.exe $fullPath /inheritance:r /grant:r "${identity}:(F)" 'SYSTEM:(F)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "无法为开发账号口令文件设置 ACL：$fullPath" }
    return $fullPath
}

function Escape-PowerShellSingleQuoted {
    param([string]$Value)
    return $Value.Replace("'", "''")
}

function Get-ProbeBaseUrl {
    param([string]$BaseUrl)
    $value = $BaseUrl -replace '^(https?://)0\.0\.0\.0(?=[:/])', '${1}127.0.0.1'
    return ($value -replace '^(https?://)(\[::\]|::)(?=[:/])', '${1}[::1]')
}

$originList = Get-AllowedOriginList $AllowedOrigins
$listenBaseUrl = $ListenUrls.Split(';')[0].Trim().TrimEnd('/')
if ([string]::IsNullOrWhiteSpace($listenBaseUrl)) { throw '必须至少配置一个 -ListenUrls。' }
$probeBaseUrl = Get-ProbeBaseUrl $listenBaseUrl
$httpOrigin = $originList | Where-Object { $_ -match '^https?://' } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($httpOrigin)) { $httpOrigin = $probeBaseUrl }

# Windows PowerShell 5.1 没有 Invoke-WebRequest -SkipCertificateCheck；
# 仅当目标是本机 https（自签证书）时放宽校验回调，避免脚本在旧版 PowerShell 上直接失败。
$isHttps = $ListenUrls.TrimStart().StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)
$supportsSkip = (Get-Command Invoke-WebRequest).Parameters.ContainsKey('SkipCertificateCheck')
if ($isHttps -and -not $supportsSkip) {
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}
# CrossSiteCookies=true 会让会话 Cookie 带 Secure 标记，HTTP 下客户端不会回传；
# 未显式指定时按监听协议决定。
if ($null -eq $CrossSiteCookies) { $CrossSiteCookies = $isHttps }

function Invoke-ScpCvWeb {
    param([string]$Uri, [string]$Method = 'GET', $WebSession = $null, [hashtable]$Headers = $null, [string]$ContentType = '', [string]$Body = '')
    $splat = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true; TimeoutSec = 15 }
    if ($supportsSkip) { $splat['SkipCertificateCheck'] = $true }
    if ($WebSession) { $splat['WebSession'] = $WebSession }
    if ($Headers) { $splat['Headers'] = $Headers }
    if ($ContentType) { $splat['ContentType'] = $ContentType }
    if ($Body) { $splat['Body'] = $Body }
    return Invoke-WebRequest @splat
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path
}
if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    throw '必须显式传入 -DataRoot，避免无头进程误用其他运行时的数据目录。'
}
$dataPath = [System.IO.Path]::GetFullPath($DataRoot)
New-Item -ItemType Directory -Force -Path $dataPath | Out-Null
$scriptLog = Join-Path $dataPath 'run-headless.log'

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $Message
    Write-Host $line
    # 分离启动时本进程没有控制台，日志必须同时落盘才能回看。
    Add-Content -LiteralPath $scriptLog -Value $line -Encoding UTF8
}

function Get-HeadlessControlHost {
    # 注意：Windows PowerShell 5.1 基于 .NET Framework，没有
    # String.Contains(string, StringComparison) 重载，必须用 IndexOf。
    Get-CimInstance Win32_Process -Filter "Name='ScpCv.ControlHost.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            ($_.CommandLine.IndexOf($dataPath, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        }
}

function Get-HeadlessTaskName {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($dataPath.ToLowerInvariant()))
    }
    finally { $sha.Dispose() }
    return 'ScpCvHeadless-' + (($hash[0..3] | ForEach-Object { $_.ToString('x2') }) -join '')
}

if ($Stop) {
    $targets = @(Get-HeadlessControlHost)
    if ($targets.Count -eq 0) {
        Write-Log "DataRoot 下没有运行中的 ControlHost：$dataPath"
    }
    foreach ($target in $targets) {
        Stop-Process -Id $target.ProcessId -Force
        Write-Log "已停止 ControlHost pid=$($target.ProcessId)"
    }
    $taskName = Get-HeadlessTaskName
    schtasks /delete /tn $taskName /f 2>$null | Out-Null
    Write-Log "已清理计划任务 $taskName"
    return
}

# -Detach：用一次性计划任务重新拉起自身。SSH 会话关闭会回收会话内的进程树，
# 计划任务实例不属于该进程树，因此 SSH 可以立刻返回而 ControlHost 继续运行。
# 任务保留用于后续手动运行；-Stop 会连同任务一起清理。
if ($Detach) {
    # schtasks /tr 上限 261 字符，且内层引号会被参数传递破坏；
    # 因此先落一个启动器脚本，任务只指向它。
    $launcher = Join-Path $dataPath 'headless-launch.ps1'
    $detachedPassword = Resolve-DevelopmentPassword -Explicit $DevelopmentPassword -FilePath $DevelopmentPasswordFile
    if ([string]::IsNullOrWhiteSpace($DevelopmentPasswordFile)) {
        $DevelopmentPasswordFile = Write-GeneratedPasswordFile -Path (Join-Path $dataPath 'headless-secret.txt') -Password $detachedPassword
    }
    else {
        $DevelopmentPasswordFile = [System.IO.Path]::GetFullPath($DevelopmentPasswordFile)
    }
    $qRuntimeRoot = Escape-PowerShellSingleQuoted $RuntimeRoot
    $qDataRoot = Escape-PowerShellSingleQuoted $dataPath
    $qListenUrls = Escape-PowerShellSingleQuoted $ListenUrls
    $qAllowedOrigins = Escape-PowerShellSingleQuoted $AllowedOrigins
    $qSafetyMode = Escape-PowerShellSingleQuoted $SafetyMode
    $qPasswordFile = Escape-PowerShellSingleQuoted $DevelopmentPasswordFile
    $qControlHostPath = Escape-PowerShellSingleQuoted $ControlHostPath
    $qSupervisorExecutable = Escape-PowerShellSingleQuoted $SupervisorExecutable
    $qMediaMtxPath = Escape-PowerShellSingleQuoted $MediaMtxPath
    # 用 splat 生成启动器：多行调用需要续行符，splat 更不易写坏。
    $lines = @(
        '$params = @{'
        ("    RuntimeRoot = '" + $qRuntimeRoot + "'")
        ("    DataRoot = '" + $qDataRoot + "'")
        ("    ListenUrls = '" + $qListenUrls + "'")
        ("    AllowedOrigins = '" + $qAllowedOrigins + "'")
        ("    SafetyMode = '" + $qSafetyMode + "'")
        ("    CrossSiteCookies = $" + $CrossSiteCookies.ToString().ToLowerInvariant())
        ("    DevelopmentUsername = '" + $DevelopmentUsername + "'")
        ("    DevelopmentPasswordFile = '" + $qPasswordFile + "'")
    )
    if (-not [string]::IsNullOrWhiteSpace($ControlHostPath)) { $lines += "    ControlHostPath = '" + $qControlHostPath + "'" }
    if (-not [string]::IsNullOrWhiteSpace($SupervisorExecutable)) { $lines += "    SupervisorExecutable = '" + $qSupervisorExecutable + "'" }
    if (-not [string]::IsNullOrWhiteSpace($MediaMtxPath)) { $lines += "    MediaMtxPath = '" + $qMediaMtxPath + "'" }
    if ($StartWorkers) { $lines += '    StartWorkers = $true' }
    $lines += "    ReadyTimeoutSeconds = $ReadyTimeoutSeconds"
    $lines += '}'
    $lines += '& ' + "'" + $PSCommandPath + "'" + ' @params'
    [System.IO.File]::WriteAllText(
        $launcher,
        ($lines -join [Environment]::NewLine) + [Environment]::NewLine,
        (New-Object System.Text.UTF8Encoding($true)))

    $action = (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') +
        ' -NoProfile -ExecutionPolicy Bypass -File ' + $launcher
    if ($action.Length -gt 261) { throw "计划任务动作超过 261 字符：$action" }
    $taskName = Get-HeadlessTaskName
    $created = schtasks /create /tn $taskName /tr $action /sc once /st 23:59 /f 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Write-Log $created.Trim(); throw "创建计划任务 $taskName 失败。" }
    $ran = schtasks /run /tn $taskName 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Write-Log $ran.Trim(); throw "运行计划任务 $taskName 失败。" }
    Write-Log "已通过计划任务分离启动：$taskName（启动器 $launcher），日志：$scriptLog"
    return
}

# 幂等守卫：同一 DataRoot 已有 ControlHost 时不再重复启动（计划任务可能被再次触发）。
$running = @(Get-HeadlessControlHost)
if ($running.Count -gt 0) {
    Write-Log "DataRoot 下已有 ControlHost 运行（pid=$($running[0].ProcessId)），跳过启动。"
    return
}

if ([string]::IsNullOrWhiteSpace($ControlHostPath)) {
    $candidates = @(
        (Join-Path $RuntimeRoot 'ScpCv.ControlHost\bin\Debug\net10.0\ScpCv.ControlHost.exe')
        (Join-Path $RuntimeRoot 'ScpCv.ControlHost\bin\Release\net10.0\ScpCv.ControlHost.exe')
    )
    $exe = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $exe) {
        # 兼容自包含发布布局：任意深度的 ScpCv.ControlHost 目录下的可执行文件。
        $exe = Get-ChildItem -LiteralPath $RuntimeRoot -Filter 'ScpCv.ControlHost.exe' -Recurse -File -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
    }
}
else {
    $exe = [System.IO.Path]::GetFullPath($ControlHostPath)
}
if ([string]::IsNullOrWhiteSpace($exe) -or -not (Test-Path -LiteralPath $exe)) {
    throw "未找到 ControlHost（RuntimeRoot=$RuntimeRoot）。先 dotnet build/publish，或用 -ControlHostPath 指定。"
}
$exe = [System.IO.Path]::GetFullPath($exe)
$DevelopmentPassword = Resolve-DevelopmentPassword -Explicit $DevelopmentPassword -FilePath $DevelopmentPasswordFile

$outLog = Join-Path $dataPath 'control-host.out.log'
$errLog = Join-Path $dataPath 'control-host.err.log'

$arguments = @(
    '--urls=' + $ListenUrls
    '--SafetyMode=' + $SafetyMode
    '--DataRoot=' + $dataPath
    '--Authentication:CrossSiteCookies=' + $CrossSiteCookies.ToString().ToLowerInvariant()
    '--Authentication:DevelopmentAccount:Username=' + $DevelopmentUsername
    '--Authentication:DevelopmentAccount:IsStaff=true'
    '--Authentication:DevelopmentAccount:IsSuperuser=true'
)
for ($i = 0; $i -lt $originList.Count; $i++) {
    $arguments += '--Authentication:AllowedOrigins:{0}={1}' -f $i, $originList[$i]
}

if (-not [string]::IsNullOrWhiteSpace($SupervisorExecutable)) {
    $arguments += '--Supervisor:ExecutablePath=' + ([System.IO.Path]::GetFullPath($SupervisorExecutable))
    $arguments += '--Supervisor:RuntimeRoot=' + $RuntimeRoot
    $arguments += '--Supervisor:StatePath=' + (Join-Path $dataPath 'runtime-processes.json')
}
if (-not [string]::IsNullOrWhiteSpace($MediaMtxPath)) {
    $arguments += '--Supervisor:MediaMtxPath=' + ([System.IO.Path]::GetFullPath($MediaMtxPath))
}

$passwordEnvironmentName = 'Authentication__DevelopmentAccount__Password'
$previousPasswordEnvironment = [Environment]::GetEnvironmentVariable($passwordEnvironmentName, 'Process')
[Environment]::SetEnvironmentVariable($passwordEnvironmentName, $DevelopmentPassword, 'Process')
try {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog
}
finally {
    if ($null -eq $previousPasswordEnvironment) {
        Remove-Item "Env:$passwordEnvironmentName" -ErrorAction SilentlyContinue
    }
    else {
        [Environment]::SetEnvironmentVariable($passwordEnvironmentName, $previousPasswordEnvironment, 'Process')
    }
}
Write-Log "ControlHost 已无头启动 pid=$($process.Id)"
Write-Log "日志：$outLog"

$ready = $false
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReadyTimeoutSeconds)
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    if ($process.HasExited) { throw "ControlHost 提前退出，退出码 $($process.ExitCode)；见 $errLog" }
    try {
        $response = Invoke-ScpCvWeb -Uri ($probeBaseUrl + '/health/ready')
        if ($response.StatusCode -eq 200) { $ready = $true; break }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}
if (-not $ready) { throw "ControlHost 未在 $ReadyTimeoutSeconds 秒内就绪；见 $outLog" }
Write-Log 'ControlHost /health/ready = 200'

if (-not $StartWorkers) {
    Write-Log '未指定 -StartWorkers，跳过 Worker 编排。'
    return
}

$baseUrl = $probeBaseUrl
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$headers = @{ Origin = $httpOrigin }
$csrf = (Invoke-ScpCvWeb -Uri "$baseUrl/api/auth/csrf/" -WebSession $session -Headers $headers).Content | ConvertFrom-Json | Select-Object -ExpandProperty csrfToken
Invoke-ScpCvWeb -Method Post -Uri "$baseUrl/api/auth/login/" -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body (@{ username = $DevelopmentUsername; password = $DevelopmentPassword } | ConvertTo-Json) | Out-Null
$headers['X-CSRFToken'] = $csrf
$launch = (Invoke-ScpCvWeb -Method Post -Uri "$baseUrl/api/system/restart/" -WebSession $session -Headers $headers).Content | ConvertFrom-Json
Write-Log ("Worker 编排：group_epoch={0} detail={1}" -f $launch.group_epoch, $launch.detail)
