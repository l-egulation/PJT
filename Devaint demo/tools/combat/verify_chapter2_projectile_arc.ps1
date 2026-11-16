param([string]$UnityEditor)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$version=(Select-String -LiteralPath (Join-Path $repo 'unity/ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()
if(-not $UnityEditor){$UnityEditor="C:/Program Files/Unity/Hub/Editor/$version/Editor/Unity.exe"}
$core=Join-Path (Split-Path -Parent $UnityEditor) 'Data/Managed/UnityEngine/UnityEngine.CoreModule.dll'
if(-not (Test-Path -LiteralPath $core)){throw "Unity $version CoreModule not found: $core"}
# Compile the actual production arc against the pinned Unity vectors/math.
# This is a numeric check, not a Unity scene or collision simulation.
[void][Reflection.Assembly]::LoadFrom($core)
$source=Get-Content -LiteralPath (Join-Path $repo 'unity/Assets/Chapter2/Runtime/Chapter2NormalOrdnance.cs') -Raw
$begin=$source.IndexOf('    public struct C2Arc')
$end=$source.IndexOf('    // Arena-owned')
if($begin -lt 0 -or $end -le $begin){throw 'C2Arc source boundary not found'}
$definition='using System; using UnityEngine; namespace Deviant.Chapter2 {'+$source.Substring($begin,$end-$begin)+'}'
Add-Type -TypeDefinition $definition -ReferencedAssemblies @((Join-Path $PSHOME 'ref/netstandard.dll'),(Join-Path $PSHOME 'ref/System.Runtime.dll'),$core)
$script:checks=0
function Check([bool]$ok,[string]$message){if(-not $ok){throw "FAIL: $message"};$script:checks++}
function Near([float]$a,[float]$b){[Math]::Abs($a-$b) -lt .0002}
$catalog=Get-Content -LiteralPath (Join-Path $repo 'unity/Assets/Chapter2/Resources/Chapter2ApprovedNormals.json') -Raw | ConvertFrom-Json
foreach($config in $catalog.monsters | Where-Object { $_.id -in @('R-selected','H3') }){
    Check (Near $config.minimumFlightSeconds .9) ($config.id+' catalog minimum')
    $start=[UnityEngine.Vector2]::new($config.launchX,$config.launchY)
    foreach($targetX in @(0,2,3.4,4,8,11,20)){
        foreach($targetY in @(-3,0,2)){
            $target=[UnityEngine.Vector2]::new($targetX,$targetY)
            $arc=[Deviant.Chapter2.C2Arc]::new()
            Check ([Deviant.Chapter2.C2Arc]::TryCreate($start,$target,$config.projectileSpeed,$config.apex,[ref]$arc,$config.minimumFlightSeconds)) 'valid target'
            Check ($arc.duration -ge .9-.00001) 'minimum time'
            Check ([Math]::Abs($arc.horizontal) -le $config.projectileSpeed+.00001) 'maximum horizontal speed'
            Check (Near ($arc.Sample($arc.duration).y) $targetY) 'target height'
            Check (Near ($arc.Sample($arc.vertical/$arc.gravity).y) ($config.launchY+$config.apex)) 'peak rise'
            if([Math]::Abs($targetX-$config.launchX) -ge 1/32){
                Check (Near ($arc.Sample($arc.duration).x) $targetX) 'target horizontal position'
                $originalTime=[Math]::Abs($targetX-$config.launchX)/$config.projectileSpeed
                Check (Near $arc.duration ([Math]::Max($originalTime,.9))) 'clamped flight duration'
                if($originalTime -ge .9){Check (Near ([Math]::Abs($arc.horizontal)) $config.projectileSpeed) 'unchanged far speed'}
            }else{Check (Near $arc.horizontal 0) 'vertical toss'}
        }
    }
}
$arc=[Deviant.Chapter2.C2Arc]::new()
$zero=[UnityEngine.Vector2]::new(0,0)
Check ([Deviant.Chapter2.C2Arc]::TryCreate($zero,$zero,6,.1,[ref]$arc,.9)) 'short vertical toss'
Check (Near $arc.duration .9) 'vertical minimum'
Check (Near ($arc.Sample($arc.duration).y) 0) 'vertical target'
foreach($bad in @(-.1,[float]::NaN,[float]::PositiveInfinity)){
    Check (-not [Deviant.Chapter2.C2Arc]::TryCreate($zero,[UnityEngine.Vector2]::new(2,0),6,1.5,[ref]$arc,$bad)) 'invalid minimum rejected'
}
Check (-not [Deviant.Chapter2.C2Arc]::TryCreate($zero,[UnityEngine.Vector2]::new(2,3),6,1.5,[ref]$arc,.9)) 'unreachable height rejected'
Check ([Deviant.Chapter2.C2Arc]::TryCreate($zero,[UnityEngine.Vector2]::new(.6,0),6,1.5,[ref]$arc)) 'legacy optional minimum'
Check (Near $arc.duration .1) 'legacy call unchanged'
Write-Output "PASS: $checks actual C2Arc numeric checks against Unity $version; scene/collision tests not run."
