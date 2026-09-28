<#
.SYNOPSIS
  两窗普通命令开始执行基准，或显式开启的健康热切换完成基准。

.DESCRIPTION
  通过真实 ControlHost 提交 N 条窗口音量命令，默认在大屏窗口 1/2 间轮询并留出提交间隔，
  避免同目标“后到意图覆盖先前意图”的折叠把样本吃掉。完成后从命令表读取
  CreatedAt → StartedAt 的真实间隔作为“开始执行”延迟。
  普通模式要求独占测试目标的控制入口；逐个关联本请求的唯一SET_VOLUME命令，
  并发目标命令使关联无效。最终仅统计自有ID，全样本真实Completed且零折叠/失败/未完成才通过。

  必须连接 SafetyMode=Hardware 且 Supervisor 已拉起真实 Worker 的主机；simulation 没有
  Worker 认领命令，本脚本会直接判定测量无效。

  -Mode HotSwitch 必须显式提供至少两个不同的 -SourceIds 和 -RawSamplesPath；默认100次，
  串行在窗口1/2间轮询，每个窗口前后异源。只在当前会话健康且实验性PowerPoint关闭时测量。
  HTTP受理不等于完成：必须核对唯一OPEN、完成摘要、Worker身份、代次及实际源；首错停止。
  延迟为命令创建至控制完成（非画面验收），实体稳定帧仍须另外截图或采样。
  不启动/停机、不改系统音量或墙面；结束保留最后媒体，调用者负责恢复测试前状态。

.EXAMPLE
  .\benchmark-commands.ps1 -Mode HotSwitch -BaseUrl http://localhost:18443 -DatabasePath D:\qa\control.db `
    -PasswordFile D:\qa\private\password.txt -SourceIds 39,40,48,42,45 -Samples 100 `
    -RawSamplesPath D:\qa\hot-switch.csv -OutputPath D:\qa\hot-switch.md
#>
[CmdletBinding()]
param(
    [ValidateSet('Ordinary', 'HotSwitch')][string]$Mode = 'Ordinary',
    [string]$BaseUrl = 'http://localhost:18444',
    [string]$DatabasePath = '',
    [string]$Username = 'qa-admin',
    [string]$PasswordFile = '',
    [string]$SqliteExecutable = 'sqlite3',
    [int]$Samples = 1000,
    [int[]]$Targets = @(1, 2),
    [long[]]$SourceIds = @(),
    [int]$SwitchTimeoutSeconds = 30,
    [int]$PollMilliseconds = 100,
    [int]$DelayMilliseconds = 50,
    [int]$DrainTimeoutSeconds = 180,
    [double]$MaxSupersededRatio = 0.2,
    [string]$HardwareNote = '',
    [string]$OutputPath = '',
    [string]$RawSamplesPath = ''
)

$ErrorActionPreference = 'Stop'

if ($Mode -eq 'HotSwitch' -and -not $PSBoundParameters.ContainsKey('Samples')) { $Samples = 100 }
if ($Samples -le 0) { throw '样本数必须大于 0。' }
$targetList = @($Targets | Select-Object -Unique | Sort-Object)
if ($targetList.Count -eq 0) { throw '至少指定一个大屏窗口。' }
foreach ($target in $targetList) {
    if ($target -lt 1 -or $target -gt 2) { throw "窗口号必须在 1..2：$target" }
}
if ($DelayMilliseconds -lt 0 -or $SwitchTimeoutSeconds -lt 1 -or $PollMilliseconds -lt 20) {
    throw '延迟不得为负，切换超时至少1秒，轮询间隔至少20毫秒。'
}
if ($Mode -eq 'HotSwitch') {
    $uniqueSources = @($SourceIds | Select-Object -Unique)
    if ($uniqueSources.Count -lt 2 -or @($SourceIds | Where-Object { $_ -le 0 }).Count -gt 0) {
        throw '健康热切换必须显式指定至少两个不同的正数 -SourceIds。'
    }
    $SourceIds = $uniqueSources
    if ([string]::IsNullOrWhiteSpace($RawSamplesPath)) { throw '健康热切换必须提供 -RawSamplesPath 保存逐样本CSV。' }
}
if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
    throw '必须显式传入 -DatabasePath，避免误读其他运行时的命令表。'
}
if (-not (Test-Path -LiteralPath $DatabasePath)) {
    throw "命令表不存在：$DatabasePath"
}
$protectedPaths = @([IO.Path]::GetFullPath($DatabasePath))
if ($PasswordFile) { $protectedPaths += [IO.Path]::GetFullPath($PasswordFile) }
foreach ($evidencePath in @($RawSamplesPath, $OutputPath) | Where-Object { $_ }) {
    if ([IO.Path]::GetFullPath($evidencePath) -in $protectedPaths) {
        throw '证据输出不得覆盖数据库或口令文件。'
    }
}
if ($RawSamplesPath -and $OutputPath -and [IO.Path]::GetFullPath($RawSamplesPath) -eq [IO.Path]::GetFullPath($OutputPath)) {
    throw '原始CSV与报告必须使用不同路径。'
}

$resolvedSqlite = Get-Command -Name $SqliteExecutable -ErrorAction SilentlyContinue
if ($null -eq $resolvedSqlite) {
    throw "未找到 sqlite3 可执行文件：$SqliteExecutable；可通过 -SqliteExecutable 指定忽略目录中的工具。"
}
if (-not [string]::IsNullOrWhiteSpace($PasswordFile)) {
    if (-not (Test-Path -LiteralPath $PasswordFile -PathType Leaf)) {
        throw "开发账号口令文件不存在：$PasswordFile"
    }
    $Password = (Get-Content -LiteralPath $PasswordFile -Raw).TrimEnd("`r", "`n")
}
else {
    $Password = [Environment]::GetEnvironmentVariable('SCP_CV_DEVELOPMENT_PASSWORD')
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw '未提供开发账号口令：使用 -PasswordFile 或 SCP_CV_DEVELOPMENT_PASSWORD。'
}

function Get-Percentile {
    <# :param Values: 延迟样本。 :param Percentile: 百分位。 :returns: 最近秩百分位，无样本返回null。 #>
    param([double[]]$Values, [double]$Percentile)
    if ($Values.Count -eq 0) { return $null }
    $sorted = $Values | Sort-Object
    $rank = [Math]::Ceiling($Percentile / 100 * $sorted.Count) - 1
    if ($rank -lt 0) { $rank = 0 }
    if ($rank -ge $sorted.Count) { $rank = $sorted.Count - 1 }
    return [double]$sorted[$rank]
}

function Invoke-Sqlite {
    <# :param Sql: 只读诊断查询。 :returns: sqlite3标准输出；查询失败明确抛错。 #>
    param([string]$Sql)
    $result = & $resolvedSqlite.Source -readonly $DatabasePath $Sql
    if ($LASTEXITCODE -ne 0) { throw "sqlite3 查询失败：$Sql" }
    return $result
}

function Get-HotSnapshot {
    <# :param WindowId: 仅1/2。 :param AfterId: 请求前命令边界。 :param Generation: 请求预期代次。 :returns: 一次只读查询的一致快照。 #>
    param([int]$WindowId, [long]$AfterId = 0, [long]$Generation = 0)
    $commandFields = ''
    $commandJoin = ''
    $tag = 'hot_baseline'
    if ($Generation -gt 0) {
        $tag = 'hot_sample'
        $commandJoin = "LEFT JOIN command_records c ON c.TargetKind = 'Display' AND c.TargetId = $WindowId AND c.SourceGeneration = $Generation AND c.Command = 'OPEN' AND c.Id > $AfterId"
        $commandFields = @"
,'command_count',(SELECT COUNT(*) FROM command_records q WHERE q.TargetKind='Display' AND q.TargetId=$WindowId AND q.Id>$AfterId)
,'command_id',c.Id,'command_guid',c.CommandId,'command_generation',c.SourceGeneration,'command_source_id',json_extract(c.ArgsJson,'$.source_id')
,'status',c.Status,'created_ticks',c.CreatedAt,'started_ticks',c.StartedAt,'completed_ticks',c.CompletedAt
,'result_code',c.ResultCode,'result_hash',c.ResultHash,'result_evidence_json',c.ResultEvidenceJson
,'consumer_instance_id',c.ConsumerInstanceId,'command_owner_epoch',c.OwnerEpoch
"@
    }
    $sql = @"
/* $tag */ SELECT json_object('after_id',(SELECT COALESCE(MAX(Id),0) FROM command_records),
'desired_generation',s.DesiredGeneration,'observed_generation',s.ObservedGeneration,
'source_id',s.MediaSourceId,'actual_source_id',s.ActualSourceId,'playback_state',s.PlaybackState,
'pending_command',s.PendingCommand,'error_message',s.ErrorMessage,'cleanup_pending',s.CleanupPending,
'worker_instance_id',w.WorkerInstanceId,'worker_pid',w.ProcessId,'worker_start_ticks',w.ProcessStartTime,
'owner_epoch',w.OwnerEpoch,'worker_status',w.Status,'group_state',r.State $commandFields)
FROM playback_sessions s LEFT JOIN worker_ownerships w ON w.TargetKind='Display' AND w.TargetId=s.WindowId
CROSS JOIN runtime_group_control r $commandJoin WHERE s.WindowId = $WindowId;
"@
    $rows = @(Invoke-Sqlite $sql)
    if ($rows.Count -ne 1) { throw 'snapshot_ambiguous：目标会话/命令不是唯一行。' }
    return $rows[0] | ConvertFrom-Json
}

function Test-HotSession {
    <# :param Snapshot: 数据库快照。 :param WindowId: HTTP目标。 :returns: 同时检查HTTP健康与持久投影；不把期望源当实际源。 #>
    param($Snapshot, [int]$WindowId)
    $api = (Invoke-RestMethod -Uri "$BaseUrl/api/sessions/$WindowId/" -WebSession $session -Headers $headers -TimeoutSec $SwitchTimeoutSeconds).session
    return $Snapshot.group_state -eq 'Armed' -and $Snapshot.worker_status -eq 'Online' -and
        $Snapshot.worker_pid -gt 0 -and $Snapshot.owner_epoch -gt 0 -and $Snapshot.worker_instance_id -and
        $Snapshot.desired_generation -eq $Snapshot.observed_generation -and $Snapshot.source_id -gt 0 -and
        $Snapshot.source_id -eq $Snapshot.actual_source_id -and $Snapshot.playback_state -eq 'Playing' -and
        -not $Snapshot.pending_command -and -not $Snapshot.error_message -and -not $Snapshot.cleanup_pending -and
        $api.window_id -eq $WindowId -and $api.source_id -eq $Snapshot.actual_source_id -and $api.player_online -and
        $api.playback_state -eq 'playing' -and -not $api.pending_command -and -not $api.error_message
}

function Invoke-HotSwitchBenchmark {
    <# :returns: 每次唯一OPEN的完成、身份和代次证据；首错停止继续发令并留下CSV。 #>
    $hostState = Invoke-RestMethod -Uri ($BaseUrl.TrimEnd('/') + '/') -WebSession $session -Headers $headers
    if ($hostState.safety_mode -ne 'hardware') { throw '热切换必须连接Hardware模式，Simulation不是实机证据。' }
    $settings = Invoke-RestMethod -Uri "$BaseUrl/api/settings/powerpoint/" -WebSession $session -Headers $headers
    if ($settings.settings.experimental_enabled -ne $false) { throw '健康热切换要求明确关闭实验性PowerPoint；脚本不会自行修改设置。' }
    $rows = [Collections.Generic.List[object]]::new()
    for ($index = 1; $index -le $Samples; $index++) {
        $windowId = $targetList[($index - 1) % $targetList.Count]
        $row = [ordered]@{sample=$index;window_id=$windowId;baseline_source_id=0;source_id=0;generation=0;command_id=$null;command_guid='';
            worker_instance_id='';worker_pid=0;worker_start_ticks=0;owner_epoch=0;created_ticks=$null;started_ticks=$null;
            completed_ticks=$null;http_ms=$null;started_after_ms=$null;completed_after_ms=$null;end_to_end_ms=$null;
            status='';desired_generation=0;observed_generation=0;actual_source_id=0;command_generation=0;command_source_id=0;
            consumer_instance_id='';command_owner_epoch=0;result_code='';result_hash='';result_evidence_json='';
            succeeded=$false;failure_code=''}
        $watch = [Diagnostics.Stopwatch]::StartNew()
        try {
            $baseline = Get-HotSnapshot $windowId
            if (-not (Test-HotSession $baseline $windowId)) { throw 'baseline_unhealthy' }
            $row.baseline_source_id = $baseline.actual_source_id
            $sourceIndex = [int][Math]::Floor(($index - 1) / $targetList.Count) % $SourceIds.Count
            if ($SourceIds[$sourceIndex] -eq $baseline.actual_source_id) { $sourceIndex = ($sourceIndex + 1) % $SourceIds.Count }
            $row.source_id = $SourceIds[$sourceIndex]
            $row.generation = [long]$baseline.desired_generation + 1
            foreach ($key in @('worker_instance_id','worker_pid','worker_start_ticks','owner_epoch')) { $row[$key] = $baseline.$key }
            $httpWatch = [Diagnostics.Stopwatch]::StartNew()
            try {
                $body = @{source_id=$row.source_id;autoplay=$true} | ConvertTo-Json -Compress
                $accepted = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/playback/$windowId/open/" -WebSession $session `
                    -Headers $headers -ContentType 'application/json' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec $SwitchTimeoutSeconds
                if (-not $accepted.success) { throw 'http_not_accepted' }
            }
            finally { $httpWatch.Stop(); $row.http_ms = [Math]::Round($httpWatch.Elapsed.TotalMilliseconds, 3) }
            $completionWatch = [Diagnostics.Stopwatch]::StartNew()
            do {
                $current = Get-HotSnapshot $windowId $baseline.after_id $row.generation
                foreach ($key in @('command_id','command_guid','status','desired_generation','observed_generation','actual_source_id',
                    'command_generation','command_source_id','consumer_instance_id','command_owner_epoch','result_code',
                    'result_hash','result_evidence_json','created_ticks','started_ticks','completed_ticks')) { $row[$key] = $current.$key }
                if ($current.command_count -gt 1) { throw 'concurrent_commands' }
                if ($current.desired_generation -gt $row.generation -or $current.observed_generation -gt $row.generation) { throw 'generation_changed' }
                if ($current.worker_instance_id -ne $baseline.worker_instance_id -or $current.worker_pid -ne $baseline.worker_pid -or
                    $current.worker_start_ticks -ne $baseline.worker_start_ticks -or $current.owner_epoch -ne $baseline.owner_epoch) { throw 'worker_identity_changed' }
                if ($current.status -in @('Superseded','Failed','Uncertain')) { throw ('command_' + $current.status.ToLowerInvariant()) }
                if ($current.consumer_instance_id -and ($current.consumer_instance_id -ne $baseline.worker_instance_id -or
                    $current.command_owner_epoch -ne $baseline.owner_epoch)) { throw 'worker_identity_changed' }
                if ($current.status -eq 'Completed') {
                    if (-not $current.command_id -or $current.command_generation -ne $row.generation -or
                        $current.command_source_id -ne $row.source_id -or -not $current.consumer_instance_id -or
                        $current.result_code -ne 'ok' -or -not $current.result_hash -or -not $current.started_ticks -or
                        -not $current.completed_ticks -or $current.started_ticks -lt $current.created_ticks -or
                        $current.completed_ticks -lt $current.started_ticks) { throw 'completion_evidence_missing' }
                    if ($current.desired_generation -eq $row.generation -and $current.observed_generation -eq $row.generation -and
                        $current.actual_source_id -eq $row.source_id -and (Test-HotSession $current $windowId)) {
                        $row.succeeded = $true
                        $row.started_after_ms = [Math]::Round(($current.started_ticks - $current.created_ticks) / 10000.0, 3)
                        $row.completed_after_ms = [Math]::Round(($current.completed_ticks - $current.created_ticks) / 10000.0, 3)
                        break
                    }
                }
                if ($completionWatch.Elapsed.TotalSeconds -ge $SwitchTimeoutSeconds) { throw 'completion_timeout' }
                Start-Sleep -Milliseconds $PollMilliseconds
            } while ($true)
        }
        catch { $row.failure_code = $_.Exception.Message }
        finally { $watch.Stop(); $row.end_to_end_ms = [Math]::Round($watch.Elapsed.TotalMilliseconds, 3) }
        $rows.Add([pscustomobject]$row)
        $rows | Export-Csv -LiteralPath $RawSamplesPath -NoTypeInformation -Encoding UTF8
        Write-Host ("热切换 {0}/{1}：窗口{2} 源{3} generation={4} healthy={5} {6}" -f $index,$Samples,$windowId,$row.source_id,$row.generation,$row.succeeded,$row.failure_code)
        if (-not $row.succeeded) { break }
        if ($DelayMilliseconds -gt 0) { Start-Sleep -Milliseconds $DelayMilliseconds }
    }
    $healthy = @($rows | Where-Object { $_.succeeded })
    $durations = @($healthy | ForEach-Object { [double]$_.completed_after_ms })
    $failures = $rows.Count - $healthy.Count
    $result = [ordered]@{mode='HotSwitch';samples=$Samples;attempted=$rows.Count;healthy_switches=$healthy.Count;failed=$failures;
        unattempted=$Samples-$rows.Count;targets=($targetList -join ',');sources=($SourceIds -join ',');minimum_met=($healthy.Count -ge 100);
        completed_p95_ms=(Get-Percentile $durations 95);completed_max_ms=($durations | Measure-Object -Maximum).Maximum;
        end_to_end_p95_ms=(Get-Percentile @($healthy | ForEach-Object { [double]$_.end_to_end_ms }) 95);
        verdict=$(if ($failures -eq 0 -and $healthy.Count -eq $Samples) {'控制样本通过（非画面验收）'} else {'未通过'})}
    $result | ConvertTo-Json | Write-Host
    if ($OutputPath) {
        @('# 健康热切换基准（非画面验收）','','实体稳定帧须另行截图/采样；不把HTTP受理、同源重开或Superseded算完成。',
            "采集时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')；ControlHost：$BaseUrl；命令表：$DatabasePath",
            "目标窗口：$($targetList -join '/')；源ID：$($SourceIds -join ',')；硬件/媒体条件：$HardwareNote",
            "轮询：$PollMilliseconds ms；逐次完成超时：$SwitchTimeoutSeconds s；提交间隔：$DelayMilliseconds ms；原始CSV：$RawSamplesPath",'',
            '| 指标 | 值 |','| --- | --- |',"| 请求样本 | $Samples |","| 已尝试 | $($rows.Count) |",
            "| 健康完成 | $($healthy.Count) |","| 失败数 | $failures |","| 未尝试 | $($result.unattempted) |",
            "| 创建至控制完成 p95 | $($result.completed_p95_ms) ms |","| 创建至控制完成最大 | $($result.completed_max_ms) ms |",
            "| 请求至完成观测 p95 | $($result.end_to_end_p95_ms) ms |","| 达到100健康样本 | $($result.minimum_met) |",'',
            "判定：$($result.verdict)") | Set-Content -LiteralPath $OutputPath -Encoding utf8
    }
    if ($failures -gt 0) { throw "健康热切换未通过：$failures 次失败；已停止继续发令，原始证据见 $RawSamplesPath。" }
}

function Get-OrdinarySamples {
    <# :param IdList: 本脚本逐次确认的数字命令ID列表。 :returns: 精确目标/命令/ID的持久结果，不统计全表并发流量。 #>
    param([string]$IdList)
    $sql = @"
/* ordinary_samples */ SELECT json_object('command_id',Id,'command_guid',CommandId,'window_id',TargetId,
'command',Command,'volume',json_extract(ArgsJson,'$.volume'),'status',Status,'created_ticks',CreatedAt,
'started_ticks',StartedAt,'completed_ticks',CompletedAt,'result_code',ResultCode,'result_hash',ResultHash,
'consumer_instance_id',ConsumerInstanceId,'owner_epoch',OwnerEpoch) FROM command_records
WHERE Id IN ($IdList) AND TargetKind = 'Display' AND TargetId IN ($($targetList -join ',')) AND Command = 'SET_VOLUME' ORDER BY Id;
"@
    return @(Invoke-Sqlite $sql | ForEach-Object { $_ | ConvertFrom-Json })
}

# --- 登录 ---
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$origin = ([Uri]$BaseUrl).GetLeftPart([System.UriPartial]::Authority)
$headers = @{ Origin = $origin }
$csrf = (Invoke-RestMethod -Uri "$BaseUrl/api/auth/csrf/" -WebSession $session -Headers $headers).csrfToken
$headers['X-CSRFToken'] = $csrf
Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login/" -WebSession $session -Headers $headers `
    -ContentType 'application/json' -Body (@{ username = $Username; password = $Password } | ConvertTo-Json) | Out-Null
$headers['X-CSRFToken'] = $csrf
Remove-Variable Password
if ($Mode -eq 'HotSwitch') { Invoke-HotSwitchBenchmark; return }

$startId = [long](Invoke-Sqlite 'SELECT COALESCE(MAX(Id), 0) FROM command_records;')
Write-Host ("基准起点 command id = {0}；样本 {1}；目标窗口 {2}；间隔 {3} ms" -f $startId, $Samples, ($targetList -join '/'), $DelayMilliseconds) -ForegroundColor Cyan

# --- 提交普通控制命令 ---
$httpSamples = [System.Collections.Generic.List[double]]::new()
$ordinaryRequests = [Collections.Generic.List[object]]::new()
$submitWatch = [Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le $Samples; $i++) {
    $windowId = $targetList[($i - 1) % $targetList.Count]
    $volume = if ($i % 2 -eq 0) { 40 } else { 60 }
    $beforeRequestId = [long](Invoke-Sqlite 'SELECT COALESCE(MAX(Id), 0) FROM command_records;')
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $accepted = Invoke-RestMethod -Method Patch -Uri "$BaseUrl/api/playback/$windowId/volume/" -WebSession $session `
            -Headers $headers -ContentType 'application/json' -Body (@{ volume = $volume } | ConvertTo-Json)
        if (-not $accepted.success) { throw 'ordinary_http_not_accepted' }
    }
    finally {
        $watch.Stop()
        $httpSamples.Add($watch.Elapsed.TotalMilliseconds)
    }
    $correlationSql = "/* ordinary_correlate */ SELECT json_object('command_id',Id,'command',Command,'volume',json_extract(ArgsJson,'$.volume')) FROM command_records WHERE Id>$beforeRequestId AND TargetKind='Display' AND TargetId=$windowId ORDER BY Id;"
    $correlated = @(Invoke-Sqlite $correlationSql | ForEach-Object { $_ | ConvertFrom-Json })
    if ($correlated.Count -ne 1 -or $correlated[0].command -ne 'SET_VOLUME' -or $correlated[0].volume -ne $volume) {
        throw 'ordinary_request_ambiguous：测试目标有并发控制，不能关联本请求；请独占窗口1/2控制入口后重测。'
    }
    $ordinaryRequests.Add([pscustomobject]@{sample=$i;window_id=$windowId;volume=$volume;command_id=[long]$correlated[0].command_id;http_ms=$watch.Elapsed.TotalMilliseconds})
    if ($DelayMilliseconds -gt 0) { Start-Sleep -Milliseconds $DelayMilliseconds }
}
$submitWatch.Stop()
Write-Host ("提交完成：{0:N1}s，平均 HTTP {1:N1}ms" -f $submitWatch.Elapsed.TotalSeconds, ($httpSamples | Measure-Object -Average).Average) -ForegroundColor Cyan

# --- 等待队列排空 ---
$drainWatch = [Diagnostics.Stopwatch]::StartNew()
$open = 0
$ownIds = ($ordinaryRequests | ForEach-Object { $_.command_id }) -join ','
do {
    Start-Sleep -Milliseconds 250
    $records = @(Get-OrdinarySamples $ownIds)
    $open = @($records | Where-Object { $_.status -in @('Pending','Processing') }).Count
    if ($drainWatch.Elapsed.TotalSeconds -gt $DrainTimeoutSeconds) { break }
} while ($open -gt 0)
$drainWatch.Stop()

# --- 只从自有请求ID取样；其他窗口/命令/并发记录不混入结果 ---
if ($RawSamplesPath) {
    $raw = foreach ($request in $ordinaryRequests) {
        $record = @($records | Where-Object { $_.command_id -eq $request.command_id })
        [pscustomobject]@{sample=$request.sample;window_id=$request.window_id;volume=$request.volume;http_ms=$request.http_ms;
            command_id=$request.command_id;command_guid=$record.command_guid;status=$record.status;created_ticks=$record.created_ticks;
            started_ticks=$record.started_ticks;completed_ticks=$record.completed_ticks;result_code=$record.result_code;
            result_hash=$record.result_hash;consumer_instance_id=$record.consumer_instance_id;owner_epoch=$record.owner_epoch}
    }
    $raw | Export-Csv -LiteralPath $RawSamplesPath -NoTypeInformation -Encoding UTF8
}
$startedMs = @($records | Where-Object { $_.started_ticks } | ForEach-Object { ($_.started_ticks - $_.created_ticks) / 10000.0 })
$enqueued = $records.Count
$superseded = @($records | Where-Object { $_.status -eq 'Superseded' }).Count
$completed = @($records | Where-Object { $_.status -eq 'Completed' }).Count
$failed = @($records | Where-Object { $_.status -eq 'Failed' }).Count
$uncertain = @($records | Where-Object { $_.status -eq 'Uncertain' }).Count
$invalidEvidence = @($records | Where-Object { $_.status -eq 'Completed' -and (-not $_.started_ticks -or
    -not $_.completed_ticks -or $_.started_ticks -lt $_.created_ticks -or $_.completed_ticks -lt $_.started_ticks -or
    -not $_.result_hash -or -not $_.consumer_instance_id -or $_.owner_epoch -le 0 -or $_.result_code -ne 'ok') }).Count
$sampleFailures = $Samples - $completed + $invalidEvidence
$supersededRatio = if ($enqueued -gt 0) { $superseded / $enqueued } else { 0 }

$verdict = '通过'
$verdictNote = ''
if ($sampleFailures -gt 0 -or $enqueued -ne $Samples -or $startedMs.Count -ne $Samples -or $open -gt 0 -or $superseded -gt 0) {
    $verdict = '未通过'
    $verdictNote = "不是全部自有请求真实完成：Completed=$completed/$Samples，Failed=$failed，Uncertain=$uncertain，Superseded=$superseded，Open=$open，无效完成证据=$invalidEvidence。"
}
elseif ((Get-Percentile $startedMs 95) -gt 1000) {
    $verdict = '未通过'
    $verdictNote = '开始执行p95超过1000ms。'
}
if ($supersededRatio -gt $MaxSupersededRatio) {
    $verdictNote += " 折叠比例 $([Math]::Round($supersededRatio * 100, 1))% 超过诊断阈值，请增大 -DelayMilliseconds；阈值内的折叠也不算成功。"
}

$result = [ordered]@{
    samples          = $Samples
    targets          = ($targetList -join ',')
    delay_ms         = $DelayMilliseconds
    enqueued         = $enqueued
    started          = $startedMs.Count
    completed        = $completed
    failed           = $failed
    uncertain        = $uncertain
    invalid_evidence = $invalidEvidence
    sample_failures  = $sampleFailures
    superseded       = $superseded
    superseded_ratio = [Math]::Round($supersededRatio, 3)
    leftover_open    = $open
    submit_seconds   = [Math]::Round($submitWatch.Elapsed.TotalSeconds, 2)
    drain_seconds    = [Math]::Round($drainWatch.Elapsed.TotalSeconds, 2)
    http_avg_ms      = [Math]::Round(($httpSamples | Measure-Object -Average).Average, 1)
    http_p95_ms      = [Math]::Round((Get-Percentile $httpSamples.ToArray() 95), 1)
    started_p50_ms   = $(if ($startedMs.Count) { [Math]::Round((Get-Percentile $startedMs 50), 1) } else { $null })
    started_p95_ms   = $(if ($startedMs.Count) { [Math]::Round((Get-Percentile $startedMs 95), 1) } else { $null })
    started_p99_ms   = $(if ($startedMs.Count) { [Math]::Round((Get-Percentile $startedMs 99), 1) } else { $null })
    started_max_ms   = $(if ($startedMs.Count) { [Math]::Round(($startedMs | Measure-Object -Maximum).Maximum, 1) } else { $null })
    verdict          = $verdict
}
$result | ConvertTo-Json | Write-Host
if ($verdictNote) { Write-Host $verdictNote -ForegroundColor Yellow }

if ($OutputPath) {
    $lines = @(
        '# 普通命令基准原始样本'
        ''
        "采集时间：$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
        "ControlHost：$BaseUrl"
        "命令表：$DatabasePath"
        "样本：$Samples 次 PATCH /api/playback/{窗口}/volume/（40/60 交替）"
        "目标窗口：$($targetList -join '/')；提交间隔：$DelayMilliseconds ms"
        "硬件/媒体条件：$HardwareNote"
        ''
        '| 指标 | 值 |'
        '| --- | --- |'
        "| 入队命令数 | $($result.enqueued) |"
        "| 已开始执行 | $($result.started) |"
        "| 已完成 | $($result.completed) |"
        "| Failed / Uncertain | $failed / $uncertain |"
        "| 无效完成证据 | $invalidEvidence |"
        "| 失败/不完整样本 | $sampleFailures |"
        "| 被折叠 | $($result.superseded)（$([Math]::Round($supersededRatio * 100, 1))%） |"
        "| 排空后仍未完成 | $($result.leftover_open) |"
        "| HTTP 提交平均 | $($result.http_avg_ms) ms |"
        "| HTTP 提交 p95 | $($result.http_p95_ms) ms |"
        "| 开始执行 p50 | $($result.started_p50_ms) ms |"
        "| 开始执行 p95 | $($result.started_p95_ms) ms |"
        "| 开始执行 p99 | $($result.started_p99_ms) ms |"
        "| 开始执行最大 | $($result.started_max_ms) ms |"
        ''
        "判定（SC-006：p95 ≤ 1000 ms）：$verdict"
    )
    if ($verdictNote) { $lines += $verdictNote }
    $lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
    Write-Host "原始样本已写入 $OutputPath" -ForegroundColor Green
}
if ($verdict -ne '通过') { throw "普通命令基准未通过：$verdict；$verdictNote" }
