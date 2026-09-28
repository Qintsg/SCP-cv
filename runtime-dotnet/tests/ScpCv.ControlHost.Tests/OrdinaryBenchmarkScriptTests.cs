// 普通命令基准必须精确关联请求，并拒绝“已经开始但失败、未完成或被折叠”的假成功。
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ScpCv.ControlHost.Tests;

public sealed class OrdinaryBenchmarkScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        AppContext.BaseDirectory, "scripts", "benchmark-commands.ps1");

    [Theory]
    [InlineData("failed")]
    [InlineData("uncertain")]
    [InlineData("processing")]
    [InlineData("pending")]
    [InlineData("all_superseded")]
    [InlineData("partial_superseded")]
    [InlineData("invalid_evidence")]
    public async Task StartedCommandsCannotHideIncompleteOrFailedSamples(string scenario)
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync(scenario);

        Assert.Contains("未通过", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("：通过", result.GetProperty("report").GetString(), StringComparison.Ordinal);
        Assert.Contains("失败/不完整样本", result.GetProperty("report").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompletedOwnSamplesIgnoreUnrelatedCommandTableRows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync("completed");

        Assert.Equal(string.Empty, result.GetProperty("error").GetString());
        Assert.Contains("：通过", result.GetProperty("report").GetString(), StringComparison.Ordinal);
        Assert.Contains("| 失败/不完整样本 | 0 |", result.GetProperty("report").GetString(), StringComparison.Ordinal);
        Assert.Equal(10, result.GetProperty("requests").GetInt32());
        Assert.True(result.GetProperty("used_own_ids").GetBoolean());
        Assert.Equal(10, result.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task ConcurrentTargetCommandMakesCorrelationFailClosed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = await RunFixtureAsync("concurrent");

        Assert.Contains("ordinary_request_ambiguous", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, result.GetProperty("requests").GetInt32());
    }

    /// <summary>
    /// 每个替身只生成唯一临时报告，所有HTTP和sqlite调用均截断，不操作运行组。
    /// :param scenario: 注入的普通命令终态。
    /// :returns: 隔离进程的错误、报告与精确命令查询证据。
    /// </summary>
    private static async Task<JsonElement> RunFixtureAsync(string scenario)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scp-ordinary-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var command = Fixture.Replace("__SCRIPT_PATH__", Convert.ToBase64String(Encoding.UTF8.GetBytes(ScriptPath)), StringComparison.Ordinal)
                .Replace("__OUTPUT_ROOT__", Convert.ToBase64String(Encoding.UTF8.GetBytes(directory)), StringComparison.Ordinal)
                .Replace("__SCENARIO__", scenario, StringComparison.Ordinal);
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
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
        $ProgressPreference = 'SilentlyContinue'
        [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
        $scriptPath = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__SCRIPT_PATH__'))
        $fixtureRoot = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__OUTPUT_ROOT__'))
        $global:fixtureScenario = '__SCENARIO__'
        $global:fixtureCommands = [Collections.Generic.List[object]]::new()
        $global:fixtureOwnQuery = $false
        function Start-Process { throw 'unexpected_process_side_effect' }
        function Stop-Process { throw 'unexpected_process_side_effect' }
        function Test-Path { param($LiteralPath, $PathType) return $true }
        function Get-Command { param($Name, $ErrorAction) return [pscustomobject]@{Source='Invoke-ContractSqlite'} }
        function Invoke-RestMethod {
            param($Uri, $Method, $WebSession, $Headers, $ContentType, $Body, $TimeoutSec)
            if ($Uri -match '/api/auth/csrf/$') { return @{csrfToken='fixture-token'} }
            if ($Uri -match '/api/auth/login/$') { return @{success=$true} }
            if ($Uri -eq 'http://contract.invalid/') { return @{safety_mode='hardware'} }
            if ($Uri -notmatch '/api/playback/([12])/volume/$' -or $Method -ne 'Patch') { throw 'unexpected_http' }
            $window = [int]$Matches[1]
            $json = if ($Body -is [byte[]]) { [Text.Encoding]::UTF8.GetString($Body) } else { $Body }
            $volume = ($json | ConvertFrom-Json).volume
            $status = switch ($fixtureScenario) {
                'failed' {'Failed'} 'uncertain' {'Uncertain'} 'processing' {'Processing'} 'pending' {'Pending'}
                'all_superseded' {'Superseded'} default {'Completed'}
            }
            if ($fixtureScenario -eq 'partial_superseded' -and $fixtureCommands.Count -eq 9) { $status='Superseded' }
            $started = if ($status -in @('Superseded','Pending')) {$null} else {1100000}
            $completed = if ($status -in @('Processing','Pending')) {$null} else {1200000}
            $global:fixtureCommands.Add([pscustomobject]@{command_id=(11+$fixtureCommands.Count);command_guid='own-command';
                window_id=$window;command='SET_VOLUME';volume=$volume;status=$status;created_ticks=1000000;
                started_ticks=$started;completed_ticks=$completed;result_hash=$(if ($fixtureScenario -eq 'invalid_evidence') {''} else {'proof'});
                consumer_instance_id='own-worker';owner_epoch=8;result_code='ok'})
            return @{success=$true}
        }
        function Invoke-ContractSqlite {
            $global:LASTEXITCODE=0
            if ($args[0] -ne '-readonly') { throw 'unexpected_sqlite_write' }
            $sql = $args[-1]
            if ($sql -match 'MAX\(Id\)') { return (10+$fixtureCommands.Count) }
            if ($sql -match 'ordinary_correlate') {
                $row = $fixtureCommands[-1] | ConvertTo-Json -Compress
                if ($fixtureScenario -eq 'concurrent') { return @($row,$row) }
                return $row
            }
            if ($sql -match 'ordinary_samples') {
                if ($sql -notmatch 'Id IN \(11,12,13,14,15,16,17,18,19,20\)' -or
                    $sql -notmatch "TargetKind = 'Display'" -or $sql -notmatch "Command = 'SET_VOLUME'") { throw 'missing_own_command_scope' }
                $global:fixtureOwnQuery=$true
                return @($fixtureCommands | ForEach-Object { $_ | ConvertTo-Json -Compress })
            }
            # 兼容旧实现以直接证实“已开始但未完成”的假通过，不用于新路径。
            if ($sql -match "Status IN \('Pending','Processing'\)") { return @($fixtureCommands | Where-Object {$_.status -eq 'Processing'}).Count }
            if ($sql -match "Status = 'Superseded'") { return @($fixtureCommands | Where-Object {$_.status -eq 'Superseded'}).Count }
            if ($sql -match "Status = 'Completed'") { return @($fixtureCommands | Where-Object {$_.status -eq 'Completed'}).Count }
            if ($sql -match 'COUNT\(\*\)') { return $fixtureCommands.Count }
            if ($sql -match 'StartedAt - CreatedAt') {
                return @($fixtureCommands | Where-Object {$_.started_ticks} | ForEach-Object {"$($_.command_id)|$($_.started_ticks-$_.created_ticks)"})
            }
            throw 'unexpected_sql_query'
        }
        $env:SCP_CV_DEVELOPMENT_PASSWORD='fixture-non-secret'
        $rawPath = Join-Path $fixtureRoot 'samples.csv'
        $reportPath = Join-Path $fixtureRoot 'report.md'
        $failure=''
        try { & $scriptPath -BaseUrl http://contract.invalid -DatabasePath fixture.db -Samples 10 -DelayMilliseconds 0 `
            -DrainTimeoutSeconds 1 -SqliteExecutable fixture-sqlite -RawSamplesPath $rawPath -OutputPath $reportPath }
        catch { $failure=$_.Exception.Message }
        $report = if ([IO.File]::Exists($reportPath)) {[IO.File]::ReadAllText($reportPath)} else {''}
        $rows = if ([IO.File]::Exists($rawPath)) {@(Import-Csv -LiteralPath $rawPath)} else {@()}
        [Console]::WriteLine('CONTRACT_JSON:' + (@{error=$failure;report=$report;requests=$fixtureCommands.Count;
            used_own_ids=$fixtureOwnQuery;rows=@($rows)} | ConvertTo-Json -Depth 5 -Compress))
        """;
}
