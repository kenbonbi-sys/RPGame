# Dừng máy chủ Rừng Thì Thầm đang chạy trên máy này. Người đang chơi bị ngắt; nhân vật của họ đã được lưu
# trong vòng 30 giây trước đó. Tác vụ tự chạy khi đăng nhập vẫn giữ: lần đăng nhập sau máy chủ lại chạy.
# Gỡ hẳn: Unregister-ScheduledTask -TaskName "RungThiTham Server"
param(
    [string]$Target = (Join-Path $env:LOCALAPPDATA "RungThiTham-Server"),
    [switch]$Quiet
)

# dừng vòng tự bật lại trước (tác vụ lúc đăng nhập, hay run-server.ps1 chạy tay), rồi tới máy chủ
$task = Get-ScheduledTask -TaskName "RungThiTham Server" -ErrorAction SilentlyContinue
if ($task -and $task.State -eq "Running") { Stop-ScheduledTask -TaskName "RungThiTham Server" }
$runner = Join-Path $Target "run-server.ps1"
$loops = Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe' OR Name = 'pwsh.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($runner, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
foreach ($l in $loops) { Stop-Process -Id $l.ProcessId -Force -ErrorAction SilentlyContinue }
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
