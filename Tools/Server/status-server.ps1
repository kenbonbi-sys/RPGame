# Trạng thái máy chủ Rừng Thì Thầm trên máy này (Docs/MayChu.md): tác vụ tự chạy, tiến trình, từng kênh
# (người chơi, thời gian chạy, FPS, bộ nhớ, theo file data\status-k<kênh>.json máy chủ ghi 10 giây một lần),
# câu trả lời khi game của người chơi tìm máy chủ, và cảnh báo / lỗi trong log mới nhất của mỗi kênh.
# Dùng: powershell -ExecutionPolicy Bypass -File Tools/Server/status-server.ps1
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA "RungThiTham-Server"),
    [int]$BasePort = 7770
)

$task = Get-ScheduledTask -TaskName "RungThiTham Server" -ErrorAction SilentlyContinue
if ($task) { Write-Host "Tác vụ tự chạy: $($task.State)" } else { Write-Host "Tác vụ tự chạy: chưa cài (install-server.ps1)" }
$game = Join-Path $Target "game"
$procs = @(Get-Process -Name "RungThiTham" -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($game, [StringComparison]::OrdinalIgnoreCase) })
Write-Host "Tiến trình máy chủ: $($procs.Count)"

function Ask-Server([int]$port) {
    $udp = New-Object System.Net.Sockets.UdpClient
    $udp.Client.ReceiveTimeout = 1500
    try {
        $token = [string](Get-Random)
        $ask = [Text.Encoding]::UTF8.GetBytes("RTT?|$token")
        [void]$udp.Send($ask, $ask.Length, "127.0.0.1", $port + 1)
        $from = New-Object System.Net.IPEndPoint([Net.IPAddress]::Any, 0)
        $reply = [Text.Encoding]::UTF8.GetString($udp.Receive([ref]$from))
        return $reply
    } catch { return $null } finally { $udp.Close() }
}

$data = Join-Path $Target "data"
$files = @(Get-ChildItem $data -Filter "status-k*.json" -ErrorAction SilentlyContinue | Sort-Object Name)
if ($files.Count -eq 0) { Write-Host "Chưa có file trạng thái trong $data (máy chủ chưa chạy bản có giám sát?)" }
foreach ($f in $files) {
    try { $s = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Write-Host "$($f.Name): không đọc được"; continue }
    $age = [int]((Get-Date) - [DateTime]::Parse($s.updated)).TotalSeconds
    $up = [TimeSpan]::FromSeconds($s.uptimeSeconds)
    $who = if ($s.names.Count -gt 0) { " (" + ($s.names -join ", ") + ")" } else { "" }
    $stale = if ($age -gt 30) { "  <-- không cập nhật $age giây: máy chủ kênh này có thể đã dừng" } else { "" }
    Write-Host ("Kênh {0} · cổng {1}: {2}/{3} người{4} · chạy {5:%d} ngày {5:hh\:mm} · {6:0} FPS · {7} MB bộ nhớ Unity · {8} tài khoản · cập nhật {9} giây trước{10}" -f `
        $s.channel, $s.port, $s.players, $s.maxPlayers, $who, $up, $s.fps, $s.memoryMB, $s.accounts, $age, $stale)
    if ($s.errors -gt 0) { Write-Host "   $($s.errors) lỗi, $($s.warnings) cảnh báo từ lúc chạy; lỗi mới nhất: $($s.lastError)" }
    $reply = Ask-Server $s.port
    if ($reply) { Write-Host "   trả lời tìm máy chủ: $reply" } else { Write-Host "   KHÔNG trả lời tìm máy chủ trên cổng $($s.port + 1)" }
    $log = Get-ChildItem (Join-Path $Target "logs") -Filter "server-k$($s.channel)-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($log) {
        $bad = @(Select-String -Path $log.FullName -Pattern "Exception|\[Server\].*(cannot|could not|slow|put back)|Error" -ErrorAction SilentlyContinue |
            Where-Object { $_.Line -notmatch "^Curl error" })   # Unity calling its own services: harmless
        Write-Host "   log: $($log.Name), $($bad.Count) dòng cảnh báo / lỗi"
        $bad | Select-Object -Last 5 | ForEach-Object { Write-Host "     $($_.Line)" }
    }
}
