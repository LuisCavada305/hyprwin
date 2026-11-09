# Perfil do Windows PowerShell - prompt em duas linhas, paleta Rose Pine, sem modulos externos.
# Os glifos vem da JetBrainsMono Nerd Font (configurada no Windows Terminal).
# O arquivo usa so ASCII: o PowerShell 5.1 le scripts sem BOM como ANSI.

[Console]::OutputEncoding = [Text.Encoding]::UTF8

$script:E = [char]27
function script:Fg([string]$hex) {
    $r = [Convert]::ToInt32($hex.Substring(0, 2), 16)
    $g = [Convert]::ToInt32($hex.Substring(2, 2), 16)
    $b = [Convert]::ToInt32($hex.Substring(4, 2), 16)
    "$script:E[38;2;${r};${g};${b}m"
}

# Rose Pine
$script:C = @{
    Love = Fg 'eb6f92'; Gold = Fg 'f6c177'; Rose = Fg 'ebbcba'; Pine = Fg '31748f'
    Foam = Fg '9ccfd8'; Iris = Fg 'c4a7e7'; Muted = Fg '6e6a86'; Subtle = Fg '908caa'; Text = Fg 'e0def4'
    Reset = "$script:E[0m"
}

$script:G = @{
    Folder = [char]0xF07C; Home = [char]0xF015; Branch = [char]0xE0A0; Arrow = [char]0x276F
    Clock = [char]0xF017; Fail = [char]0xF00D; Bolt = [char]0xF0E7; Python = [char]0xE73C; Dots = [char]0x2026
}

$script:IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
$script:FirstPrompt = $true

# Branch atual lida direto de .git/HEAD (sem chamar git, para o prompt ficar instantaneo).
function script:Get-GitBranch([string]$dir) {
    while ($dir) {
        $git = Join-Path $dir '.git'
        if (Test-Path $git -PathType Container) { $head = Join-Path $git 'HEAD'; break }
        if (Test-Path $git -PathType Leaf) {
            $target = (Get-Content $git -TotalCount 1) -replace '^gitdir:\s*', ''
            if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path $dir $target }
            $head = Join-Path $target 'HEAD'; break
        }
        $dir = Split-Path $dir -Parent
    }
    if (-not $head -or -not (Test-Path $head)) { return $null }
    $ref = Get-Content $head -TotalCount 1
    if ($ref -match '^ref: refs/heads/(.+)$') { return $Matches[1] }
    return $ref.Substring(0, [Math]::Min(7, $ref.Length))
}

function script:Format-ShortPath([string]$path) {
    $isHome = $path.StartsWith($HOME, [StringComparison]::OrdinalIgnoreCase)
    if ($isHome) { $path = '~' + $path.Substring($HOME.Length) }
    $parts = $path.TrimEnd('\') -split '\\'
    if ($parts.Count -gt 4) { $path = ($parts[0], $script:G.Dots, $parts[-2], $parts[-1]) -join '\' }
    return @{ Text = $path; Home = $isHome }
}

function prompt {
    $ok = $?
    $code = $global:LASTEXITCODE
    $c = $script:C; $g = $script:G

    $loc = $ExecutionContext.SessionState.Path.CurrentLocation
    $raw = if ($loc.Provider.Name -eq 'FileSystem') { $loc.ProviderPath } else { $loc.Path }
    $p = Format-ShortPath $raw
    $icon = if ($p.Home -and $p.Text -eq '~') { $g.Home } else { $g.Folder }

    # Linha 1: contexto pequeno e discreto acima da linha de comando.
    $line = "$($c.Foam)$icon $($c.Subtle)$($p.Text)"
    if ($loc.Provider.Name -eq 'FileSystem') {
        $branch = Get-GitBranch $loc.ProviderPath
        if ($branch) { $line += "  $($c.Iris)$($g.Branch) $branch" }
    }
    if ($env:VIRTUAL_ENV) { $line += "  $($c.Gold)$($g.Python) $(Split-Path $env:VIRTUAL_ENV -Leaf)" }

    $last = Get-History -Count 1
    if ($last) {
        $secs = ($last.EndExecutionTime - $last.StartExecutionTime).TotalSeconds
        if ($secs -ge 2) {
            $txt = if ($secs -ge 60) { '{0}m{1:00}s' -f [int][Math]::Floor($secs / 60), [int]($secs % 60) } else { '{0:0.0}s' -f $secs }
            $line += "  $($c.Muted)$($g.Clock) $txt"
        }
    }
    if (-not $ok) {
        $err = if ($code -and $code -ne 0) { " $code" } else { '' }
        $line += "  $($c.Love)$($g.Fail)$err"
    }
    if ($script:IsAdmin) { $line += "  $($c.Love)$($g.Bolt) admin" }

    # Linha 2: o cursor. A seta fica vermelha quando o comando anterior falhou.
    $arrowColor = if ($ok) { $c.Iris } else { $c.Love }
    $sep = if ($script:FirstPrompt) { '' } else { "`n" }
    $script:FirstPrompt = $false

    $host.UI.RawUI.WindowTitle = $p.Text
    "$sep$line$($c.Reset)`n$arrowColor$($g.Arrow)$($c.Reset) "
}

# Limpa a tela sem deixar uma linha em branco sobrando no topo.
function Clear-Host { $script:FirstPrompt = $true; [Console]::Write("$script:E[2J$script:E[3J$script:E[H") }
Set-Alias -Name cls -Value Clear-Host -Option AllScope -Force
Set-Alias -Name clear -Value Clear-Host -Option AllScope -Force

# PSReadLine 2.0 (ja vem no Windows): cores da sintaxe e historico.
if (Get-Module -ListAvailable PSReadLine) {
    Import-Module PSReadLine
    Set-PSReadLineOption -BellStyle None -HistoryNoDuplicates -HistorySearchCursorMovesToEnd -ContinuationPrompt '  '
    Set-PSReadLineOption -Colors @{
        Command   = $script:C.Foam
        Parameter = $script:C.Iris
        String    = $script:C.Gold
        Number    = $script:C.Rose
        Variable  = $script:C.Rose
        Operator  = $script:C.Subtle
        Keyword   = $script:C.Pine
        Type      = $script:C.Foam
        Member    = $script:C.Text
        Comment   = $script:C.Muted
        Default   = $script:C.Text
        Error     = $script:C.Love
        Emphasis  = $script:C.Gold
        Selection = "$script:E[48;2;64;61;82m"
        ContinuationPrompt = $script:C.Muted
    }
    # Setas buscam no historico pelo que ja foi digitado; Tab mostra um menu.
    Set-PSReadLineKeyHandler -Key UpArrow -Function HistorySearchBackward
    Set-PSReadLineKeyHandler -Key DownArrow -Function HistorySearchForward
    Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete
    Set-PSReadLineKeyHandler -Key Ctrl+d -Function DeleteCharOrExit
}

# ll: lista o diretorio com icones e cores.
function ll([string]$Path = '.') {
    $c = $script:C
    Get-ChildItem -Force -LiteralPath $Path | Sort-Object { -not $_.PSIsContainer }, Name | ForEach-Object {
        if ($_.PSIsContainer) {
            $icon = [char]0xF07B; $color = $c.Foam; $size = ''
        }
        else {
            $icon = [char]0xF15B; $color = $c.Text
            switch -Regex ($_.Extension) {
                '^\.(ps1|psm1|psd1)$' { $icon = [char]0xE795; $color = $c.Pine }
                '^\.(py)$'            { $icon = [char]0xE73C; $color = $c.Gold }
                '^\.(js|ts|json)$'    { $icon = [char]0xE781; $color = $c.Gold }
                '^\.(cs)$'            { $icon = [char]0xF81A; $color = $c.Iris }
                '^\.(md|txt|log)$'    { $icon = [char]0xF48A; $color = $c.Subtle }
                '^\.(exe|dll|lnk)$'   { $icon = [char]0xF489; $color = $c.Love }
                '^\.(zip|7z|rar)$'    { $icon = [char]0xF410; $color = $c.Rose }
                '^\.(png|jpg|jpeg|gif|svg)$' { $icon = [char]0xF1C5; $color = $c.Rose }
                '^\.(xlsx|csv)$'      { $icon = [char]0xF1C3; $color = $c.Pine }
            }
            $len = $_.Length
            $size = if ($len -ge 1GB) { '{0:0.0}G' -f ($len / 1GB) } elseif ($len -ge 1MB) { '{0:0.0}M' -f ($len / 1MB) } elseif ($len -ge 1KB) { '{0:0.0}K' -f ($len / 1KB) } else { "${len}B" }
        }
        $date = $_.LastWriteTime.ToString('dd/MM HH:mm')
        "{0}{1,7}  {2}  {3}{4} {5}{6}" -f $c.Muted, $size, $date, $color, $icon, $_.Name, $c.Reset
    }
}

function .. { Set-Location .. }
function ... { Set-Location ..\.. }

# ---------------------------------------------------------------- fetch
# Estilo fastfetch, sem instalar nada. Dados lentos (CIM) ficam em cache por 7 dias;
# o resto e lido na hora. Rode "fetch" a qualquer momento, ou "fetch -Refresh" para refazer o cache.
function fetch([switch]$Refresh) {
    $c = $script:C
    $cache = Join-Path $env:LOCALAPPDATA 'psfetch-cache.json'
    $info = $null
    if (-not $Refresh -and (Test-Path $cache) -and ((Get-Date) - (Get-Item $cache).LastWriteTime).TotalDays -lt 7) {
        try { $info = Get-Content $cache -Raw | ConvertFrom-Json } catch { $info = $null }
    }
    if (-not $info) {
        $os = Get-CimInstance Win32_OperatingSystem
        $cs = Get-CimInstance Win32_ComputerSystem
        $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
        $gpus = @(Get-CimInstance Win32_VideoController | Where-Object { $_.Name -notmatch 'Basic Display|Remote|Virtual' })
        $info = [pscustomobject]@{
            OS      = ($os.Caption -replace '^Microsoft\s+', '')
            Host    = ("$($cs.Manufacturer) $($cs.Model)" -replace '\s+', ' ').Trim()
            CPU     = (($cpu.Name -replace '\(R\)|\(TM\)|CPU|@.*$', '' -replace '\s+', ' ').Trim() + " ($($cpu.NumberOfLogicalProcessors))")
            GPU     = @($gpus | ForEach-Object { $_.Name })
            Display = @($gpus | Where-Object CurrentHorizontalResolution | ForEach-Object {
                    "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution) @ $($_.CurrentRefreshRate)Hz" })
        }
        try { $info | ConvertTo-Json | Set-Content $cache -Encoding UTF8 } catch { }
    }

    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $ticks = [BitConverter]::ToUInt32([BitConverter]::GetBytes([Environment]::TickCount), 0)
    $up = [TimeSpan]::FromMilliseconds($ticks)
    $upTxt = (@(
            $(if ($up.Days) { "$($up.Days)d" }), $(if ($up.Hours) { "$($up.Hours)h" }), "$($up.Minutes)m"
        ) | Where-Object { $_ }) -join ' '

    Add-Type -AssemblyName Microsoft.VisualBasic
    $ci = New-Object Microsoft.VisualBasic.Devices.ComputerInfo
    $memTot = $ci.TotalPhysicalMemory / 1GB; $memUsed = $memTot - $ci.AvailablePhysicalMemory / 1GB
    $drive = New-Object IO.DriveInfo 'C'
    $dTot = $drive.TotalSize / 1GB; $dUsed = $dTot - $drive.AvailableFreeSpace / 1GB

    $wm = 'DWM'
    $m = $null
    if ([Threading.Mutex]::TryOpenExisting('Local\HyprWin', [ref]$m)) { $wm = 'HyprWin (dwindle)'; $m.Dispose() }
    $term = if ($env:WT_SESSION) { 'Windows Terminal' } else { $Host.Name }

    function Pct([double]$used, [double]$tot) {
        $p = [int](100 * $used / $tot)
        $col = if ($p -ge 85) { $c.Love } elseif ($p -ge 60) { $c.Gold } else { $c.Foam }
        '{0:0.0} GiB / {1:0.0} GiB ({2}{3}%{4})' -f $used, $tot, $col, $p, $c.Text
    }

    $user = "$env:USERNAME@$env:COMPUTERNAME".ToLower()
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("$($c.Iris)$($env:USERNAME.ToLower())$($c.Text)@$($c.Foam)$($env:COMPUTERNAME.ToLower())")
    $lines.Add("$($c.Muted)" + ([string][char]0x2500 * $user.Length))
    $kv = [ordered]@{
        'OS'       = "$($info.OS) $($cv.DisplayVersion) x64"
        'Host'     = $info.Host
        'Kernel'   = "WIN32_NT $([Environment]::OSVersion.Version.ToString(3)).$($cv.UBR)"
        'Uptime'   = $upTxt
        'Shell'    = "PowerShell $($PSVersionTable.PSVersion.ToString(2))"
        'Display'  = ($info.Display -join ', ')
        'WM'       = $wm
        'Terminal' = $term
        'Font'     = 'JetBrainsMono Nerd Font'
        'CPU'      = $info.CPU
        'GPU'      = ($info.GPU -join ', ')
        'Memory'   = (Pct $memUsed $memTot)
        'Disk (C:)' = (Pct $dUsed $dTot)
        'Locale'   = (Get-Culture).Name
    }
    foreach ($k in $kv.Keys) { if ($kv[$k]) { $lines.Add("$($c.Iris)$k$($c.Muted): $($c.Text)$($kv[$k])") } }
    $lines.Add('')
    $blk = [string][char]0x2588 * 3
    $e = $script:E
    $lines.Add((30..37 | ForEach-Object { "$e[${_}m$blk" }) -join '')
    $lines.Add((90..97 | ForEach-Object { "$e[${_}m$blk" }) -join '')

    # Logo do Windows em quatro blocos com a paleta Rose Pine.
    $b = [string][char]0x2588 * 8
    $logo = @()
    foreach ($row in 0..8) {
        if ($row -eq 4) { $logo += ' ' * 18; continue }
        $top = $row -lt 4
        $l = if ($top) { $c.Foam } else { $c.Pine }
        $r = if ($top) { $c.Iris } else { $c.Rose }
        $logo += "$l$b  $r$b"
    }

    ''
    $n = [Math]::Max($logo.Count, $lines.Count)
    for ($i = 0; $i -lt $n; $i++) {
        $left = if ($i -lt $logo.Count) { $logo[$i] } else { ' ' * 18 }
        $right = if ($i -lt $lines.Count) { $lines[$i] } else { '' }
        "  $left$($c.Reset)    $right$($c.Reset)"
    }
}

# Mostra o fetch so em sessoes interativas (nao em "powershell -Command" ou -File).
$script:CmdArgs = [Environment]::GetCommandLineArgs() | Select-Object -Skip 1
if ($Host.Name -eq 'ConsoleHost' -and -not ($script:CmdArgs | Where-Object { $_ -match '^[-/](c|command|f|file|e|ec|encodedcommand|noninteractive)$' })) {
    fetch
    $script:FirstPrompt = $false
}
