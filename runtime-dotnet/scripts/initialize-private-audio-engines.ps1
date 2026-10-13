# 项目专用WebView2/LibreOffice副本初始化；只创建新目录，不改原安装、声卡、注册表或证书。
# @Project : SCP-cv
# @File : initialize-private-audio-engines.ps1
# @Author : Qintsg
# @Date : 2026-10-13
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][string]$WebViewSource,
    [Parameter(Mandatory=$true)][string]$LibreOfficeSource,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'

function Get-PlainLocalPath {
    <# :param Path: 明确本机盘符绝对路径。:returns: 规范路径；存在的每级拒绝重解析点。 #>
    param([string]$Path)
    if ($Path -notmatch '^[A-Za-z]:[\\/]') { throw '引擎路径必须是本机盘符绝对路径，不能是相对路径或UNC。' }
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($resolvedPath))
    if ($drive.DriveType -eq [IO.DriveType]::Network) { throw '引擎路径不能使用网络映射盘。' }
    $ancestor = $resolvedPath
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '引擎路径不能经过重解析点。' }
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    return $resolvedPath.TrimEnd([char[]]'\/')
}

function Get-SourceFiles {
    <# :param Source: 原安装普通目录。:returns: 先检查再遍历的文件列表，不跟随目录链接。 #>
    param([string]$Source)
    $directories = [Collections.Generic.Queue[string]]::new()
    $directories.Enqueue($Source)
    $files = [Collections.Generic.List[object]]::new()
    while ($directories.Count -gt 0) {
        foreach ($entry in Get-ChildItem -LiteralPath $directories.Dequeue() -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '原安装包含重解析点，不复制到专用引擎。' }
            if ($entry.PSIsContainer) { $directories.Enqueue($entry.FullName) }
            else { $files.Add($entry) }
        }
    }
    return $files.ToArray()
}

function Assert-OfficialImage {
    <# :param Image: 原安装主映像。:param Publisher: 已知官方签名主体。:returns: 有效官方签名，失败不复制。 #>
    param([string]$Image, [string]$Publisher)
    if (!(Test-Path -LiteralPath $Image -PathType Leaf)) { throw '原安装缺少主映像。' }
    $signature = Get-AuthenticodeSignature -LiteralPath $Image
    if ($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch $Publisher) {
        throw '原安装主映像没有匹配的有效官方签名。'
    }
    return (Get-FileHash -LiteralPath $Image -Algorithm SHA256).Hash
}

function Copy-VerifiedTree {
    <# :param Source: 已检普通源。:param Destination: 本轮新目录。:param Files: 已检文件。:returns: 逐文件源/副本摘要；失败不生成就绪标记。 #>
    param([string]$Source, [string]$Destination, [object[]]$Files)
    [void][IO.Directory]::CreateDirectory($Destination)
    $manifest = [Collections.Generic.List[object]]::new()
    foreach ($file in $Files) {
        $relative = $file.FullName.Substring($Source.Length+1)
        $target = [IO.Path]::GetFullPath([IO.Path]::Combine($Destination, $relative))
        if (!$target.StartsWith($Destination+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '复制目标越出本轮目录。' }
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        $before = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        [IO.File]::Copy($file.FullName, $target, $false)
        $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        $after = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($before -ne $after -or $actual -ne $before) { throw '原安装在复制中发生变化或副本摘要不一致，未生成标记。' }
        $manifest.Add([pscustomobject]@{path=$relative.Replace('\','/');sha256=$actual;length=$file.Length})
    }
    return $manifest.ToArray()
}

$destinationRoot = Get-PlainLocalPath $Root
if (Test-Path -LiteralPath $destinationRoot) { throw '目标目录已存在；不覆盖既有引擎或证据。请选择新的明确版本目录。' }
$browserSource = Get-PlainLocalPath $WebViewSource
$officeSource = Get-PlainLocalPath $LibreOfficeSource
foreach ($originalSource in @($browserSource, $officeSource)) {
    if ($destinationRoot.StartsWith($originalSource+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw '专用引擎目标不能位于原安装目录中，未创建任何目录。'
    }
}
if (!(Test-Path -LiteralPath $browserSource -PathType Container) -or !(Test-Path -LiteralPath $officeSource -PathType Container)) { throw '原安装目录不存在。' }
$browserHash = Assert-OfficialImage (Join-Path $browserSource 'msedgewebview2.exe') 'Microsoft Corporation'
$officeHash = Assert-OfficialImage (Join-Path $officeSource 'program/soffice.exe') 'The Document Foundation'
$browserFiles = @(Get-SourceFiles $browserSource)
$officeFiles = @(Get-SourceFiles $officeSource)
$needed = 2*(($browserFiles | Measure-Object Length -Sum).Sum + ($officeFiles | Measure-Object Length -Sum).Sum)
$targetDrive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($destinationRoot))
if ($targetDrive.AvailableFreeSpace -lt $needed+1GB) { throw '目标磁盘空间不足，未创建目录。' }
if ($ValidateOnly) {
    [pscustomobject]@{status='validated_only';root=$destinationRoot;bytes=$needed;webview_files=$browserFiles.Count;office_files=$officeFiles.Count} | ConvertTo-Json -Compress
    return
}
[void][IO.Directory]::CreateDirectory($destinationRoot)
$records = [Collections.Generic.List[object]]::new()
foreach ($window in 1,2) {
    $browserDestination = Join-Path $destinationRoot "window-$window/webview"
    $officeDestination = Join-Path $destinationRoot "window-$window/libreoffice"
    $browserManifest = @(Copy-VerifiedTree $browserSource $browserDestination $browserFiles)
    $officeManifest = @(Copy-VerifiedTree $officeSource $officeDestination $officeFiles)
    if ((Assert-OfficialImage (Join-Path $browserDestination 'msedgewebview2.exe') 'Microsoft Corporation') -ne $browserHash -or
        (Assert-OfficialImage (Join-Path $officeDestination 'program/soffice.exe') 'The Document Foundation') -ne $officeHash) { throw '副本主映像核验失败，未生成标记。' }
    $records.Add([pscustomobject]@{window=$window;webview=$browserManifest;libreoffice=$officeManifest})
}
$encoding = [Text.UTF8Encoding]::new($false)
$metadata = [pscustomobject]@{version=1;created_utc=[DateTime]::UtcNow.ToString('O');engines=$records.ToArray()}
[IO.File]::WriteAllText((Join-Path $destinationRoot 'private-audio-engines.json'), ($metadata | ConvertTo-Json -Depth 8), $encoding)
# 标记最后写入；中途失败的目录保留供诊断，不自动递归删除，也不会被运行时认可。
[IO.File]::WriteAllText((Join-Path $destinationRoot 'private-audio-engines.marker'), "SCP-cv private audio engines v1`n", $encoding)
[pscustomobject]@{status='copied_verified';root=$destinationRoot;bytes=$needed;windows=2} | ConvertTo-Json -Compress
