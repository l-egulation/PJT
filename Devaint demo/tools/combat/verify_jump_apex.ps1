$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sources = @(
 (Join-Path $repo 'unity/Assets/Deviant/Combat/Contracts/ActorMotion.cs'),
 (Join-Path $repo 'unity/Assets/Deviant/Combat/Runtime/Application/ActorMotionResolver.cs'),
 (Join-Path $repo 'unity/Assets/Deviant/Definitions/Runtime/Combat/LocomotionProfile.cs')
)
$testSource = @'
using System;
using Deviant.Combat.Contracts;
using Deviant.Combat.Execution;
public static class JumpApexProof {
 static void Near(float actual, float expected, string message) {
  if (Math.Abs(actual - expected) > .00002f) throw new Exception(message + ": " + actual + " != " + expected);
 }
 static ActorMotionProposal Step(float y, float dt, ActorJumpApexState apex, bool freeze = false, bool forced = false, bool dead = false, bool ground = false, float? controlled = null) {
  return ActorMotionResolver.Resolve(new ActorMotionInput(1, dt, 7.5f, y, ground, freeze, forced, dead, false, 0, 0, 0, 0, 0, 0, 66.6666667f, 104.1666667f, controlled, apex, .06f));
 }
 public static void Run() {
  var apex = new ActorJumpApexState(true, 0);
  var crossing = Step(1, .02f, apex);
  Near(crossing.DeltaY, .0075f, "fractional rise reaches exact apex"); Near(crossing.VelocityY, 0, "apex freezes only remaining tick");
  Near(crossing.Apex.Remaining, .055f, "only five milliseconds consumed");
  var leaving = Step(0, .06f, crossing.Apex);
  Near(leaving.VelocityY, -104.1666667f * .005f, "fractional fall after pause");
  Near(leaving.DeltaY, -.5f * 104.1666667f * .005f * .005f, "fractional fall distance");
  var large = Step(20, .4f, apex);
  Near(large.DeltaY, 3 - .5f * 104.1666667f * .04f * .04f, "single tick spans rise, hold, fall");
  foreach (float dt in new[] { .01f, .02f, .017f, .05f }) {
   float y = 20, height = 0, total = 0; var state = apex;
   while (total < .48f - .000001f) {
    float step = Math.Min(dt, .48f - total); var motion = Step(y, step, state);
    height += motion.DeltaY; y = motion.VelocityY; state = motion.Apex; total += step;
   }
   Near(height, 2.25f, "trajectory independent of tick partition " + dt); Near(y, -12.5f, "fall velocity independent of tick partition " + dt);
  }
  Near(Step(0, .02f, default(ActorJumpApexState)).DeltaY, -.5f * 104.1666667f * .02f * .02f, "ledge or ceiling never arms hang");
  foreach (var cancelled in new[] { Step(0,.02f,crossing.Apex,true), Step(0,.02f,crossing.Apex,false,true), Step(0,.02f,crossing.Apex,false,false,true), Step(0,.02f,crossing.Apex,false,false,false,true), Step(0,.02f,crossing.Apex,controlled:2) }) {
   Near(cancelled.Apex.Remaining, 0, "interruption cancels timer"); if (cancelled.Apex.Armed) throw new Exception("interruption left jump armed");
  }
  Near(Step(0, .02f, crossing.Apex).DeltaX, .15f, "horizontal movement remains available");
  Near(Step(20, .4f, default(ActorJumpApexState)).DeltaY, 3 - .5f * 104.1666667f * .1f * .1f, "nonplayer trajectory unchanged");
 }
}
'@
$production = ($sources | ForEach-Object { (Get-Content -Raw $_) -replace '(?m)^using [^;]+;\r?\n', '' }) -join [Environment]::NewLine
Add-Type -TypeDefinition ("using System; using System.Globalization; using System.Security.Cryptography; using System.Text; using Deviant.Combat.Contracts; using Deviant.Combat.Execution;" + $production + ($testSource -replace '(?m)^using [^;]+;\r?\n', ''))
[JumpApexProof]::Run()
$data = Get-Content -Raw (Join-Path $repo 'data/combat/locomotion/player.json') | ConvertFrom-Json
$dto = [Deviant.Definitions.LocomotionData]::new()
foreach ($property in $data.PSObject.Properties) { $dto.($property.Name) = $property.Value }
$profile = $null; $reason = $null
if (-not [Deviant.Definitions.LocomotionProfile]::TryCreate($dto,[ref]$profile,[ref]$reason)) { throw $reason }
if ([Math]::Abs($profile.JumpApexSeconds - .06) -gt .000001) { throw 'Canonical apex duration differs from approved 0.06 seconds.' }
$copy = $profile.ToData(); $copy.jumpApexSeconds = .04
$changed = $null
if (-not [Deviant.Definitions.LocomotionProfile]::TryCreate($copy,[ref]$changed,[ref]$reason)) { throw $reason }
if ($changed.ContentHash -eq $profile.ContentHash -or [Math]::Abs($profile.ToData().jumpApexSeconds - .06) -gt .000001) { throw 'Apex duration must be immutable, exported and hashed.' }
foreach ($invalid in @(-.01,.061,[single]::NaN,[single]::PositiveInfinity)) {
 $copy.jumpApexSeconds = $invalid
 if ([Deviant.Definitions.LocomotionProfile]::TryCreate($copy,[ref]$changed,[ref]$reason)) { throw "Invalid apex duration accepted: $invalid" }
}
Write-Output 'PASS: exact player jump apex integration, tick partitions, interruption, ledge and unchanged default trajectory'
Write-Output 'PASS: canonical apex duration validation, immutable snapshot, export and content hash'
