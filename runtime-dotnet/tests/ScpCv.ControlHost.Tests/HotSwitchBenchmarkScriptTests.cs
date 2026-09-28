// 热切换脚本以受控 HTTP/SQLite 替身验证完成、代次与两窗门禁，不触碰真实播放器。
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class HotSwitchBenchmarkScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "scripts", "benchmark-commands.ps1");

    [Fact]
    public void HotSwitchRequiresExplicitModeSourcesAndRawEvidence()
    {
        var script = File.ReadAllText(ScriptPath);

        Assert.Contains("[string]$Mode = 'Ordinary'", script, StringComparison.Ordinal);
        Assert.Contains("[long[]]$SourceIds = @()", script, StringComparison.Ordinal);
        Assert.Contains("-Mode HotSwitch", script, StringComparison.Ordinal);
        Assert.Contains("窗口1/2", script, StringComparison.Ordinal);
        Assert.Contains("非画面验收", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/system/restart/", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/devices/", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/volume/", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop-Process", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no_sources", "至少两个不同")]
    [InlineData("bad_target", "窗口号必须在 1..2")]
    [InlineData("no_raw", "-RawSamplesPath")]
    public async Task InvalidHotSwitchArgumentsFailBeforeAnyHttp(string scenario, string message)
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync(scenario);

        Assert.Contains(message, result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Empty(result.GetProperty("http_calls").EnumerateArray());
        Assert.Empty(result.GetProperty("rows").EnumerateArray());
    }

    [Theory]
    [InlineData("simulation", "Hardware")]
    [InlineData("experimental", "实验")]
    public async Task UnsafeRuntimeFailsWithoutOpen(string scenario, string message)
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync(scenario);

        Assert.Contains(message, result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, result.GetProperty("open_calls").GetInt32());
    }

    [Fact]
    public async Task HealthySwitchesWaitForCompletionAndAlternateEachWindow()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync("success");

        Assert.Equal(string.Empty, result.GetProperty("error").GetString());
        Assert.Equal(4, result.GetProperty("open_calls").GetInt32());
        var rows = result.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(["1", "2", "1", "2"], rows.Select(row => row.GetProperty("window_id").GetString()));
        Assert.Equal(["39", "39", "40", "40"], rows.Select(row => row.GetProperty("source_id").GetString()));
        Assert.All(rows, row =>
        {
            Assert.Equal("True", row.GetProperty("succeeded").GetString());
            Assert.Equal("Completed", row.GetProperty("status").GetString());
            Assert.Equal("20", row.GetProperty("completed_after_ms").GetString());
            Assert.Equal("hash-proof", row.GetProperty("result_hash").GetString());
            Assert.Equal("{}", row.GetProperty("result_evidence_json").GetString());
            Assert.Equal(row.GetProperty("generation").GetString(), row.GetProperty("observed_generation").GetString());
            Assert.Equal(row.GetProperty("source_id").GetString(), row.GetProperty("actual_source_id").GetString());
        });
        Assert.Contains("非画面验收", result.GetProperty("report").GetString(), StringComparison.Ordinal);
        Assert.Contains("| 失败数 | 0 |", result.GetProperty("report").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("accepted_only", "completion_timeout")]
    [InlineData("superseded", "command_superseded")]
    [InlineData("failed", "command_failed")]
    [InlineData("uncertain", "command_uncertain")]
    [InlineData("wrong_generation", "generation_changed")]
    [InlineData("wrong_source", "completion_timeout")]
    [InlineData("missing_hash", "completion_evidence_missing")]
    [InlineData("wrong_worker", "worker_identity_changed")]
    [InlineData("duplicate", "concurrent_commands")]
    [InlineData("cleanup", "completion_timeout")]
    public async Task AcceptedOrStaleResultsCannotCountAsHealthySwitches(string scenario, string failure)
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync(scenario);

        Assert.Equal(1, result.GetProperty("open_calls").GetInt32());
        var row = Assert.Single(result.GetProperty("rows").EnumerateArray());
        Assert.Equal("False", row.GetProperty("succeeded").GetString());
        Assert.Equal(failure, row.GetProperty("failure_code").GetString());
        Assert.Contains("| 失败数 | 1 |", result.GetProperty("report").GetString(), StringComparison.Ordinal);
        Assert.Contains("未通过", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptHasPowerShell51BomLfAndUsesReadOnlyExactTargetQueries()
    {
        var bytes = File.ReadAllBytes(ScriptPath);
        Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, bytes[..3]);
        Assert.DoesNotContain((byte)'\r', bytes);
        var script = File.ReadAllText(ScriptPath);
        Assert.Contains("-readonly", script, StringComparison.Ordinal);
        Assert.Contains("c.TargetKind = 'Display' AND c.TargetId = $WindowId", script, StringComparison.Ordinal);
        Assert.Contains("c.SourceGeneration = $Generation", script, StringComparison.Ordinal);
        Assert.Contains("c.Id > $AfterId", script, StringComparison.Ordinal);
        Assert.Contains("ConsumerInstanceId", script, StringComparison.Ordinal);
        Assert.Contains("CompletedAt", script, StringComparison.Ordinal);
        Assert.Contains("ActualSourceId", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// 运行只允许测试替身访问的 PowerShell，原始 CSV 保存在唯一临时目录并在退出后清理。
    /// :param scenario: 注入的命令或会话状态。
    /// :returns: 替身记录的调用、CSV 与可读报告。
    /// </summary>
    private static async Task<JsonElement> RunFixtureAsync(string scenario)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scp-hot-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var scriptPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(ScriptPath));
            var outputRoot = Convert.ToBase64String(Encoding.UTF8.GetBytes(directory));
            var command = Fixture.Replace("__SCRIPT_PATH__", scriptPath, StringComparison.Ordinal)
                .Replace("__OUTPUT_ROOT__", outputRoot, StringComparison.Ordinal)
                .Replace("__SCENARIO__", scenario, StringComparison.Ordinal);
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            string[] arguments = ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
                Convert.ToBase64String(Encoding.Unicode.GetBytes(command))];
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动隔离 PowerShell。");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            var output = await stdout;
            Assert.True(process.ExitCode == 0, await stderr);
            var line = output.Split('\n').Single(item => item.StartsWith("CONTRACT_JSON:", StringComparison.Ordinal));
            using var json = JsonDocument.Parse(line["CONTRACT_JSON:".Length..]);
            return json.RootElement.Clone();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private const string Fixture = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
        $scriptPath = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__SCRIPT_PATH__'))
        $fixtureRoot = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__OUTPUT_ROOT__'))
        $global:fixtureScenario = '__SCENARIO__'
        $global:fixtureHttp = [Collections.Generic.List[string]]::new()
        $global:fixtureOpens = 0
        $global:fixtureGeneration = @{1=5; 2=5}
        $global:fixtureSource = @{1=99; 2=99}
        $global:fixtureId = 10
        $global:fixturePolls = 0
        function Start-Process { throw 'unexpected_process_side_effect' }
        function Stop-Process { throw 'unexpected_process_side_effect' }
        function Get-CimInstance { throw 'unexpected_process_side_effect' }
        function Test-Path { param($LiteralPath, $PathType) return $true }
        function Get-Command { param($Name, $ErrorAction) return [pscustomobject]@{Source='Invoke-ContractSqlite'} }
        function Invoke-RestMethod {
            param($Uri, $Method, $WebSession, $Headers, $ContentType, $Body, $TimeoutSec)
            $global:fixtureHttp.Add("$Method $Uri")
            if ($Uri -match '/api/auth/csrf/$') { return @{csrfToken='fixture-token'} }
            if ($Uri -match '/api/auth/login/$') { return @{success=$true} }
            if ($Uri -eq 'http://contract.invalid/') {
                return @{safety_mode=$(if ($fixtureScenario -eq 'simulation') {'simulation'} else {'hardware'})}
            }
            if ($Uri -match '/api/settings/powerpoint/$') {
                return @{settings=@{experimental_enabled=($fixtureScenario -eq 'experimental')}}
            }
            if ($Uri -match '/api/playback/([12])/open/$') {
                if ($Method -ne 'Post') { throw 'unexpected_method' }
                $window = [int]$Matches[1]
                $json = if ($Body -is [byte[]]) { [Text.Encoding]::UTF8.GetString($Body) } else { $Body }
                $global:fixtureSource[$window] = ($json | ConvertFrom-Json).source_id
                $global:fixtureGeneration[$window]++
                $global:fixtureId++
                $global:fixtureOpens++
                $global:fixturePolls = 0
                return @{success=$true; sessions=@()}
            }
            if ($Uri -match '/api/sessions/([12])/$') {
                $window = [int]$Matches[1]
                return @{session=@{window_id=$window;source_id=$fixtureSource[$window];player_online=$true;
                    playback_state='playing';pending_command='';error_message=''}}
            }
            throw "unexpected_http: $Uri"
        }
        function Invoke-ContractSqlite {
            $global:LASTEXITCODE = 0
            if ($args[0] -ne '-readonly') { throw 'unexpected_sqlite_write' }
            $sql = $args[-1]
            if ($sql -notmatch 's.WindowId = ([12])') { throw 'missing_two_window_query_guard' }
            $window = [int]$Matches[1]
            $generation = $fixtureGeneration[$window]
            $worker = "11111111-1111-1111-1111-11111111111$window"
            $row = [ordered]@{after_id=$fixtureId;desired_generation=$generation;observed_generation=$generation;
                source_id=$fixtureSource[$window];actual_source_id=$fixtureSource[$window];playback_state='Playing';
                pending_command='';error_message='';cleanup_pending=0;worker_instance_id=$worker;worker_pid=(100+$window);
                worker_start_ticks=12345;owner_epoch=8;worker_status='Online';group_state='Armed'}
            if ($sql -match 'hot_sample') {
                $global:fixturePolls++
                $ticks = [DateTimeOffset]::UtcNow.UtcTicks
                $row.command_count=1; $row.command_id=$fixtureId; $row.command_guid='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
                $row.command_generation=$generation; $row.command_source_id=$fixtureSource[$window]
                $row.status=$(if ($fixturePolls -eq 1) {'Processing'} else {'Completed'})
                $row.created_ticks=$ticks; $row.started_ticks=$ticks+100000; $row.completed_ticks=$ticks+200000
                $row.result_code='ok'; $row.result_hash='hash-proof'; $row.result_evidence_json='{}'
                $row.consumer_instance_id=$worker; $row.command_owner_epoch=8
                switch ($fixtureScenario) {
                    'accepted_only' {$row.status='Pending';$row.completed_ticks=$null}
                    'superseded' {$row.status='Superseded'}
                    'failed' {$row.status='Failed'}
                    'uncertain' {$row.status='Uncertain'}
                    'wrong_generation' {$row.desired_generation++}
                    'wrong_source' {$row.actual_source_id=999}
                    'missing_hash' {$row.result_hash=''}
                    'wrong_worker' {$row.consumer_instance_id='bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'}
                    'duplicate' {$row.command_count=2}
                    'cleanup' {$row.cleanup_pending=1}
                }
            }
            $row | ConvertTo-Json -Compress
        }
        $env:SCP_CV_DEVELOPMENT_PASSWORD='fixture-non-secret'
        $rawPath = Join-Path $fixtureRoot 'samples.csv'
        $reportPath = Join-Path $fixtureRoot 'report.md'
        $fixtureArguments = @{BaseUrl='http://contract.invalid';DatabasePath='fixture-control.db';Mode='HotSwitch';
            SourceIds=@(39,40);Targets=@(1,2);Samples=4;DelayMilliseconds=0;SwitchTimeoutSeconds=1;
            PollMilliseconds=20;SqliteExecutable='fixture-sqlite';RawSamplesPath=$rawPath;OutputPath=$reportPath}
        switch ($fixtureScenario) {
            'no_sources' {$fixtureArguments.SourceIds=@()}
            'bad_target' {$fixtureArguments.Targets=@(1,3)}
            'no_raw' {$fixtureArguments.RawSamplesPath=''}
        }
        $failure = ''
        try { & $scriptPath @fixtureArguments }
        catch { $failure = $_.Exception.Message }
        $rows = if ([IO.File]::Exists($rawPath)) { @(Import-Csv -LiteralPath $rawPath) } else { @() }
        $report = if ([IO.File]::Exists($reportPath)) { [IO.File]::ReadAllText($reportPath) } else { '' }
        $result = @{error=$failure;http_calls=@($fixtureHttp);open_calls=$fixtureOpens;rows=@($rows);report=$report}
        [Console]::WriteLine('CONTRACT_JSON:' + ($result | ConvertTo-Json -Depth 6 -Compress))
        """;
}
