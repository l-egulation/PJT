$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectPath = Join-Path $repoPath 'unity'
$version = ((Get-Content -LiteralPath (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') | Select-String '^m_EditorVersion: ').Line -replace '^m_EditorVersion: ', '').Trim()
if ($version -ne '6000.6.5f1') { throw "Expected Unity 6000.6.5f1, found $version" }
$editor = Join-Path 'C:\Program Files\Unity\Hub\Editor' "$version\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $editor)) { throw "Unity editor not installed: $editor" }
if (Test-Path -LiteralPath (Join-Path $projectPath 'Temp/UnityLockfile')) {
    Write-Host 'The local MAIN Unity project is already open. Use that Editor and press Play.'
    exit 0
}
Write-Host 'Opening local Deviant-main with selected basic-attack audio. This does not pull or publish Git changes.'
Start-Process -FilePath $editor -ArgumentList @('-projectPath', ('"' + $projectPath + '"'), '-executeMethod', 'Deviant.Game.Editor.GameBuilder.Open') -WindowStyle Normal
