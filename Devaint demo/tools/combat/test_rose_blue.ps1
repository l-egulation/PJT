param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor')
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$mono = Join-Path $UnityEditor 'Data/MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $UnityEditor 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
if (!(Test-Path -LiteralPath $mono) -or !(Test-Path -LiteralPath $compiler)) { throw 'Unity 6000.6.5f1 Mono compiler is required.' }
$version = Get-Content (Join-Path $repo 'unity/ProjectSettings/ProjectVersion.txt') -Raw
if ($version -notmatch 'm_EditorVersion: 6000.6.5f1') { throw 'Repository Unity version mismatch.' }
Push-Location $repo
try {
    & python tools/combat/export_chapter2_rose_blue.py --check
    if ($LASTEXITCODE -ne 0) { throw 'Tuning output is stale.' }
    $out = Join-Path $repo 'outputs/rose-blue-tests'
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $sources = @(Get-ChildItem -LiteralPath 'unity/Assets/Chapter2/RoseBlue' -Filter '*.cs' | ForEach-Object { $_.FullName })
    & $mono $compiler /nologo /warnaserror+ /target:exe "/out:$out/RoseBlueTests.exe" @sources tools/combat/rose-blue-tests/Program.cs
    if ($LASTEXITCODE -ne 0) { throw 'Rose/Blue compilation failed.' }
    & $mono (Join-Path $out 'RoseBlueTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Rose/Blue regression tests failed.' }
} finally { Pop-Location }
