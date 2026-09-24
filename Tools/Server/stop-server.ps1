# Dừng máy chủ Rừng Thì Thầm đang chạy trên máy này. Máy chủ lưu mọi nhân vật trước khi tắt
# (người đang chơi bị ngắt). Tác vụ tự chạy khi đăng nhập vẫn giữ: lần đăng nhập sau máy chủ lại chạy.
# Gỡ hẳn: Unregister-ScheduledTask -TaskName "RungThiTham Server"
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA "RungThiTham-Server"),
    [switch]$Quiet
)

$flag = Join-Path $Target "stop.flag"
if (Test-Path $Target) { Set-Content -Path $flag -Value "stop" }
$game = Join-Path $Target "game"
$procs = Get-Process -Name "RungThiTham" -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($game, [StringComparison]::OrdinalIgnoreCase) }
foreach ($p in $procs) {
    # CloseMainWindow does nothing for a windowless server: stop it; characters were saved at most 30 s ago
    Stop-Process -Id $p.Id -Force
}
if (-not $Quiet) {
    if ($procs) { Write-Host "Đã dừng máy chủ." } else { Write-Host "Máy chủ không chạy." }
}
Start-Sleep -Seconds 1
