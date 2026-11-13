Add-Type @"
using System;
using System.Runtime.InteropServices;

public class WinApi {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@

Add-Type -AssemblyName System.Windows.Forms

# Prefer repo-relative checkout; keep C:\Scripts for shared logs.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$paths = @(
    'C:\Scripts',
    $repoRoot,
    'C:\data',
    '\\192.168.1.1\g\my datas'
)

$ws = New-Object -ComObject WScript.Shell

function Focus-ExplorerWindow {
    param($Handle)

    [WinApi]::ShowWindow($Handle, 5) | Out-Null
    Start-Sleep -Milliseconds 200

    [WinApi]::SetForegroundWindow($Handle) | Out-Null
    Start-Sleep -Milliseconds 500
}

Write-Host ""
Write-Host "Explorer başlatılıyor..."
Write-Host ""

# İlk pencere
Start-Process explorer.exe $paths[0]

Start-Sleep -Seconds 3

# Explorer handle bul
$explorer = Get-Process explorer |
    Where-Object { $_.MainWindowHandle -ne 0 } |
    Sort-Object StartTime -Descending |
    Select-Object -First 1

if (-not $explorer) {
    Write-Host "Explorer bulunamadı!"
    pause
    exit
}

$hwnd = $explorer.MainWindowHandle

for ($i = 1; $i -lt $paths.Count; $i++) {

    $path = $paths[$i]

    Write-Host "Açılıyor: $path"

    Focus-ExplorerWindow $hwnd

    # Yeni sekme
    $ws.SendKeys("^t")
    Start-Sleep -Milliseconds 800

    # Adres çubuğu
    $ws.SendKeys("%d")
    Start-Sleep -Milliseconds 400

    # Clipboard'a koy
    [System.Windows.Forms.Clipboard]::SetText($path)

    Start-Sleep -Milliseconds 200

    # Yapıştır
    $ws.SendKeys("^v")

    Start-Sleep -Milliseconds 300

    # Git
    $ws.SendKeys("{ENTER}")

    Start-Sleep -Milliseconds 1800
}

Write-Host ""
Write-Host "Tüm sekmeler açıldı."
Write-Host ""

pause