param([ValidateRange(1,5)][int]$Ink = 1, [switch]$Check, [switch]$Discovery, [switch]$Review, [switch]$Storybook)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $repo '../../..')).Path
$editor = Join-Path $workspace '90_도구/Unity/6000.6.5f1/Editor/Unity.exe'
$project = Join-Path $repo 'unity'
$methods = @('One','Two','Three','Four','Five')
$method = if ($Storybook) { 'Deviant.Game.Editor.VillageInkPreviewEditor.OpenRecordReview' } elseif ($Review) { 'Deviant.Game.Editor.VillageInkPreviewEditor.OpenReview' } elseif ($Discovery) { 'Deviant.Game.Editor.VillageInkPreviewEditor.PlayBossDiscovery' } else { 'Deviant.Game.Editor.VillageInkPreviewEditor.Play' + $methods[$Ink - 1] }
if (-not (Test-Path -LiteralPath $editor -PathType Leaf)) { throw "Unity editor missing: $editor" }
if ((Get-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw) -notmatch '6000\.6\.5f1') { throw 'Unity version mismatch.' }
if ($Check) { [pscustomobject]@{Project=$project;Editor=$editor;Ink=$Ink;Method=$method}; exit 0 }
$logs = Join-Path $repo 'outputs/village-ink'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
Start-Process -FilePath $editor -WindowStyle Normal -ArgumentList @('-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-logFile',('"'+(Join-Path $logs 'preview.log')+'"'))
