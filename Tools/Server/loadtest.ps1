# Thử tải máy chủ với bản build (Debug/LoadBot.cs, Docs/KeHoach-Online.md giai đoạn 5): một máy chủ thật trên cổng thử
# (dữ liệu tạm, không đụng máy chủ đang chạy) và -Bots người chơi máy chạy nền, đi đánh quái, dùng chiêu và lướt.
# Trong lúc chạy in số người, FPS và bộ nhớ của máy chủ (file trạng thái nó ghi 10 giây một lần). Bot cuối chơi lâu hơn:
# khi các bot khác xong, xin máy chủ tắt đúng cách (như stop-server.ps1) và kiểm tra nó lưu bot còn đang chơi rồi mới
# thoát, log không lỗi, không kéo ai về chỗ cũ vì đi quá nhanh.
# Mỗi bot tốn khoảng 150–250 MB RAM: trên một máy 16 GB, 10–15 bot là vừa. Kết quả: mã thoát 0 = đạt.
# Dùng: powershell -ExecutionPolicy Bypass -File Tools/Server/loadtest.ps1 -Bots 12 -Seconds 150
param(
    [string]$Game = (Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))) "Builds\Windows\RungThiTham.exe"),
    [string]$Out = (Join-Path $env:TEMP "rtt_loadtest"),
    [int]$Bots = 10,
    [int]$Seconds = 120,
    [int]$Port = 7799,
    [float]$MinFps = 30
)

$ErrorActionPreference = "Stop"
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$data = Join-Path $Out "server-data"

$serverLog = Join-Path $Out "server.log"
$server = Start-Process -FilePath $Game -PassThru -ArgumentList @("-server", "-channel", "1", "-port", "$Port", "-maxplayers", "$([Math]::Max(20, $Bots))",
    "-batchmode", "-nographics", "-data", "`"$data`"", "-logFile", "`"$serverLog`"")
Start-Sleep -Seconds 8
Write-Host "Máy chủ thử chạy (PID $($server.Id), cổng $Port). Bật $Bots bot, mỗi bot chơi $Seconds giây..."

$botProcs = @()
for ($i = 1; $i -le $Bots; $i++) {
    $log = Join-Path $Out "bot$i.log"
    $play = if ($i -eq $Bots) { $Seconds + 300 } else { $Seconds }   # the last one stays: the server stops under it
    $botProcs += Start-Process -FilePath $Game -PassThru -ArgumentList @("-client", "127.0.0.1", "-port", "$Port", "-login", "Bot$i", "matkhau", "-register",
        "-bot", "-botSeconds", "$play", "-batchmode", "-nographics", "-logFile", "`"$log`"")
    Start-Sleep -Milliseconds 700
}
$stayer = $botProcs[$botProcs.Count - 1]
$others = @($botProcs | Where-Object { $_ -ne $stayer })

$status = Join-Path $data "status-k1.json"
$samples = @()
$deadline = (Get-Date).AddSeconds($Seconds + 90)
while ((Get-Date) -lt $deadline -and @($others | Where-Object { -not $_.HasExited }).Count -gt 0) {
    Start-Sleep -Seconds 10
    try { $s = Get-Content $status -Raw -Encoding UTF8 | ConvertFrom-Json } catch { continue }
    $botMem = 0
    foreach ($b in $botProcs) { if (-not $b.HasExited) { try { $b.Refresh(); $botMem += $b.WorkingSet64 } catch { } } }
    $server.Refresh()
    $cpu = $server.TotalProcessorTime.TotalSeconds
    $cpuShare = if ($lastCpu) { 100 * ($cpu - $lastCpu) / ((Get-Date) - $lastAt).TotalSeconds } else { 0 }
    $lastCpu = $cpu; $lastAt = Get-Date
    $s | Add-Member -NotePropertyName cpu -NotePropertyValue $cpuShare -Force
    $s | Add-Member -NotePropertyName workingMB -NotePropertyValue ([int]($server.WorkingSet64 / 1MB)) -Force
    $samples += $s
    Write-Host ("  {0:HH:mm:ss}  {1}/{2} người  ·  máy chủ {3:0} FPS, {4:0}% một nhân CPU, {5} MB  ·  các bot {6:0} MB" -f (Get-Date), $s.players, $s.maxPlayers, $s.fps, $cpuShare, $s.workingMB, ($botMem / 1MB))
}
foreach ($b in $others) { if (-not $b.HasExited) { $b.Kill(); Write-Host "  bot $($b.Id) did not finish in time" } }

# tắt máy chủ đúng cách khi bot cuối còn chơi: nó báo người chơi, lưu mọi nhân vật rồi thoát (Net/ServerStopSignal.cs)
$asked = $false
try {
    $signal = [System.Threading.EventWaitHandle]::OpenExisting("Local\RungThiTham-Stop-$($server.Id)")
    $asked = $signal.Set()
    $signal.Dispose()
} catch { }
$stopped = $asked -and $server.WaitForExit(30000)
if (-not $stopped -and -not $server.HasExited) { $server.Kill() }
if (-not $stayer.WaitForExit(40000)) { $stayer.Kill(); Write-Host "  the last bot did not leave after the server stopped" }

$full = @($samples | Where-Object { $_.players -ge [Math]::Ceiling($Bots * 0.8) })
$fps = @($full | ForEach-Object { [double]$_.fps })
$minFps = if ($fps.Count) { ($fps | Measure-Object -Minimum).Minimum } else { 0 }
$avgFps = if ($fps.Count) { ($fps | Measure-Object -Average).Average } else { 0 }
$maxMem = if ($samples.Count) { ($samples | ForEach-Object { [int]$_.workingMB } | Measure-Object -Maximum).Maximum } else { 0 }
$maxCpu = if ($full.Count) { ($full | ForEach-Object { [double]$_.cpu } | Measure-Object -Maximum).Maximum } else { 0 }
$most = if ($samples.Count) { ($samples | ForEach-Object { [int]$_.players } | Measure-Object -Maximum).Maximum } else { 0 }
$lines = Get-Content $serverLog -Encoding UTF8 -ErrorAction SilentlyContinue
$reported = if ($samples.Count) { [int]$samples[$samples.Count - 1].errors } else { 0 }
$errors = @($lines | Where-Object { $_ -match "Exception|\[Server\] could not" })
if ($reported -gt $errors.Count) { $errors += "the server counted $reported errors (last: $($samples[$samples.Count - 1].lastError))" }
$putBack = @($lines | Where-Object { $_ -match "put back" })
$saved = @($lines | Where-Object { $_ -match 'saved ".*" \(server stopping\)' })
$askedLine = @($lines | Where-Object { $_ -match "asked to stop" })
$botsOk = @($botProcs | Where-Object { $_.ExitCode -eq 0 }).Count
Get-ChildItem $Out -Filter "bot*.log" | ForEach-Object { Select-String -Path $_.FullName -Pattern "\[Bot\]" | Select-Object -Last 1 | ForEach-Object { Write-Host "  $($_.Line)" } }

Write-Host ""
Write-Host ("Kết quả: nhiều nhất {0}/{1} người cùng lúc, FPS máy chủ khi đông: thấp nhất {2:0.#}, trung bình {3:0.#}; CPU nhiều nhất {4:0}% một nhân; bộ nhớ nhiều nhất {5} MB" -f $most, $Bots, $minFps, $avgFps, $maxCpu, $maxMem)
Write-Host ("Bot xong: {0}/{1}; lỗi trong log máy chủ: {2}; bị kéo về vì đi quá nhanh: {3}; tắt đúng cách: {4} (lưu {5} nhân vật lúc tắt)" -f $botsOk, $Bots, $errors.Count, $putBack.Count, ($stopped -and $askedLine.Count -gt 0), $saved.Count)
$errors | Select-Object -First 5 | ForEach-Object { Write-Host "   $_" }
$putBack | Select-Object -First 5 | ForEach-Object { Write-Host "   $_" }
$ok = $most -ge $Bots -and $minFps -ge $MinFps -and $errors.Count -eq 0 -and $putBack.Count -eq 0 -and $botsOk -eq $Bots -and $stopped -and $saved.Count -ge 1
if ($ok) { Write-Host "LOADTEST OK"; exit 0 } else { Write-Host "LOADTEST FAILED (log: $Out)"; exit 1 }
