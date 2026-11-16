$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Compile the engine-independent movement-assist owners and their NUnit tests.
# Wall coyote and keyboard edges need Unity scenes and are verified there.
$paths = @(
 'Definitions/Runtime/Combat/LocomotionProfile.cs', 'Combat/Contracts/PlayerJump.cs', 'Combat/Contracts/ActorMotion.cs',
 'Combat/Runtime/Application/PlayerJumpExecution.cs', 'Combat/Runtime/Application/HorizontalRunControl.cs',
 'Combat/Runtime/Application/ActorMotionResolver.cs',
 'Combat/Tests/Editor/PlayerJumpTests.cs', 'Combat/Tests/Editor/HorizontalRunControlTests.cs'
)
$sources = foreach ($path in $paths) { Get-Content -Raw (Join-Path $root "unity/Assets/Deviant/$path") }
$usings = 'using System; using System.Globalization; using System.Security.Cryptography; using System.Text; using Deviant.Combat.Contracts; using Deviant.Combat.Execution; using Deviant.Definitions; using NUnit.Framework;'
$source = $usings + (($sources | ForEach-Object { $_ -replace '(?m)^using [^;]+;\s*', '' }) -join "`n")
$nunit = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll'
[Reflection.Assembly]::LoadFrom($nunit) | Out-Null
Add-Type -TypeDefinition $source -ReferencedAssemblies (@(Get-ChildItem "$PSHOME/ref" -Filter *.dll | Select-Object -ExpandProperty FullName) + @($nunit))
$count = 0
foreach ($fixtureType in @([Deviant.Combat.Tests.PlayerJumpTests], [Deviant.Combat.Tests.HorizontalRunControlTests])) {
    $fixture = [Activator]::CreateInstance($fixtureType)
    foreach ($method in $fixtureType.GetMethods()) {
        if ($method.IsDefined([NUnit.Framework.TestAttribute], $false)) { $method.Invoke($fixture, @()) | Out-Null; $count++ }
    }
}
Write-Output "PASS: $count portable NUnit jump/run-control cases"

$data = Get-Content -Raw (Join-Path $root 'data/combat/locomotion/player.json') | ConvertFrom-Json
$dto = [Deviant.Definitions.LocomotionData]::new()
foreach ($property in $data.PSObject.Properties) { $dto.($property.Name) = $property.Value }
$profile = $null; $reason = $null
if (-not [Deviant.Definitions.LocomotionProfile]::TryCreate($dto, [ref]$profile, [ref]$reason)) { throw $reason }
$actual = @($profile.JumpBufferDuration, $profile.WallCoyoteDuration, $profile.JumpReleaseVelocityScale,
 $profile.GroundAccelerationSeconds, $profile.GroundDecelerationSeconds, $profile.AirAccelerationSeconds, $profile.AirDecelerationSeconds)
$expected = @(.12, .1, 1, .05, .03, .1, .08)
for ($i = 0; $i -lt $expected.Count; $i++) { if ([Math]::Abs($actual[$i] - $expected[$i]) -gt .000001) { throw "Canonical movement assist value $i is $($actual[$i]), expected $($expected[$i])." } }
$export = Get-Content -Raw (Join-Path $root 'unity/Assets/Deviant/Combat/Resources/PlayerLocomotion.json')
if (($export -replace "`r", '') -ne ((Get-Content -Raw (Join-Path $root 'data/combat/locomotion/player.json')) -replace "`r", '')) { throw 'PlayerLocomotion.json differs from data/combat/locomotion/player.json.' }
$legacy = $profile.ToData(); $legacy.jumpAssist = $null; $legacy.horizontalControl = $null
$original = $null
if (-not [Deviant.Definitions.LocomotionProfile]::TryCreate($legacy, [ref]$original, [ref]$reason)) { throw $reason }
if ([Math]::Abs($original.JumpBufferDuration - .08) -gt .000001 -or $original.JumpReleaseVelocityScale -ne 1 -or $original.GroundAccelerationSeconds -ne 0) { throw 'Profiles without assist blocks changed behaviour.' }
Write-Output 'PASS: canonical assist values, export copy, legacy fallback'

# Releasing the jump button at any point keeps the full rise and apex hang.
# Use the real hold-edge owner and canonical release scale for both jumps.
function Measure-Rise([float]$launch, [float]$dt, [float]$releaseAfter) {
    $y = $launch; $height = 0; $peak = 0; $hang = 0
    $jump = [Deviant.Combat.Execution.PlayerJumpExecution]::new()
    $null = $jump.ObserveHold($true, $false)
    if ($releaseAfter -lt 0) { $null = $jump.ObserveHold($false, $true) }
    $jump.ArmCut($true)
    $released = $releaseAfter -lt 0
    $apex = [Deviant.Combat.Contracts.ActorJumpApexState]::new($true, 0)
    for ($tick = 0; $tick -lt 200 -and ($y -gt 0 -or $apex.Armed -or $apex.Remaining -gt 0 -or $height -ge $peak); $tick++) {
        $releaseNow = -not $released -and $tick * $dt -ge $releaseAfter
        if ($jump.ObserveHold($false, $releaseNow) -and $apex.Armed -and $y -gt 0) { $y *= $profile.JumpReleaseVelocityScale }
        if ($releaseNow) { $released = $true }
        $input = [Deviant.Combat.Contracts.ActorMotionInput]::new(1, $dt, 0, $y, $false, $false, $false, $false, $false, 0, 0, 0, 0, 0, 0, $profile.RiseGravity, $profile.FallGravity, $null, $apex, $profile.JumpApexSeconds)
        $motion = [Deviant.Combat.Execution.ActorMotionResolver]::Resolve($input)
        if ($motion.VelocityY -eq 0 -and $motion.DeltaY -eq 0) { $hang += $dt }
        $height += $motion.DeltaY; $y = $motion.VelocityY; $apex = $motion.Apex
        if ($height -gt $peak) { $peak = $height }
    }
    return @($peak, $hang)
}
foreach ($dt in @((1.0 / 120), .01, .02)) {
    foreach ($launch in @($profile.FirstJumpVelocity, $profile.SecondJumpVelocity)) {
        $full = Measure-Rise $launch $dt ([float]::PositiveInfinity)
        if ([Math]::Abs($full[0] - 3) -gt .0005) { throw "Held jump peak changed: $($full[0])" }
        foreach ($releaseAfter in @(-1, 0, .05, .15, .29)) {
            $released = Measure-Rise $launch $dt $releaseAfter
            if ([Math]::Abs($released[0] - 3) -gt .0005) { throw "Released jump peak is $($released[0]), expected 3 (dt $dt, release $releaseAfter)" }
            if ([Math]::Abs($released[1] - $full[1]) -gt .000001 -or $released[1] -lt .04 - .000001) { throw "Released jump changed its apex hang at dt $dt" }
        }
    }
}
Write-Output 'PASS: first/second jumps keep 3u and apex hang when held, tapped, released during ascent or released before a buffered landing'
