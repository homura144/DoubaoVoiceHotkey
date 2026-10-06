$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\DoubaoVoiceHotkey\DoubaoVoiceHotkey.csproj'
$output = Join-Path $repoRoot 'artifacts\win-x64'

Push-Location $repoRoot
try {
    dotnet restore $project
    dotnet build $project -c Release --no-restore
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        --no-restore `
        -o $output
    Write-Host "Built: $output\DoubaoVoiceHotkey.exe"
}
finally {
    Pop-Location
}
