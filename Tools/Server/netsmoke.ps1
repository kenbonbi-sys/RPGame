# Kiểm tra online tự động với bản build (Debug/NetSmoke.cs): một máy chủ và hai người chơi trên máy này.
#   Vòng 1: SmokeA (có cửa sổ, chụp ảnh) và SmokeB (chạy nền) vào, thấy nhau đi, mỗi người đánh chết một Slime Rêu, nhận XP từ máy chủ.
#   Vòng 2: cả hai vào lại; SmokeA phải còn nguyên cấp và XP như lúc thoát (máy chủ đã lưu).
# Kết quả: mã thoát 0 = tất cả qua. Log và ảnh ở thư mục -Out.
param(
    [string]$Game = (Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))) "Builds\Windows\RungThiTham.exe"),
    [string]$Out = (Join-Path $env:TEMP "rtt_netsmoke"),
    [int]$Port = 7795
)

$ErrorActionPreference = "Stop"
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$data = Join-Path $Out "server-data"

function Start-Game($name, [string[]]$more) {
    $log = Join-Path $Out "$name.log"
    $args = $more + @("-port", "$Port", "-netsmoke", "-netsmokeDir", "`"$Out`"", "-logFile", "`"$log`"")
    return Start-Process -FilePath $Game -ArgumentList $args -PassThru
}

function Wait-All($procs, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    foreach ($p in $procs) {
        $left = [int]([Math]::Max(1, ($deadline - (Get-Date)).TotalMilliseconds))
        if (-not $p.WaitForExit($left)) { $p.Kill(); Write-Host "  $($p.Id) did not finish in time" }
    }
}

function Report($name) {
    $log = Join-Path $Out "$name.log"
    $lines = Select-String -Path $log -Pattern "\[NetSmoke\]" -ErrorAction SilentlyContinue | ForEach-Object { $_.Line }
    Write-Host "== $name"
    $lines | ForEach-Object { Write-Host "   $_" }
}

$server = Start-Game "server" @("-server", "-batchmode", "-nographics", "-data", "`"$data`"", "-netsmokeRounds", "2")
Start-Sleep -Seconds 8

Write-Host "Vòng 1: hai người chơi mới"
$a = Start-Game "round1_SmokeA" @("-client", "127.0.0.1", "-login", "SmokeA", "matkhau", "-register", "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0")
$b = Start-Game "round1_SmokeB" @("-client", "127.0.0.1", "-login", "SmokeB", "matkhau", "-register", "-batchmode", "-nographics")
Wait-All @($a, $b) 150
Report "round1_SmokeA"; Report "round1_SmokeB"
$ok = ($a.ExitCode -eq 0) -and ($b.ExitCode -eq 0)

$saved = Select-String -Path (Join-Path $Out "round1_SmokeA.log") -Pattern "character level (\d+) xp (\d+)" | Select-Object -Last 1
if (-not $saved) { Write-Host "Không đọc được cấp / XP của SmokeA ở vòng 1"; $ok = $false; $expect = "1 0" }
else { $expect = "$($saved.Matches[0].Groups[1].Value) $($saved.Matches[0].Groups[2].Value)" }

Write-Host "Vòng 2: vào lại, SmokeA phải còn cấp và XP ($expect)"
$a2 = Start-Game "round2_SmokeA" @("-client", "127.0.0.1", "-login", "SmokeA", "matkhau", "-netsmokeExpect", "`"$expect`"", "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0")
$b2 = Start-Game "round2_SmokeB" @("-client", "127.0.0.1", "-login", "SmokeB", "matkhau", "-batchmode", "-nographics")
Wait-All @($a2, $b2) 150
Report "round2_SmokeA"; Report "round2_SmokeB"
$ok = $ok -and ($a2.ExitCode -eq 0) -and ($b2.ExitCode -eq 0)

Wait-All @($server) 30
Report "server"
$ok = $ok -and ($server.ExitCode -eq 0)
Write-Host ""
Write-Host "Mã thoát: server $($server.ExitCode), vòng 1 A $($a.ExitCode) B $($b.ExitCode), vòng 2 A $($a2.ExitCode) B $($b2.ExitCode)"
if ($ok) { Write-Host "NETSMOKE OK"; exit 0 } else { Write-Host "NETSMOKE FAILED (log: $Out)"; exit 1 }
