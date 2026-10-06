$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'artifacts\win-x64\DoubaoVoiceHotkey.exe'
if (-not (Test-Path $exe)) {
    throw "Build not found: $exe. Run scripts\build.ps1 first."
}

& $exe --probe
