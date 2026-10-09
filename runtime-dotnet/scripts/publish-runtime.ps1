<#
.SYNOPSIS
  为Windows x64生成完整运行时包，不部署、不启动服务、不复制用户数据。
.DESCRIPTION
  .NET10组件共用发布根；Framework4.8 LibreOffice宿主及其协议DLL仅放libreoffice子目录。
  沿用项目自身RID与锁文件；DefaultAppHostRuntimeIdentifier只选择x64宿主，不向共享库传播RID。
  输出必须是全新目录；失败保留本轮部分产物并明确报错，不覆盖现有运行目录。
.PARAMETER OutputDirectory
  :param OutputDirectory: 尚不存在的运行时包绝对或相对目录。
.PARAMETER Configuration
  :param Configuration: 构建配置，默认Release。
.PARAMETER CheckDependencies
  :param CheckDependencies: 只核构建机依赖，不创建发布目录。
#>
[CmdletBinding(DefaultParameterSetName = 'Publish')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Publish')][string]$OutputDirectory,
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release',
    [Parameter(Mandatory = $true, ParameterSetName = 'Check')][switch]$CheckDependencies
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'runtime-publish.functions.ps1')
$runtimeSourceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$officeProgram = [Environment]::GetEnvironmentVariable('SCP_CV_LIBREOFFICE_PROGRAM')
if ([string]::IsNullOrWhiteSpace($officeProgram)) {
    $officeProgram = Join-Path $env:ProgramFiles 'LibreOffice\program'
}
Assert-RuntimePublishDependencies -OfficeProgram $officeProgram
if ($CheckDependencies) { Write-Host '运行时发布依赖检查通过；尚未构建或启动任何组件。'; return }

$packageRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $packageRoot) {
    throw "发布目录必须尚不存在，避免覆盖运行时或用户数据：$packageRoot"
}
$plan = @(Get-RuntimePublishPlan -RuntimeSourceRoot $runtimeSourceRoot -PackageRoot $packageRoot)
foreach ($component in $plan) {
    if (-not (Test-Path -LiteralPath $component.Project -PathType Leaf)) {
        throw "运行时项目不存在：$($component.Project)"
    }
}
$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('scp-cv-publish-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Path $stageRoot | Out-Null
try {
    foreach ($component in $plan) {
        $stage = Join-Path $stageRoot $component.Name
        $arguments = @(Get-RuntimePublishArguments -Component $component -Configuration $Configuration -StageDirectory $stage)
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw "发布失败：$($component.Name)，dotnet退出码$LASTEXITCODE；部分产物保留在$packageRoot。" }
        $executable = Join-Path $stage ($component.Name + '.exe')
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw "发布未生成预期可执行文件：$executable" }
        Copy-MergedRuntimeFiles -SourceDirectory $stage -DestinationDirectory $component.Destination
    }
    foreach ($component in $plan) {
        if (-not (Test-Path -LiteralPath (Join-Path $component.Destination ($component.Name + '.exe')) -PathType Leaf)) {
            throw "运行时包缺少组件：$($component.Name)"
        }
    }
    $frameworkProtocol = Join-Path $packageRoot 'libreoffice\ScpCv.Presentation.Protocol.dll'
    $mainProtocol = Join-Path $packageRoot 'ScpCv.Presentation.Protocol.dll'
    if (-not (Test-Path -LiteralPath $frameworkProtocol -PathType Leaf) -or -not (Test-Path -LiteralPath $mainProtocol -PathType Leaf)) {
        throw '运行时包缺少隔离的Net48/Net10文稿协议程序集。'
    }
    if ((Get-RuntimeFileSha256 -Path $frameworkProtocol) -eq (Get-RuntimeFileSha256 -Path $mainProtocol)) {
        throw 'Net48与Net10文稿协议程序集相同，无法证明TFM隔离；拒绝标记发布完成。'
    }
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'scripts') | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'run-headless.ps1') -Destination (Join-Path $packageRoot 'scripts\run-headless.ps1')
    # 所有插件文件合并后再生成索引，避免复制覆盖或时间戳变化使其立即失效。
    New-RuntimeVlcPluginCache -PackageRoot $packageRoot
    Write-Host "运行时包生成完成：$packageRoot；未部署、未启动服务。"
}
finally {
    # 只删除本轮GUID临时发布目录，先再次校验绝对目标与临时根边界；不清理发布包或数据。
    $resolvedStage = [System.IO.Path]::GetFullPath($stageRoot)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if ($resolvedStage.StartsWith($temporaryRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedStage) -match '^scp-cv-publish-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
    else { throw "临时目录边界验证失败，未删除：$resolvedStage" }
}
