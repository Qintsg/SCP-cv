<#
.SYNOPSIS
  运行时发布的纯布局与文件合并函数；加载本文件不构建、不启动任何服务。
#>

function Get-RuntimePublishPlan {
    <#
    .DESCRIPTION
      :param RuntimeSourceRoot: 运行时源码根目录。
      :param PackageRoot: 已解析的全新发布根目录。
      :returns: 不含退役PowerPointHost的五个明确发布任务。
    #>
    param([string]$RuntimeSourceRoot, [string]$PackageRoot)
    $components = @(
        @{ Name = 'ScpCv.ControlHost'; Framework = 'net10.0' }
        @{ Name = 'ScpCv.Supervisor'; Framework = 'net10.0-windows10.0.19041.0' }
        @{ Name = 'ScpCv.PlayerWorker'; Framework = 'net10.0-windows10.0.19041.0' }
        @{ Name = 'ScpCv.AudioWorker'; Framework = 'net10.0-windows10.0.19041.0' }
        @{ Name = 'ScpCv.LibreOfficeHost'; Framework = 'net48' }
    )
    foreach ($component in $components) {
        $destination = $PackageRoot
        if ($component.Framework -eq 'net48') { $destination = Join-Path $PackageRoot 'libreoffice' }
        [pscustomobject]@{
            Name = $component.Name
            Framework = $component.Framework
            Project = Join-Path $RuntimeSourceRoot ('src\{0}\{0}.csproj' -f $component.Name)
            Destination = $destination
        }
    }
}

function New-RuntimeVlcPluginCache {
    <#
    .DESCRIPTION
      :param PackageRoot: 本轮已合并完成的独占发布根。
      :returns: 官方索引已生成；只执行显式生成命令，不启动播放器/服务或覆盖旧包。
    #>
    param([string]$PackageRoot)
    $root = [System.IO.Path]::GetFullPath($PackageRoot).TrimEnd('\', '/')
    if ($root.Contains('"')) { throw '发布路径含非法引号，拒绝构造生成命令。' }
    $generator = Join-Path $root 'ScpCv.AudioWorker.dll'
    if (-not (Test-Path -LiteralPath $generator -PathType Leaf)) { throw '缺少插件索引生成入口；未标记运行时包完成。' }
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $start.Arguments = '"' + $generator + '" --generate-plugin-cache "' + $root + '"'
    $start.WorkingDirectory = $root
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw '插件索引生成进程未启动。' }
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) { throw '自有索引生成进程退出未确认；发布失败。' }
            throw '插件索引生成超时；部分包保留，未标记发布完成。'
        }
        $errorText = $errors.GetAwaiter().GetResult()
        $outputText = $output.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw ('插件索引生成失败，退出码{0}：{1}' -f $process.ExitCode, $errorText) }
        $cache = Join-Path $root 'libvlc\win-x64\plugins\plugins.dat'
        if (-not (Test-Path -LiteralPath $cache -PathType Leaf) -or (Get-Item -LiteralPath $cache).Length -eq 0) {
            throw '生成命令未留下非空插件索引；拒绝标记发布完成。'
        }
        Write-Host $outputText.Trim()
    }
    finally { $process.Dispose() }
}

function Get-RuntimePublishArguments {
    <#
    .DESCRIPTION
      :param Component: 明确发布计划中的单一组件。
      :param Configuration: 构建配置。
      :param StageDirectory: 本轮组件独占暂存目录。
      :returns: 保留locked restore的参数；不传全局RID，沿用项目自身的RID与锁文件。
    #>
    param([psobject]$Component, [string]$Configuration, [string]$StageDirectory)
    $arguments = @('publish', $Component.Project, '-c', $Configuration, '-f', $Component.Framework,
        '-p:RestoreLockedMode=true', '-p:SelfContained=false', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-o', $StageDirectory)
    # 仅选择无RID项目的x64可执行宿主；不是RuntimeIdentifier，不扩展共享库的NuGet还原图。
    # Player/Audio已有项目级win-x64声明，仍由其各自锁文件决定平台依赖。
    if ($Component.Framework -ne 'net48') { $arguments += '-p:DefaultAppHostRuntimeIdentifier=win-x64' }
    return $arguments
}

function Get-RuntimeFileSha256 {
    <#
    .DESCRIPTION
      :param Path: 待核对的自有发布文件。
      :returns: 大写SHA256十六进制摘要；流式读取并释放句柄，不依赖跨版本PowerShell模块。
    #>
    param([string]$Path)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try { return [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose() }
    }
    finally { $sha256.Dispose() }
}

function Copy-MergedRuntimeFiles {
    <#
    .DESCRIPTION
      :param SourceDirectory: 本轮dotnet publish生成的独占暂存目录。
      :param DestinationDirectory: 对应TFM的发布目录。
      :returns: 无；同路径内容不同或重解析点均拒绝，不覆盖已复制文件。
    #>
    param([string]$SourceDirectory, [string]$DestinationDirectory)
    $sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\', '/')
    $destinationRoot = [System.IO.Path]::GetFullPath($DestinationDirectory).TrimEnd('\', '/')
    if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) {
        throw "发布暂存目录不存在：$sourceRoot"
    }
    $items = @(Get-Item -LiteralPath $sourceRoot) + @(Get-ChildItem -LiteralPath $sourceRoot -Force -Recurse)
    foreach ($item in $items) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "发布暂存区含重解析点，拒绝复制：$($item.FullName)"
        }
    }
    foreach ($file in $items | Where-Object { -not $_.PSIsContainer }) {
        $relative = $file.FullName.Substring($sourceRoot.Length + 1)
        $target = [System.IO.Path]::GetFullPath((Join-Path $destinationRoot $relative))
        if (-not $target.StartsWith($destinationRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "发布文件超出指定目录：$target"
        }
        if (Test-Path -LiteralPath $target) {
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "发布路径已被目录占用：$target" }
            $existingHash = Get-RuntimeFileSha256 -Path $target
            $incomingHash = Get-RuntimeFileSha256 -Path $file.FullName
            if ($existingHash -ne $incomingHash) {
                throw "发布依赖内容冲突，未覆盖：$relative"
            }
            continue
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}

function Assert-RuntimePublishDependencies {
    <#
    .DESCRIPTION
      :param OfficeProgram: 官方LibreOffice安装的program目录。
      :returns: 无；明确报告构建机缺失的Framework与官方CLI依赖，不安装或修复系统。
    #>
    param([string]$OfficeProgram)
    if ($env:OS -ne 'Windows_NT') { throw '运行时发布只支持Windows x64构建机。' }
    if (-not [Environment]::Is64BitOperatingSystem) { throw '运行时发布要求Windows x64。' }
    Get-Command dotnet -ErrorAction Stop | Out-Null
    $referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'
    if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'mscorlib.dll') -PathType Leaf)) {
        throw '缺少.NET Framework 4.8 targeting pack；请安装官方Developer Pack后重新发布。'
    }
    foreach ($name in @('soffice.exe', 'uno.ini', 'types\offapi.rdb')) {
        if (-not (Test-Path -LiteralPath (Join-Path $OfficeProgram $name) -PathType Leaf)) {
            throw "LibreOffice官方安装不完整，缺少：$name；请检查SCP_CV_LIBREOFFICE_PROGRAM。"
        }
    }
    foreach ($name in @('cli_basetypes', 'cli_ure', 'cli_uretypes', 'cli_oootypes', 'cli_cppuhelper')) {
        $gacKind = 'GAC_MSIL'
        if ($name -eq 'cli_cppuhelper') { $gacKind = 'GAC_64' }
        $assemblyRoot = Join-Path $env:windir ('Microsoft.NET\assembly\{0}\{1}' -f $gacKind, $name)
        $binding = Get-ChildItem -LiteralPath $assemblyRoot -Filter ($name + '.dll') -Recurse -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $binding) { throw "缺少LibreOffice官方.NET互操作组件：$name；请用官方安装器补装该组件。" }
    }
}
