$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Compile engine-independent production input/arbitration and their NUnit tests.
# Commands/read-model contribute their actual input-only declarations; AI/read
# snapshots are outside this portable compilation boundary.
$paths = @(
 'Combat/Contracts/CombatInput.cs', 'Combat/Contracts/CombatInputIngress.cs',
 'Combat/Contracts/IPlayerActionHost.cs', 'Combat/Runtime/Application/CombatInputBuffer.cs',
 'Combat/Runtime/Application/CombatInputIngress.cs', 'Combat/Runtime/Application/PlayerActionArbiter.cs',
 'CombatLab/Contracts/CombatInputRecording.cs', 'Combat/Tests/Editor/PlayerActionArbiterTests.cs',
 'Combat/Tests/Editor/CombatInputIngressTests.cs'
)
$sources = foreach ($path in $paths) { Get-Content -Raw (Join-Path $root "unity/Assets/Deviant/$path") }
$commands = Get-Content -Raw (Join-Path $root 'unity/Assets/Deviant/Combat/Contracts/CombatCommands.cs')
$sources += $commands.Substring(0, $commands.IndexOf('    public readonly struct CombatAICommand')) + '}'
$handles = Get-Content -Raw (Join-Path $root 'unity/Assets/Deviant/Combat/Contracts/CombatReadModel.cs')
$sources += $handles.Substring(0, $handles.IndexOf('    public enum CombatActionReadPhase')) + '}'
$usings = 'using System; using System.Collections.Generic; using System.Linq; using System.IO; using System.Security.Cryptography; using Deviant.Combat.Contracts; using Deviant.Combat.Execution; using NUnit.Framework;'
$source = $usings + (($sources | ForEach-Object { $_ -replace '(?m)^using [^;]+;\s*', '' }) -join "`n")
$nunit = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom/nunit.framework.dll'
[Reflection.Assembly]::LoadFrom($nunit) | Out-Null
Add-Type -TypeDefinition $source -ReferencedAssemblies (@(Get-ChildItem "$PSHOME/ref" -Filter *.dll | Select-Object -ExpandProperty FullName) + @($nunit))
$count = 0
foreach ($fixtureType in @([Deviant.Combat.Tests.PlayerActionArbiterTests], [Deviant.Combat.Tests.CombatInputIngressTests])) {
    $fixture = [Activator]::CreateInstance($fixtureType)
    foreach ($method in $fixtureType.GetMethods()) {
        $cases = @($method.GetCustomAttributes([NUnit.Framework.TestCaseAttribute], $false))
        if ($cases.Count -gt 0) {
            foreach ($case in $cases) { $method.Invoke($fixture, $case.Arguments) | Out-Null; $count++ }
        } elseif ($method.IsDefined([NUnit.Framework.TestAttribute], $false)) {
            $method.Invoke($fixture, @()) | Out-Null; $count++
        }
    }
}
Write-Output "PASS: $count portable NUnit input/arbiter cases"
# The four targeted edges must survive tick coalescing independently.
$buffer = [Deviant.Combat.Execution.CombatInputBuffer]::new()
$buffer.Submit([Deviant.Combat.Contracts.CombatInput]::new(0, [Enum]::ToObject([Deviant.Combat.Contracts.CombatButtons],64)))
$buffer.Submit([Deviant.Combat.Contracts.CombatInput]::new(0, [Enum]::ToObject([Deviant.Combat.Contracts.CombatButtons],128)))
if ([int]$buffer.Current.Pressed -ne 192) { throw 'Targeted basic edges were lost or aliased.' }
Write-Output 'PASS: targeted basic edges survive coalescing'

$first = [Deviant.Combat.Contracts.CombatInput]::new(1, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special, $false, 0, 1, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special)
$second = [Deviant.Combat.Contracts.CombatInput]::new(-1, [Deviant.Combat.Contracts.CombatButtons]::Weapon2Special, $false, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special, -1, [Deviant.Combat.Contracts.CombatButtons]::Weapon2Special)
$buffer.Submit($first); $buffer.Submit($second); $buffer.ClearPressed()
if ($buffer.Current.IsSpecialHeld(0) -or -not $buffer.Current.IsSpecialHeld(1)) { throw 'Slot releases contaminated another charge hold.' }
if ($buffer.Current.ClimbAxis -ne -1 -or $buffer.Current.Pressed -ne 0 -or $buffer.Current.Released -ne 0) { throw 'Tick consumption lost continuous state or retained edges.' }
$one = [Deviant.CombatLab.Contracts.CombatInputRecording]::new('targeted', .02, @($first))
$changed = [Deviant.Combat.Contracts.CombatInput]::new(1, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special, $false, 0, 1, [Deviant.Combat.Contracts.CombatButtons]::Weapon2Special)
$two = [Deviant.CombatLab.Contracts.CombatInputRecording]::new('targeted', .02, @($changed))
if ($one.ContentHash -eq $two.ContentHash) { throw 'Recording hash ignored the charge-owning slot.' }
$climb = [Deviant.Combat.Contracts.CombatInput]::new(1, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special, $false, 0, -1, [Deviant.Combat.Contracts.CombatButtons]::Weapon1Special)
$three = [Deviant.CombatLab.Contracts.CombatInputRecording]::new('targeted', .02, @($climb))
if ($one.ContentHash -eq $three.ContentHash) { throw 'Recording hash ignored climb direction.' }
Write-Output 'PASS: independent slot holds, edge consumption, recording identity'
