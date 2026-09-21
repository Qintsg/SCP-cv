[CmdletBinding()]
param(
    # 让网卡上的 192.168.5.101~150 与目标集完全一致（补齐缺失、摘掉多余的）。
    [switch]$Apply,
    # 摘掉全部 50 个节点别名。
    [switch]$Clear,
    # 只打印现状，不改动。
    [switch]$Status,
    [string]$Adapter = 'VMware Network Adapter VMnet1',
    [int]$Port = 4830,
    # 故意不挂的节点，例如 -Except 192.168.5.150 用来制造「节点掉线」。
    [string[]]$Except = @()
)

$ErrorActionPreference = 'Stop'

$nodeIps = @(101..150 | ForEach-Object { "192.168.5.$_" })
$targetIps = @($nodeIps | Where-Object { $Except -notcontains $_ })
$testCommand = 'dotnet test runtime-dotnet/ScpCv.sln -c Debug --filter "Requires=VideoWallLoopback"'

function Get-AdapterIpv4 {
    param([string]$Name)
    $adapter = @([System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces() |
            Where-Object { $_.Name -eq $Name })
    if ($adapter.Count -eq 0) {
        $available = '（用 Get-NetAdapter 查看网卡名）'
        try { $available = (Get-NetAdapter | Select-Object -ExpandProperty Name) -join '、' } catch { }
        throw "找不到网卡「$Name」。本机网卡：$available"
    }

    @($adapter[0].GetIPProperties().UnicastAddresses |
            Where-Object { $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
            ForEach-Object { $_.Address.ToString() })
}

function Test-Bindable {
    param([string]$Ip, [int]$Port)
    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse($Ip), $Port)
        $listener.Start()
        return $true
    }
    catch {
        # 别名没挂（WSAEADDRNOTAVAIL）或被别的进程占着端口，都算「当前不能用来当节点」。
        return $false
    }
    finally {
        if ($null -ne $listener) { $listener.Stop() }
    }
}

# 刚 add 完的地址要先过重复地址检测，这期间绑不上；等到全部能绑为止。
function Wait-Bindable {
    param([string[]]$Ips, [int]$Port, [int]$TimeoutSeconds = 20)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        $pending = @($Ips | Where-Object { -not (Test-Bindable -Ip $_ -Port $Port) })
        if ($pending.Count -eq 0) {
            Write-Host ("  {0}/{1} 个节点地址就绪（用时 {2:0.0} 秒）" -f $Ips.Count, $Ips.Count, $stopwatch.Elapsed.TotalSeconds)
            return @()
        }

        if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { return $pending }
        Start-Sleep -Milliseconds 500
    }
}

function Add-Alias {
    param([string]$Name, [string]$Ip)
    $output = netsh interface ipv4 add address "$Name" $Ip 255.255.255.255 store=active 2>&1
    if ($LASTEXITCODE -ne 0) { throw "挂别名失败：$Ip`n$($output -join "`n")" }
}

function Remove-Alias {
    param([string]$Name, [string]$Ip)
    $output = netsh interface ipv4 delete address "$Name" $Ip store=active 2>&1
    if ($LASTEXITCODE -ne 0) { throw "摘别名失败：$Ip`n$($output -join "`n")" }
}

function Assert-Admin {
    $principal = [System.Security.Principal.WindowsPrincipal]::new(
        [System.Security.Principal.WindowsIdentity]::GetCurrent())
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '改网卡地址需要管理员权限：请用管理员 PowerShell 重跑本脚本。'
    }
}

if (@($Apply, $Clear, $Status | Where-Object { $_ }).Count -ne 1) {
    throw '请给且只给一个动作：-Apply（挂到目标集）、-Clear（摘掉全部 50 个）、-Status（看现状）。'
}

$present = @(Get-AdapterIpv4 $Adapter)
$onAdapter = @($nodeIps | Where-Object { $present -contains $_ })

if ($Status) {
    Write-Host "网卡：$Adapter，端口 $Port，别名一律 store=active 挂载（重启即消失，不写持久配置）"
    Write-Host ("已挂 {0}/50，未挂 {1}/50" -f $onAdapter.Count, (50 - $onAdapter.Count))
    if ($onAdapter.Count -gt 0) { Write-Host ($onAdapter -join ' ') }
    $absent = @($nodeIps | Where-Object { $onAdapter -notcontains $_ })
    if ($absent.Count -gt 0) { Write-Host ("未挂：{0}" -f ($absent -join ' ')) }
    return
}

if ($Clear) {
    Assert-Admin
    $removed = @($nodeIps | Where-Object { $present -contains $_ })
    foreach ($ip in $removed) {
        Remove-Alias -Name $Adapter -Ip $ip
        Write-Host "  - $ip"
    }

    $left = @($nodeIps | Where-Object { @(Get-AdapterIpv4 $Adapter) -contains $_ })
    if ($left.Count -gt 0) {
        throw "以下别名没摘干净，请手工检查 netsh interface ipv4 show addresses ""$Adapter""：$($left -join ' ')"
    }

    Write-Host "已摘掉 $($removed.Count) 个别名，50 个节点地址全部归还。"
    return
}

Assert-Admin
$missing = @($targetIps | Where-Object { $present -notcontains $_ })
$extra = @($onAdapter | Where-Object { $targetIps -notcontains $_ })

Write-Host "网卡：$Adapter"
Write-Host ("目标集：{0}/50 个节点{1}" -f $targetIps.Count, $(if ($Except.Count -gt 0) { "，故意不挂 $($Except -join '、')" } else { '' }))
foreach ($ip in $missing) {
    Add-Alias -Name $Adapter -Ip $ip
    Write-Host "  + $ip"
}
foreach ($ip in $extra) {
    Remove-Alias -Name $Adapter -Ip $ip
    Write-Host "  - $ip"
}
if ($missing.Count -eq 0 -and $extra.Count -eq 0) { Write-Host '  现状已与目标集一致，无需改动。' }

$pending = Wait-Bindable -Ips $targetIps -Port $Port
if ($pending.Count -gt 0) {
    throw "以下节点地址 $($pending.Count) 个在超时内仍绑不上 $Port，回环下发跑不起来：$($pending -join ' ')"
}

Write-Host ''
Write-Host '下一步：'
if ($Except.Count -eq 0) {
    Write-Host "  $testCommand"
    Write-Host "  # 跑完再换最坏场景：& $PSCommandPath -Apply -Except 192.168.5.150"
}
else {
    Write-Host "  $testCommand"
}
Write-Host "  # 收工摘干净：& $PSCommandPath -Clear"
