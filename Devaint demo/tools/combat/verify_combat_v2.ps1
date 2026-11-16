param([string]$UnityEditor, [string]$UnityProject, [string]$LogFile, [switch]$RulesOnly)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime=Join-Path $repoRoot 'unity/Assets/Deviant/Combat/Runtime'
$equipmentRoot=Join-Path $repoRoot 'unity/Assets/Deviant/Equipment'
# Portable rules compile the real owner with its compatibility adapter. Unity
# separately verifies the Equipment.Contracts / Equipment / Combat asmdef split.
Add-Type -Path @(
    (Join-Path $equipmentRoot 'Contracts/WeaponCharges.cs'),
    (Join-Path $equipmentRoot 'Contracts/WeaponStats.cs'),
    (Join-Path $equipmentRoot 'Contracts/WeaponInstance.cs'),
    (Join-Path $equipmentRoot 'Contracts/WeaponLoadout.cs'),
    (Join-Path $equipmentRoot 'Contracts/EquipmentCommands.cs'),
    (Join-Path $equipmentRoot 'Contracts/InkPalette.cs'),
    (Join-Path $equipmentRoot 'Runtime/WeaponResources.cs'),
    (Join-Path $equipmentRoot 'Runtime/WeaponStatSlots.cs'),
    (Join-Path $equipmentRoot 'Runtime/WeaponInstance.cs'),
    (Join-Path $equipmentRoot 'Runtime/WeaponLoadout.cs'),
    (Join-Path $equipmentRoot 'Runtime/EquipmentCommands.cs'),
    (Join-Path $equipmentRoot 'Runtime/InkPalette.cs'),
    (Join-Path $repoRoot 'unity/Assets/Deviant/Combat/Contracts/ActorSurfaceQuery.cs'),
    (Join-Path $repoRoot 'unity/Assets/Deviant/Combat/Contracts/ActorPose.cs'),
    (Join-Path $repoRoot 'unity/Assets/Deviant/Combat/Contracts/ActorRecovery.cs'),
    (Join-Path $runtime 'Application/SafeGroundHistory.cs'),
    (Join-Path $repoRoot 'unity/Assets/Deviant/Combat/Contracts/PlayerJump.cs'),
    (Join-Path $repoRoot 'unity/Assets/Deviant/Definitions/Runtime/Combat/LocomotionProfile.cs'),
    (Join-Path $runtime 'Application/PlayerJumpExecution.cs'),
    (Join-Path $runtime 'CombatCore.cs')
)
$count=0
function Assert-Combat([bool]$condition,[string]$message) {
    if(-not $condition){throw "FAIL: $message"}
    $script:count++
}
function Near([double]$a,[double]$b){ return [Math]::Abs($a-$b) -lt 0.0001 }
Assert-Combat (Near ([Deviant.Combat.CombatRules]::Cooldown(10,1)) 2) '80 percent CDR cap'
Assert-Combat (Near ([Deviant.Combat.CombatRules]::Damage(25,.2)) 30) 'float special damage'
Assert-Combat ([Deviant.Combat.CombatRules]::IntersectsTime(.1,.2,.15,.01)) 'sub-step active interval is detected'
Assert-Combat (-not [Deviant.Combat.CombatRules]::IntersectsTime([single]([single]0.15 + [single]0.01),.2,.15,.01)) 'end-exclusive active interval'
$ledger=[Deviant.Combat.HitLedger]::new()
Assert-Combat ($ledger.First(0,42)) 'first target hit'
Assert-Combat (-not $ledger.First(0,42)) 'duplicate collider rejected'
Assert-Combat ($ledger.First(1,42)) 'next hit index can damage same target'
$pool=[Deviant.Combat.ChargePool]::new(3,5)
Assert-Combat ($pool.Spend(0)) 'charge 1'
Assert-Combat ($pool.Spend(.4)) 'charge 2'
Assert-Combat ($pool.Spend(.8)) 'charge 3'
Assert-Combat (-not $pool.Spend(1)) 'empty pool rejects'
$pool.Tick(5)
Assert-Combat ($pool.Available -eq 1) 'first recovery not restarted by later use'
$pool.Tick(15)
Assert-Combat ($pool.Available -eq 3) 'sequential full recovery'
$pool=[Deviant.Combat.ChargePool]::new(1,10)
$null=$pool.Spend(0)
$pool.Reconfigure(3,5,5)
Assert-Combat ($pool.Available -eq 1) 'normalized 1-to-3 charge debt'
Assert-Combat (Near $pool.NextReady 7.5) 'fractional recharge preserved'
$pool.Reconfigure(1,6,5)
Assert-Combat (Near $pool.NextReady 8) 'roundtrip debt and new cooldown'
$movementData=Get-Content -Raw (Join-Path $repoRoot 'data/combat/locomotion/player.json') | ConvertFrom-Json
$movementDto=[Deviant.Definitions.LocomotionData]::new()
foreach($property in $movementData.PSObject.Properties) { $movementDto.($property.Name)=$property.Value }
$movement=$null; $movementError=$null
if(-not [Deviant.Definitions.LocomotionProfile]::TryCreate($movementDto,[ref]$movement,[ref]$movementError)) { throw $movementError }
$jump=[Deviant.Combat.Execution.PlayerJumpExecution]::new()
$vy=[single]0
Assert-Combat ($jump.Press(0,$movement,[ref]$vy)) 'walked-off first jump available'
Assert-Combat (Near $vy $movement.FirstJumpVelocity) 'first jump velocity follows the canonical profile'
Assert-Combat ($jump.Press(.1,$movement,[ref]$vy)) 'second jump available'
Assert-Combat (Near $vy $movement.SecondJumpVelocity) 'second jump velocity follows the canonical profile'
Assert-Combat (-not $jump.Press(.2,$movement,[ref]$vy)) 'third jump not immediate'
Assert-Combat ($jump.Land(.26,$movement,[ref]$vy)) '0.06 second buffered landing jump'
$jump.Reset(); $null=$jump.Press(0,$movement,[ref]$vy); $null=$jump.Press(0,$movement,[ref]$vy); $null=$jump.Press(.2,$movement,[ref]$vy)
Assert-Combat ($jump.Land(.32,$movement,[ref]$vy)) '0.12 second landing reservation is inclusive'
$jump.Reset(); $null=$jump.Press(0,$movement,[ref]$vy); $null=$jump.Press(0,$movement,[ref]$vy); $null=$jump.Press(.2,$movement,[ref]$vy)
Assert-Combat (-not $jump.Land(.33,$movement,[ref]$vy)) '0.13 second landing input expires'
Assert-Combat (Near ([Deviant.Combat.CombatRules]::RefreshStatus(3,0,2)) 3) 'short status does not shorten existing duration'
$history=[Deviant.Combat.Execution.SafeGroundHistory]::new()
$history.Record(1,2,0,1); $history.Record(2,4,0,1); $history.Record(5,8,0,-1)
$sample=[Deviant.Combat.Contracts.SafeGroundSample]::new(0,0,0,1)
Assert-Combat ($history.TryBefore(2,[ref]$sample)) 'floor at exact fall-minus-three cutoff exists'
Assert-Combat (Near $sample.X 4) 'exact cutoff floor selected'
Assert-Combat ($history.TryBefore(4,[ref]$sample)) 'airborne cutoff uses earlier floor'
Assert-Combat (Near $sample.Time 2) 'future floor is never selected'
Assert-Combat (-not $history.TryBefore(.5,[ref]$sample)) 'no invented pre-entry history'
Write-Output "PASS: $count portable combat rule checks"
if($RulesOnly){
    Write-Output 'SKIP: Unity Editor/test and Linux/Windows Player script compilation (RulesOnly)'
    return
}

# Use the real, pinned editor/package compiler instead of an obsolete Player ZIP.
# Explicitly require an isolated, restored project so incomplete Assets cannot change main metadata.
if(-not $UnityEditor){$UnityEditor=$env:UNITY_EDITOR}
if(-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)){
    throw 'Pass -UnityEditor (or UNITY_EDITOR) pointing to Unity 6000.6.5f1. Use -RulesOnly only for portable rules.'
}
if(-not $UnityProject){throw 'Pass -UnityProject pointing to a restored isolated copy of unity/ with the original packages.'}
$UnityProject=[IO.Path]::GetFullPath($UnityProject)
$sourceProject=Join-Path $repoRoot 'unity'
if($UnityProject.TrimEnd([IO.Path]::DirectorySeparatorChar) -eq $sourceProject.TrimEnd([IO.Path]::DirectorySeparatorChar)){
    throw 'Preserve the original checkout; -UnityProject must be an isolated copy, not main unity/.'
}
foreach($relative in @('ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json')){
    $expected=Join-Path $sourceProject $relative; $actual=Join-Path $UnityProject $relative
    if(-not (Test-Path -LiteralPath $actual -PathType Leaf) -or
       (Get-FileHash -LiteralPath $expected -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash){
        throw "Isolated project version/packages differ from the original: $relative"
    }
}
if((Get-Content -Raw (Join-Path $UnityProject 'ProjectSettings/ProjectVersion.txt')) -notmatch '(?m)^m_EditorVersion: 6000\.6\.5f1\r?$'){
    throw 'The original project must stay on Unity 6000.6.5f1.'
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $sourceProject 'Assets') -Recurse -File |
        Where-Object {$_.Extension -in @('.cs','.asmdef')}){
    $relative=[IO.Path]::GetRelativePath($sourceProject,$file.FullName)
    $actual=Join-Path $UnityProject $relative
    if(-not (Test-Path -LiteralPath $actual -PathType Leaf) -or
       (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash){
        throw "Isolated C# source/assembly definition differs: $relative"
    }
}
$editorVersion=(& $UnityEditor -version 2>&1 | Out-String)
if($LASTEXITCODE -ne 0 -or $editorVersion.Trim() -ne '6000.6.5f1'){
    throw "Expected Unity 6000.6.5f1; editor reported: $editorVersion"
}
if(-not $LogFile){$LogFile=Join-Path $repoRoot ("outputs/ink_forest/CombatV2Verification/unity-"+[Guid]::NewGuid().ToString('N')+'.log')}
$LogFile=[IO.Path]::GetFullPath($LogFile)
if(Test-Path -LiteralPath $LogFile){throw "Preserve the existing validation log; choose a new -LogFile: $LogFile"}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($LogFile)) | Out-Null
$unityArgs=@('-batchmode','-nographics','-projectPath',$UnityProject,'-quit',
    '-executeMethod','Deviant.Presentation.Editor.HitFeedbackRuntimeCompile.CompileRuntimeScripts','-logFile',$LogFile)
& $UnityEditor @unityArgs
if($LASTEXITCODE -ne 0){throw "Unity compilation failed (exit $LASTEXITCODE). See $LogFile"}
if(-not (Test-Path -LiteralPath $LogFile -PathType Leaf)){throw 'Unity produced no compiler log; validation is incomplete.'}
$log=Get-Content -Raw -LiteralPath $LogFile
foreach($target in @('StandaloneLinux64','StandaloneWindows64')){
    if($log -notmatch ("HIT_FEEDBACK_PLAYER_SCRIPTS_OK target="+$target+' unity=6000\.6\.5f1 assemblies=')){
        throw "Unity did not confirm $target runtime compilation. See $LogFile"
    }
}
Write-Output 'PASS: actual Unity Editor/test assemblies and Linux/Windows Player scripts (original asmdefs/packages).'
Write-Output "Compiler log: $LogFile"
Write-Output 'NOT RUN: scene/PlayMode regression, packaged Player build, manual play and listening.'
