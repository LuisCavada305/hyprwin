# HyprWin - inicia, para, instala ou restaura o tiling manager.
#   .\hyprwin.ps1             executa (normalmente via atalho, sem janela)
#   .\hyprwin.ps1 -Install    cria o atalho na inicialização e inicia
#   .\hyprwin.ps1 -Uninstall  para e remove o atalho
#   .\hyprwin.ps1 -Stop       para (mostra de volta todas as janelas)
#   .\hyprwin.ps1 -Restore    mostra janelas que ficaram ocultas após uma falha
param([switch]$Install, [switch]$Uninstall, [switch]$Stop, [switch]$Restore)

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$log = Join-Path $dir 'hyprwin.log'
$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'HyprWin.lnk'
$conhost = Join-Path $env:WINDIR 'System32\conhost.exe'
$launchArgs = "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""

Add-Type -Namespace HyprWinCtl -Name W -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
[DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
[DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
'@

function Stop-HyprWin {
    # [NullString]::Value: o PowerShell converte $null em "" ao passar para string.
    $h = [HyprWinCtl.W]::FindWindow([NullString]::Value, 'HyprWinHost')
    if ($h -eq [IntPtr]::Zero) { return $false }
    [void][HyprWinCtl.W]::PostMessage($h, 0x8009, [IntPtr]::Zero, [IntPtr]::Zero)
    for ($i = 0; $i -lt 30 -and [HyprWinCtl.W]::IsWindow($h); $i++) { Start-Sleep -Milliseconds 100 }
    return $true
}

function Restore-Hidden {
    $state = Join-Path $dir 'hidden.state'
    if (-not (Test-Path $state)) { return 0 }
    $n = 0
    foreach ($line in Get-Content $state) {
        $h = [IntPtr][long]$line
        if ([HyprWinCtl.W]::IsWindow($h)) { [void][HyprWinCtl.W]::ShowWindowAsync($h, 8); $n++ }
    }
    Remove-Item $state -Force
    return $n
}

if ($Stop -or $Uninstall) {
    if (Stop-HyprWin) { 'HyprWin parado.' } else { 'HyprWin não estava em execução.' }
    if ($Uninstall -and (Test-Path $lnk)) { Remove-Item $lnk -Force; 'Atalho de inicialização removido.' }
    return
}

if ($Restore) {
    [void](Stop-HyprWin)
    "Janelas restauradas: $(Restore-Hidden)"
    return
}

if ($Install) {
    $sh = New-Object -ComObject WScript.Shell
    $s = $sh.CreateShortcut($lnk)
    $s.TargetPath = $conhost
    $s.Arguments = $launchArgs
    $s.WorkingDirectory = $dir
    $s.Description = 'HyprWin tiling manager'
    $s.Save()
    "Atalho criado: $lnk"
    [void](Stop-HyprWin)
    Start-Process -FilePath $conhost -ArgumentList $launchArgs -WorkingDirectory $dir
    'HyprWin iniciado.'
    return
}

# Execução normal: compila o C# uma vez (cache por hash) e roda.
try {
    $cs = Join-Path $dir 'hyprwin.cs'
    $hash = (Get-FileHash $cs -Algorithm SHA256).Hash.Substring(0, 12)
    $bin = Join-Path $dir 'bin'
    New-Item -ItemType Directory -Force $bin | Out-Null
    $dll = Join-Path $bin "hyprwin-$hash.dll"
    if (-not (Test-Path $dll)) {
        Get-ChildItem $bin -Filter 'hyprwin-*.dll' | ForEach-Object { try { Remove-Item $_.FullName -Force } catch {} }
        Add-Type -Path $cs -ReferencedAssemblies System.Windows.Forms, System.Drawing -OutputAssembly $dll -OutputType Library
    }
    Add-Type -Path $dll
    [HyprWin.App]::Run($dir)
}
catch {
    Add-Content -Path $log -Value "$(Get-Date -Format s) ERRO: $($_ | Out-String)" -Encoding utf8
    throw
}
