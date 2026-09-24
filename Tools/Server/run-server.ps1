# Chạy máy chủ Rừng Thì Thầm và giữ nó luôn chạy: tự bật lại sau 5 giây nếu nó tắt hay lỗi.
# Dữ liệu (tài khoản, nhân vật, bản sao lưu hằng ngày) nằm trong thư mục data cạnh file này.
# Log mỗi lần chạy: logs\server-<ngày giờ>.log (giữ 30 file mới nhất).
# Dùng: install-server.ps1 cài và đăng ký chạy khi đăng nhập Windows; chạy tay: powershell -File run-server.ps1
param(
    [int]$Port = 7770,
    [int]$MaxPlayers = 20,
    [string]$ServerName = "Rừng Thì Thầm"
)

$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "game\RungThiTham.exe"
$data = Join-Path $here "data"
$logs = Join-Path $here "logs"
$stopFlag = Join-Path $here "stop.flag"
New-Item -ItemType Directory -Force -Path $data, $logs | Out-Null
if (Test-Path $stopFlag) { Remove-Item $stopFlag -Force }

if (-not (Test-Path $exe)) {
    Write-Host "Không thấy $exe. Chạy install-server.ps1 trước."
    exit 1
}

while ($true) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $log = Join-Path $logs "server-$stamp.log"
    $args = @("-server", "-batchmode", "-nographics", "-port", "$Port", "-maxplayers", "$MaxPlayers",
              "-servername", "`"$ServerName`"", "-data", "`"$data`"", "-logFile", "`"$log`"")
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Máy chủ chạy (log: $log)"
    $p = Start-Process -FilePath $exe -ArgumentList $args -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Máy chủ đã tắt (mã $($p.ExitCode))"
    Get-ChildItem $logs -Filter "server-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -Skip 30 | Remove-Item -Force
    if (Test-Path $stopFlag) {
        Remove-Item $stopFlag -Force
        Write-Host "Đã dừng theo yêu cầu (stop-server.ps1)."
        break
    }
    Start-Sleep -Seconds 5
}
