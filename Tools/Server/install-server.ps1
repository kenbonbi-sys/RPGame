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
    [switch]$NoAutostart,
    [switch]$Direct   # nội bộ: lần chạy qua Task Scheduler bên dưới, không kiểm tra lại
)

$ErrorActionPreference = "Stop"
trap { Write-Host "Lỗi: $_"; exit 1 }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$taskName = "RungThiTham Server"
$Target = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Target)
$Build = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Build)

# Chương trình mở từ một ứng dụng dạng gói MSIX (ví dụ Claude desktop) có thể chỉ thấy một bản AppData riêng:
# file ghi vào %LOCALAPPDATA% thật ra nằm trong %LOCALAPPDATA%\Packages\<gói>\LocalCache\Local, còn tác vụ
# chạy khi đăng nhập thì không thấy chúng. Khi đó chạy lại script này qua Task Scheduler, nơi thấy AppData thật.
function Test-PrivateAppData {
    $probe = "RungThiTham-probe-" + [guid]::NewGuid().ToString("N")
    $file = Join-Path $env:LOCALAPPDATA $probe
    try {
        Set-Content -Path $file -Value ""
        return (Test-Path (Join-Path $env:LOCALAPPDATA "Packages\*\LocalCache\Local\$probe"))
    } finally {
        Remove-Item $file -Force -ErrorAction SilentlyContinue
    }
}

if (-not $Direct -and (Test-PrivateAppData)) {
    Write-Host "Chương trình đang chạy script này chỉ thấy một bản AppData riêng: cài qua Task Scheduler..."
    $log = Join-Path $env:TEMP "RungThiTham-install.log"   # Temp thì hai bên cùng thấy
    Remove-Item $log -Force -ErrorAction SilentlyContinue
    function Quote([string]$s) { "'" + $s.Replace("'", "''") + "'" }
    $call = "& $(Quote $PSCommandPath) -Direct -Target $(Quote $Target) -Build $(Quote $Build)"
    if ($NoAutostart) { $call += " -NoAutostart" }
    $call += " *>&1 | Out-File -FilePath $(Quote $log) -Encoding utf8; exit `$LASTEXITCODE"
    $installTask = "RungThiTham Install"
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command `"$call`""
    Register-ScheduledTask -TaskName $installTask -Action $action -Force | Out-Null
    try {
        Start-ScheduledTask -TaskName $installTask
        $deadline = (Get-Date).AddMinutes(10)
        do { Start-Sleep -Seconds 1 } while ((Get-ScheduledTask -TaskName $installTask).State -in "Queued", "Running" -and (Get-Date) -lt $deadline)
        $code = (Get-ScheduledTaskInfo -TaskName $installTask).LastTaskResult
    } finally {
        Unregister-ScheduledTask -TaskName $installTask -Confirm:$false
    }
    if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Write-Host; Remove-Item $log -Force }
    exit $code
}

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
