param([string]$EditorPath, [switch]$Check)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$branch = (& git -C $repo branch --show-current).Trim()
if ($branch -ne 'test/combat-feel') { throw "Expected test/combat-feel, found $branch. No branch was changed." }
$project = Join-Path $repo 'unity'
if (-not $EditorPath) {
    $workspace = (Get-Item -LiteralPath $repo).Parent.Parent.Parent.FullName
    $EditorPath = Join-Path $workspace '90_도구\Unity\6000.6.5f1\Editor\Unity.exe'
    if (-not (Test-Path -LiteralPath $EditorPath)) {
        $EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe'
    }
}
if (-not (Test-Path -LiteralPath $EditorPath)) { throw 'Unity 6000.6.5f1 is required. Pass -EditorPath.' }
$version = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Raw
if ($version -notmatch 'm_EditorVersion: 6000\.6\.5f1(?:\r?\n|$)') { throw 'Unexpected project version.' }
$output = Join-Path $repo 'outputs\combat-feel'
if ($Check) {
    [pscustomobject]@{ Branch=$branch; Project=$project; Editor=$EditorPath; Toggle='F6'; Strength='F7' }
    exit 0
}
New-Item -ItemType Directory -Path $output -Force > $null
$log = Join-Path $output 'interactive.log'
Start-Process -FilePath $EditorPath -WindowStyle Normal -ArgumentList @(
    '-projectPath', ('"'+$project+'"'),
    '-executeMethod', 'Deviant.Combat.Editor.CombatFeelPreview.Open',
    '-logFile', ('"'+$log+'"')
)
