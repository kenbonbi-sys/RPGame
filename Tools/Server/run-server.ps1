# Chạy máy chủ Rừng Thì Thầm và giữ nó luôn chạy: tự bật lại sau 5 giây nếu nó tắt hay lỗi.
# Dừng hẳn: stop-server.ps1 (dừng vòng này rồi tới máy chủ).
# Nhiều kênh (-Channels 2): vòng của kênh 1 bật thêm một vòng cho mỗi kênh khác; kênh n chạy ở cổng 7770 + 2(n - 1),
# mọi kênh dùng chung dữ liệu (một nhân vật chỉ ở một kênh một lúc).
# Dữ liệu (tài khoản, nhân vật, bản sao lưu hằng ngày) nằm trong thư mục data cạnh file này.
# Log mỗi lần chạy: logs\server-k<kênh>-<ngày giờ>.log (mỗi kênh giữ 30 file mới nhất).
# Dùng: install-server.ps1 cài và đăng ký chạy khi đăng nhập Windows; chạy tay: powershell -File run-server.ps1
param(
    [int]$Channel = 1,
    [int]$Channels = 1,
    [int]$BasePort = 7770,
    [int]$MaxPlayers = 20,
    [string]$ServerName = "Rừng Thì Thầm"
)

$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "game\RungThiTham.exe"
$data = Join-Path $here "data"
$logs = Join-Path $here "logs"
New-Item -ItemType Directory -Force -Path $data, $logs | Out-Null

if (-not (Test-Path $exe)) {
    Write-Host "Không thấy $exe. Chạy install-server.ps1 trước."
    exit 1
}

$port = $BasePort + 2 * ($Channel - 1)
if ($Channel -eq 1) {
    for ($k = 2; $k -le $Channels; $k++) {
        Start-Process powershell.exe -WindowStyle Hidden -ArgumentList ("-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$PSCommandPath`" " +
            "-Channel $k -Channels $Channels -BasePort $BasePort -MaxPlayers $MaxPlayers -ServerName `"$ServerName`"")
    }
}

while ($true) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $log = Join-Path $logs "server-k$Channel-$stamp.log"
    $gameArgs = @("-server", "-batchmode", "-nographics", "-channel", "$Channel", "-port", "$port", "-maxplayers", "$MaxPlayers",
                  "-servername", "`"$ServerName`"", "-data", "`"$data`"", "-logFile", "`"$log`"")
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Máy chủ kênh $Channel chạy ở cổng $port (log: $log)"
    $p = Start-Process -FilePath $exe -ArgumentList $gameArgs -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Máy chủ kênh $Channel đã tắt (mã $($p.ExitCode))"
    Get-ChildItem $logs -Filter "server-k$Channel-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -Skip 30 | Remove-Item -Force
    Start-Sleep -Seconds 5
}
