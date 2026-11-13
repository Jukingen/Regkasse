# kill-ports.ps1 — Regkasse port + Redis temizleyici
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Regkasse Port Kontrol ve Temizleyici" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Yönetici kontrolü
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Host "[UYARI] Yonetici olarak calistirilmadi! Bazi portlar temizlenemeyebilir." -ForegroundColor Yellow
    Write-Host ""
}

$ports = @(
    @{Number=5184; Name="Backend API"},
    @{Number=8081; Name="POS Metro"},
    @{Number=3000; Name="Admin Panel"},
    @{Number=3001; Name="Sites"},
    @{Number=6379; Name="Redis"}
)

$foundPorts = @()

foreach ($port in $ports) {
    Write-Host "[$($port.Number)] $($port.Name) kontrol ediliyor..." -ForegroundColor Yellow
    Write-Host "----------------------------------------"
    
    $connection = netstat -aon 2>$null | Select-String ":$($port.Number) " | Select-String "LISTENING"
    
    if ($connection) {
        # $PID yerine farklı bir değişken adı kullan (processId)
        $processId = ($connection -split '\s+')[-1]
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        
        Write-Host "[!] $($port.Number) portu KULLANILIYOR!" -ForegroundColor Red
        Write-Host "    PID: $processId"
        Write-Host "    Process: $($process.ProcessName).exe"
        if ($process.Path) {
            Write-Host "    Konum: $($process.Path)"
        }
        
        $foundPorts += @{Number=$port.Number; PID=$processId; Name=$process.ProcessName}
    } else {
        Write-Host "[OK] $($port.Number) portu BOS." -ForegroundColor Green
    }
    Write-Host ""
}

# redis-server process (port parse kaçırsa bile)
$redisProcs = @(Get-Process -Name "redis-server" -ErrorAction SilentlyContinue)
if ($redisProcs.Count -gt 0) {
    Write-Host "[redis-server] Process kontrol ediliyor..." -ForegroundColor Yellow
    Write-Host "----------------------------------------"
    foreach ($rp in $redisProcs) {
        Write-Host "[!] redis-server calisiyor!" -ForegroundColor Red
        Write-Host "    PID: $($rp.Id)"
        if ($rp.Path) {
            Write-Host "    Konum: $($rp.Path)"
        }
        $already = $foundPorts | Where-Object { $_.PID -eq "$($rp.Id)" }
        if (-not $already) {
            $foundPorts += @{Number=6379; PID="$($rp.Id)"; Name="redis-server"}
        }
    }
    Write-Host ""
} else {
    Write-Host "[OK] redis-server process yok." -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    Kontrol Tamamlandi" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($foundPorts.Count -gt 0) {
    Write-Host "Kullanilan portlar / processler:" -ForegroundColor Yellow
    foreach ($p in $foundPorts) {
        Write-Host "  - $($p.Number) ($($p.Name)) PID: $($p.PID)" -ForegroundColor White
    }
    Write-Host ""
    
    $response = Read-Host "Portlari ve Redis'i temizlemek istiyor musunuz? (E/H)"
    
    if ($response -eq "E" -or $response -eq "e") {
        Write-Host ""
        Write-Host "Temizleniyor..." -ForegroundColor Yellow
        
        $killedPids = @{}
        foreach ($p in $foundPorts) {
            if ($killedPids.ContainsKey($p.PID)) { continue }
            $killedPids[$p.PID] = $true

            Write-Host "  $($p.Number) / $($p.Name) (PID: $($p.PID)) sonlandiriliyor..."
            $result = taskkill /PID $p.PID /F 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  [OK] PID $($p.PID) sonlandirildi." -ForegroundColor Green
            } else {
                Write-Host "  [HATA] PID $($p.PID) sonlandirilamadi!" -ForegroundColor Red
                Write-Host "         $result" -ForegroundColor Red
                Write-Host "         Yonetici olarak calistirmayi deneyin." -ForegroundColor Yellow
            }
        }

        # Kalan redis-server (varsa)
        Get-Process -Name "redis-server" -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Host "  redis-server (PID: $($_.Id)) sonlandiriliyor..."
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            Write-Host "  [OK] redis-server kapatildi." -ForegroundColor Green
        }

        # Orphan Next.js / Expo workers (RAM leak; parent often already dead)
        # PSScriptRoot = <repo>/scripts/legacy
        $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
        Write-Host ""
        Write-Host "  Orphan Next/Expo node workers temizleniyor (npm run dev:cleanup)..." -ForegroundColor Yellow
        Push-Location $repoRoot
        try {
            npm run dev:cleanup
        } finally {
            Pop-Location
        }
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "    Temizleme Tamamlandi" -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        
        # Tekrar kontrol
        Write-Host ""
        Write-Host "Tekrar kontrol ediliyor..." -ForegroundColor Yellow
        foreach ($port in $ports) {
            $stillListening = netstat -aon 2>$null | Select-String ":$($port.Number) " | Select-String "LISTENING"
            if ($stillListening) {
                Write-Host "  [UYARI] $($port.Number) ($($port.Name)) hala kullaniliyor!" -ForegroundColor Red
            } else {
                Write-Host "  [OK] $($port.Number) ($($port.Name)) BOS." -ForegroundColor Green
            }
        }
        $stillRedis = Get-Process -Name "redis-server" -ErrorAction SilentlyContinue
        if ($stillRedis) {
            Write-Host "  [UYARI] redis-server hala calisiyor!" -ForegroundColor Red
        } else {
            Write-Host "  [OK] redis-server kapali." -ForegroundColor Green
        }
    } else {
        Write-Host "Temizleme yapilmadi." -ForegroundColor Gray
    }
} else {
    Write-Host "Tum portlar bos ve redis-server yok. Temizlenecek bir sey yok." -ForegroundColor Green
}

Write-Host ""
Read-Host "Press Enter to exit"
