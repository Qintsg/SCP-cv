#Requires -Version 7.2
#Requires -PSEdition Core
# 文件描述：桌面一键启动入口，后台验收服务并仅在失败时提示。
# 项目：SCP-cv；文件：start-desktop.ps1；作者：Qintsg；日期：2026-10-29。
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ConfigurationPath,
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$launcherMutex=$null;$launcherLocked=$false;$launcherExit=1
try{
    $configuration=[IO.Path]::GetFullPath($ConfigurationPath)
    $directory=Split-Path -Parent $configuration
    $entry=Join-Path $directory 'start-deployed.ps1'
    if(!(Test-Path -LiteralPath $configuration -PathType Leaf)){throw "启动配置不存在：$configuration"}
    if(!(Test-Path -LiteralPath $entry -PathType Leaf)){throw "启动程序不存在：$entry"}
    $launcherShell=Join-Path $PSHOME 'pwsh.exe'
    $principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if(!$CheckOnly -and !$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
        $elevated=[Diagnostics.ProcessStartInfo]::new($launcherShell)
        $elevated.UseShellExecute=$true;$elevated.Verb='runas';$elevated.WindowStyle='Hidden'
        foreach($argument in @('-NoLogo','-NoProfile','-WindowStyle','Hidden','-File',$PSCommandPath,'-ConfigurationPath',$configuration)){$elevated.ArgumentList.Add($argument)}
        try{
            $child=[Diagnostics.Process]::Start($elevated)
            try{$child.WaitForExit();$launcherExit=$child.ExitCode}finally{$child.Dispose()}
        }catch [ComponentModel.Win32Exception]{if($_.Exception.NativeErrorCode -ne 1223){throw};$launcherExit=0}
    }else{
        $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($configuration)))
        $launcherMutex=[Threading.Mutex]::new($false,('Global\ScpCvDesktop-'+$digest))
        try{$launcherLocked=$launcherMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$launcherLocked=$true}
        if(!$launcherLocked){$launcherExit=0}else{
            $start=[Diagnostics.ProcessStartInfo]::new($launcherShell)
            $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle='Hidden'
            $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
            $start.StandardOutputEncoding=[Text.UTF8Encoding]::new($false);$start.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
            foreach($argument in @('-NoLogo','-NoProfile','-NonInteractive','-File',$entry,'-ConfigurationPath',$configuration,'-NoPause')){$start.ArgumentList.Add($argument)}
            if($CheckOnly){$start.ArgumentList.Add('-CheckOnly')}
            $child=[Diagnostics.Process]::Start($start)
            try{
                $output=$child.StandardOutput.ReadToEndAsync();$errors=$child.StandardError.ReadToEndAsync()
                $child.WaitForExit();$launcherExit=$child.ExitCode
                $details=$output.GetAwaiter().GetResult()+$errors.GetAwaiter().GetResult()
            }finally{$child.Dispose()}
            $log=Join-Path $directory 'desktop-launch.log'
            [IO.File]::WriteAllText($log,([DateTimeOffset]::Now.ToString('o')+"`n"+$details),[Text.UTF8Encoding]::new($false))
            if($launcherExit -ne 0){throw ($details.Trim()+"`n日志："+$log)}
            if($CheckOnly){Write-Output '启动配置检查通过。'}
        }
    }
}catch{
    $launcherExit=1
    if($CheckOnly){[Console]::Error.WriteLine($_.Exception.Message)}else{
        $shell=New-Object -ComObject WScript.Shell
        try{$null=$shell.Popup($_.Exception.Message,0,'SCP-cv 启动失败',16)}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)|Out-Null}
    }
}finally{
    if($launcherLocked){$launcherMutex.ReleaseMutex()}
    if($launcherMutex){$launcherMutex.Dispose()}
}
exit $launcherExit
