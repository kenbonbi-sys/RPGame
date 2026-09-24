# Cài máy chủ luôn bật của Rừng Thì Thầm trên máy này (Docs/MayChu.md):
#   1. chép bản build Builds\Windows vào thư mục máy chủ (mặc định %LOCALAPPDATA%\RungThiTham-Server\game),
#      giữ nguyên dữ liệu người chơi trong ...\data;
#   2. đăng ký tác vụ "RungThiTham Server" chạy run-server.ps1 ẩn mỗi khi bạn đăng nhập Windows;
#   3. bật máy chủ ngay.
# Chạy lại file này sau mỗi lần build game mới để cập nhật máy chủ (dữ liệu không mất).
# Tường lửa: xem hướng dẫn ở cuối (cần quyền quản trị, bạn tự chạy lệnh).
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA "RungThiTham-Server"),
    [string]$Build = (Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))) "Builds\Windows"),
    [switch]$NoAutostart
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$taskName = "RungThiTham Server"

if (-not (Test-Path (Join-Path $Build "RungThiTham.exe"))) {
    Write-Host "Không thấy bản build ở $Build. Hãy build game trước (Tools/RPG/Build Windows Player)."
    exit 1
}

Write-Host "Thư mục máy chủ: $Target"
New-Item -ItemType Directory -Force -Path $Target | Out-Null

# dừng máy chủ đang chạy (nếu có) để chép đè được
& (Join-Path $here "stop-server.ps1") -Target $Target -Quiet

$game = Join-Path $Target "game"
if (Test-Path $game) { Remove-Item $game -Recurse -Force }
Copy-Item $Build $game -Recurse
Remove-Item (Join-Path $game "RungThiTham_BackUpThisFolder_ButDontShipItWithYourGame") -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $here "run-server.ps1") $Target -Force
Copy-Item (Join-Path $here "stop-server.ps1") $Target -Force
Write-Host "Đã chép bản build mới. Dữ liệu người chơi giữ nguyên trong $(Join-Path $Target 'data')."

$runner = Join-Path $Target "run-server.ps1"
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$runner`""
if (-not $NoAutostart) {
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description "Máy chủ game Rừng Thì Thầm (luôn bật khi bạn đăng nhập)" -Force | Out-Null
    Write-Host "Đã đăng ký tác vụ '$taskName': máy chủ tự chạy mỗi khi bạn đăng nhập Windows."
    Start-ScheduledTask -TaskName $taskName
} else {
    Start-Process powershell.exe -ArgumentList "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$runner`"" -WindowStyle Hidden
}
Write-Host "Máy chủ đang chạy. Log: $(Join-Path $Target 'logs')"
Write-Host ""
Write-Host "Tường lửa (làm một lần): mở PowerShell bằng 'Run as administrator' rồi chạy:"
Write-Host "  New-NetFirewallRule -DisplayName 'Rung Thi Tham Server' -Direction Inbound -Protocol UDP -LocalPort 7770,7771 -Action Allow -Profile Any"
Write-Host "Và đặt Windows không tự ngủ khi cắm điện (Settings > System > Power & sleep > Sleep: Never), nếu không máy chủ dừng khi máy ngủ."
