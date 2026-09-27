<#
.SYNOPSIS
  在真实双窗工作站反复开关短视频，检测 PlayerWorker 的句柄持续增长。

.DESCRIPTION
  这是有播放副作用的实机测试。运行前准备一个约 4 秒的本地视频源，
  明确传入 DataRoot、源 ID、口令文件和现场备注；不会操作窗口 3/4 或设备电源。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [Parameter(Mandatory = $true)][string]$DataRoot,
    [Parameter(Mandatory = $true)][long]$SourceId,
    [ValidateRange(1, 2)][int]$WindowId = 1,
    [Parameter(Mandatory = $true)][string]$PasswordFile,
    [string]$Username = 'qa-admin',
    [ValidateRange(4, 1000)][int]$Cycles = 20,
    [ValidateRange(0, 120)][int]$IdleSeconds = 15,
    [ValidateRange(0, 1000)][int]$MaxHandleGrowth = 30,
    [Parameter(Mandatory = $true)][string]$HardwareNote,
    [string]$OutputPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($HardwareNote)) { throw '必须填写实机测试备注。' }
if (-not (Test-Path -LiteralPath $PasswordFile -PathType Leaf)) { throw '口令文件不存在。' }
$statePath = Join-Path $DataRoot 'runtime-processes.json'
if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { throw '运行组状态文件不存在。' }
$members = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
$player = @($members | Where-Object { $_.role -eq "player-$WindowId" })
if ($player.Count -ne 1 -or $player[0].sessionId -ne 1) { throw '目标播放器不是唯一的交互会话进程。' }
$playerId = [int]$player[0].processId
$playerStart = (Get-Process -Id $playerId).StartTime

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$origin = ([Uri]$BaseUrl).GetLeftPart([System.UriPartial]::Authority)
$headers = @{ Origin = $origin }
$csrf = (Invoke-RestMethod -Uri "$BaseUrl/api/auth/csrf/" -WebSession $session -Headers $headers).csrfToken
$password = (Get-Content -LiteralPath $PasswordFile -Raw).TrimEnd([char]13, [char]10)
try {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login/" -WebSession $session -Headers $headers `
        -ContentType 'application/json' -Body (@{ username = $Username; password = $password } | ConvertTo-Json) | Out-Null
}
finally { $password = $null }
$headers['X-CSRFToken'] = $csrf

function Get-Session {
    return (Invoke-RestMethod -Uri "$BaseUrl/api/sessions/$WindowId/" -WebSession $session).session
}

function Wait-Session {
    param([bool]$Playing)
    $deadline = (Get-Date).AddSeconds(10)
    do {
        $state = Get-Session
        $sourceMatches = if ($Playing) { $state.source_id -eq $SourceId } else { $null -eq $state.source_id }
        $statusMatches = if ($Playing) { $state.playback_state -eq 'playing' } else { $state.playback_state -eq 'idle' }
        if ($sourceMatches -and $statusMatches -and -not $state.pending_command) { return }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    throw "窗口 $WindowId 未在 10 秒内进入预期播放状态（Playing=$Playing）。"
}

function Get-ResourceSample {
    param([int]$Cycle)
    $process = Get-Process -Id $playerId
    if ($process.StartTime -ne $playerStart) { throw '播放器 PID 已重启，样本无效。' }
    return [pscustomobject]@{
        cycle = $Cycle
        private_mb = [Math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        handles = $process.Handles
    }
}

$initial = Get-Session
if ($null -ne $initial.source_id -or $initial.playback_state -ne 'idle') {
    throw '目标窗口必须先处于 idle；本测试不会覆盖既有播出。'
}
$samples = [System.Collections.Generic.List[object]]::new()
$samples.Add((Get-ResourceSample 0))
try {
    for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/playback/$WindowId/open/" -WebSession $session `
            -Headers $headers -ContentType 'application/json' -Body (@{ source_id = $SourceId; autoplay = $true } | ConvertTo-Json) | Out-Null
        Wait-Session $true
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/playback/$WindowId/close/" -WebSession $session `
            -Headers $headers -ContentType 'application/json' -Body '{}' | Out-Null
        Wait-Session $false
        if ($cycle -eq 2 -or $cycle % 2 -eq 0) { $samples.Add((Get-ResourceSample $cycle)) }
    }
}
finally {
    if ((Get-Session).source_id -eq $SourceId) {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/playback/$WindowId/close/" -WebSession $session `
            -Headers $headers -ContentType 'application/json' -Body '{}' | Out-Null
    }
}
if ($IdleSeconds -gt 0) { Start-Sleep -Seconds $IdleSeconds }
$final = Get-ResourceSample $Cycles
$warm = @($samples | Where-Object { $_.cycle -eq 2 })[0]
$growth = $final.handles - $warm.handles
$result = [ordered]@{
    hardware_note = $HardwareNote
    window_id = $WindowId
    source_id = $SourceId
    player_pid = $playerId
    cycles = $Cycles
    handle_growth_after_warmup = $growth
    max_handle_growth = $MaxHandleGrowth
    idle_seconds = $IdleSeconds
    final = $final
    samples = @($samples)
    verdict = if ($growth -le $MaxHandleGrowth) { '通过' } else { '未通过' }
}
$json = $result | ConvertTo-Json -Depth 6
if ($OutputPath) { $json | Set-Content -LiteralPath $OutputPath -Encoding UTF8 }
$json
if ($growth -gt $MaxHandleGrowth) { throw "播放器句柄增长 $growth，超过阈值 $MaxHandleGrowth。" }
