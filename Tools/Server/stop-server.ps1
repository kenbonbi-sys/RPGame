# Dừng máy chủ Rừng Thì Thầm đang chạy trên máy này: máy chủ báo cho người đang chơi, lưu mọi nhân vật rồi tắt
# (game của họ tự vào lại khi máy chủ chạy lại). Tác vụ tự chạy khi đăng nhập vẫn giữ: lần đăng nhập sau máy chủ lại chạy.
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
    # ask it to stop (Net/ServerStopSignal.cs): it saves everyone and quits; kill it only if it does not
    $asked = $false
    try {
        $signal = [System.Threading.EventWaitHandle]::OpenExisting("Local\RungThiTham-Stop-$($p.Id)")
        $asked = $signal.Set()
        $signal.Dispose()
    } catch { }
    if (-not ($asked -and $p.WaitForExit(20000))) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
if (-not $Quiet) {
    if ($procs) { Write-Host "Đã dừng máy chủ." } else { Write-Host "Máy chủ không chạy." }
}
Start-Sleep -Seconds 1
