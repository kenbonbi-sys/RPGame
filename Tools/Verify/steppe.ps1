# Rebuild and verify T61 without opening a game or editor window.
#   powershell -ExecutionPolicy Bypass -File Tools/Verify/steppe.ps1
# Logs, NUnit XML and the 18-frame tour are kept together under Logs/SteppeVerify.
param(
    [string]$Unity,
    [string]$Out,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Unity) {
    $versionText = Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw
    $version = [regex]::Match($versionText, '(?m)^m_EditorVersion: (\S+)').Groups[1].Value
    $Unity = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity not found: $Unity" }
if (-not $Out) { $Out = Join-Path $project ('Logs/SteppeVerify/' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Out = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Path $Out -Force | Out-Null
$shots = Join-Path $Out 'screenshots'
New-Item -ItemType Directory -Path $shots -Force | Out-Null

function Invoke-Phase([string]$Name, [string]$Exe, [string[]]$Arguments, [int]$Timeout) {
    Write-Host "$Name ..."
    # Start-Process joins ArgumentList with spaces, so preserve paths containing spaces.
    $quoted = $Arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
    $process = Start-Process -FilePath $Exe -ArgumentList $quoted -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
        while (-not $process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -ge $deadline) {
                $process.Kill()
                throw "$Name timed out after $Timeout seconds. Logs: $Out"
            }
        }
        if ($process.ExitCode -ne 0) { throw "$Name exited with $($process.ExitCode). Logs: $Out" }
    }
    finally { $process.Dispose() }
}

$common = @('-batchmode', '-nographics', '-projectPath', $project)
$xml = Join-Path $Out 'tests.xml'
Invoke-Phase 'Steppe, cave and quest tests' $Unity ($common + @(
    '-runTests', '-testPlatform', 'EditMode',
    '-testFilter', 'RPG.EditorTools.Tests.SteppeTests;RPG.EditorTools.Tests.CaveTests;RPG.EditorTools.Tests.QuestRoadTests',
    '-testResults', $xml, '-logFile', (Join-Path $Out 'tests.log')
)) 300
[xml]$results = Get-Content -LiteralPath $xml
$run = $results.'test-run'
if ($run.result -ne 'Passed' -or [int]$run.total -lt 8) {
    throw "Tests did not all pass: $($run.passed)/$($run.total). See $xml"
}

if (-not $SkipBuild) {
    Invoke-Phase 'Windows build' $Unity ($common + @(
        '-quit', '-executeMethod', 'RPG.EditorTools.Batch.BuildPlayer',
        '-logFile', (Join-Path $Out 'build.log')
    )) 600
}
$game = Join-Path $project 'Builds/Windows/RungThiTham.exe'
Invoke-Phase 'Steppe photo tour' $game @(
    '-batchmode', '-force-d3d11', '-autoshot', '-autoshotOnly', 'steppe',
    '-autoshotOffscreen', '-autoshotDir', $shots, '-autoshotTimeout', '180',
    '-screen-width', '1600', '-screen-height', '900', '-screen-fullscreen', '0',
    '-logFile', (Join-Path $Out 'player.log')
) 210

# Existence alone is insufficient: a hidden player's backbuffer can be entirely black.
Add-Type -AssemblyName System.Drawing
$names = @(
    'steppe_tunnel', 'steppe_gate', 'steppe_camp', 'steppe_gust', 'windcolumn',
    'windcolumn_flight', 'windcolumn_landed', 'hyena_lunge_windup', 'hyena_lunge',
    'hyena_pack', 'eagle_mark', 'eagle_dive', 'eagle_grounded', 'bison_windup',
    'bison_charge', 'bison_dazed', 'steppe_night', 'world_map_steppe'
)
for ($i = 0; $i -lt $names.Count; $i++) {
    $path = Join-Path $shots ('{0:D2}_{1}.png' -f $i, $names[$i])
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing screenshot: $path" }
    $bitmap = [Drawing.Bitmap]::new($path)
    try {
        if ($bitmap.Width -ne 1600 -or $bitmap.Height -ne 900) { throw "Unexpected image size: $path" }
        $low = 765
        $high = 0
        for ($y = 20; $y -lt $bitmap.Height; $y += 40) {
            for ($x = 20; $x -lt $bitmap.Width; $x += 40) {
                $pixel = $bitmap.GetPixel($x, $y)
                $value = [int]$pixel.R + [int]$pixel.G + [int]$pixel.B
                $low = [Math]::Min($low, $value)
                $high = [Math]::Max($high, $value)
            }
        }
        if ($high - $low -lt 30) { throw "Blank or nearly uniform screenshot: $path" }
    }
    finally { $bitmap.Dispose() }
}
Write-Host "PASS: $($run.passed) tests, $($names.Count) nonblank screenshots. $Out"
