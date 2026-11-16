param(
 [Parameter(Mandatory=$true)][string]$UnityProject,
 [string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath $UnityProject).Path
$version=Join-Path $project 'ProjectSettings/ProjectVersion.txt'
if(-not (Test-Path -LiteralPath $version) -or (Get-Content -LiteralPath $version -Raw) -notmatch 'm_EditorVersion: 6000\.6\.5f1') {throw 'Use an isolated Unity 6000.6.5f1 project copy.'}
$package=Split-Path $PSScriptRoot -Parent
$environments=Split-Path $package -Parent
$editor=Join-Path $project 'Assets/ForestCombatV2/Editor'
$runtime=Join-Path $project 'Assets/ForestCombatV2/Runtime/ArtReview'
New-Item -ItemType Directory -Force $editor,$runtime | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stage1ApprovedReview.cs') -Destination $editor
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stage1PaperAsh.shader') -Destination $editor
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stage1Atmosphere.cs') -Destination $runtime
foreach($shader in @('NativeBackground.shader','VirtualWallOutlineSoil.shader')) {
 Copy-Item -LiteralPath (Join-Path $environments "stage1-pixel-background-native-20261111/source/$shader") -Destination $editor
}
$env:STAGE_BACKGROUND_CAPTURE_ROOT=Join-Path $environments 'stage1-pixel-lawn-wood-32'
$env:STAGE_NATIVE_BACKGROUND_ROOT=Join-Path $environments 'stage1-pixel-background-native-20261111'
$env:STAGE_WEATHER_ROOT=$package
$env:STAGE_LAWN_REVIEW_TARGETS='S1-01,S1-08,S1-10,S1-11'
$env:STAGE_TAUPE_REVIEW_ROOMS='S1-04,S1-07,S1-08'
$log=Join-Path (Split-Path $project -Parent) 'approved-stage1-capture.log'
Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-logFile',('"'+$log+'"'),'-executeMethod','Stage1ApprovedReview.Run') -WindowStyle Hidden -PassThru
