param([ValidateSet('Plan','Execute')][string]$Mode='Plan')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Get-Location).Path)
$final=Join-Path $workspace '05_공유/무기11종_확정_20261021'
if (-not (Test-Path -LiteralPath (Join-Path $final '검증결과.json'))) { throw 'Run from the verified project workspace.' }
$verified=Get-Content -LiteralPath (Join-Path $final '검증결과.json') -Raw | ConvertFrom-Json
if ($verified.status -ne 'PASS' -or $verified.weapons -ne 11) { throw 'Final package must pass verification first.' }
$planFile=Join-Path $final '정리대상.json'
function Check-Target([string]$candidate) {
    $resolved=(Resolve-Path -LiteralPath $candidate).Path
    if (-not $resolved.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Outside workspace: $resolved" }
    if ($resolved -eq $final -or $resolved.StartsWith($final+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Final package is protected: $resolved" }
    $item=Get-Item -LiteralPath $resolved -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point: $resolved" }
    if ($item.PSIsContainer) {
        $unsafe=Get-ChildItem -LiteralPath $resolved -Force -Recurse | Where-Object { $_.Name -eq '.git' -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }
        if ($unsafe) { throw "Nested repository or reparse point: $resolved" }
    }
    return $resolved
}
if ($Mode -eq 'Plan') {
    $share=Join-Path $workspace '05_공유'
    $targets=@(Get-ChildItem -LiteralPath $share -Directory | Where-Object { $_.Name -match '^(기본포_A_|네무기_|원거리3종_|원거리4종_|무기7종_확정_20261019$|무기11종_(대기통일|전체모션))' } | Select-Object -ExpandProperty FullName)
    $targets+=@(Get-ChildItem -LiteralPath $share -File | Where-Object { $_.Extension -eq '.zip' -and ($_.Name -match '^(기본포_A_|원거리3종_|원거리4종_)' -or $_.Name -eq '망치_전체모션_공격3안_검토_20261016.zip') } | Select-Object -ExpandProperty FullName)
    $relative=@(
        '99_보관/무기제작_확정전시안_20261019',
        '04_테스트/원거리 프레임 후보',
        '04_테스트/무기 모션/7종_클릭반응_연계보정_v7_20261019',
        'output/imagegen/ranged4-effects-20261021',
        'output/imagegen/ranged-effects-v25-20261021',
        'output/imagegen/ranged-effects-v26-20261021',
        'output/imagegen/ranged-effects-v27-20261021',
        'output/imagegen/ranged-effects-v28-20261021',
        'tmp/imagegen/fx-v25','tmp/imagegen/fx-v26','tmp/imagegen/fx-v27','tmp/imagegen/fx-v28',
        'tools/finalize_weapons_20261021.py','tools/weapon_ranged_runtime.js','tools/test_approved_ranged.js','tools/verify_weapon_package_20261021.py'
    )
    foreach ($rel in $relative) { $p=Join-Path $workspace $rel; if (Test-Path -LiteralPath $p) {$targets+=$p} }
    $entries=@(foreach ($target in $targets | Sort-Object -Unique) {
        $resolved=Check-Target $target
        $item=Get-Item -LiteralPath $resolved
        $files=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $resolved -Recurse -File -Force)}else{@($item)}
        [pscustomobject]@{path=$resolved;relative=$resolved.Substring($workspace.Length+1);directory=$item.PSIsContainer;files=$files.Count;bytes=($files | Measure-Object Length -Sum).Sum;reason='Superseded weapon review, rejected candidate, duplicate package, or completed generation temporary files'}
    })
    $plan=[pscustomobject]@{workspace=$workspace;final=$final;status='verified_plan';targets=$entries;files=($entries|Measure-Object files -Sum).Sum;bytes=($entries|Measure-Object bytes -Sum).Sum;preserved=@('02_에셋 canonical sources','03_개발 game repositories','unfinished shuriken and bomb inputs/reviews','Downloads originals','unrelated scenes/UI/assets')}
    $plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $planFile -Encoding utf8
    [pscustomobject]@{targets=$entries.Count;files=$plan.files;MiB=[math]::Round($plan.bytes/1MB,2);plan=$planFile} | ConvertTo-Json
    exit
}
$plan=Get-Content -LiteralPath $planFile -Raw | ConvertFrom-Json
if ($plan.workspace -ne $workspace -or $plan.final -ne $final) { throw 'Plan path mismatch.' }
# Recheck every absolute target before any recursive deletion.
foreach($entry in $plan.targets) { $checked=Check-Target $entry.path; if($checked -ne $entry.path){throw 'Target resolution changed.'} }
$removed=@();$failures=@()
$reportFile=Join-Path $final '정리기록.json'
if(Test-Path -LiteralPath $reportFile){$previous=Get-Content -LiteralPath $reportFile -Raw|ConvertFrom-Json;$removed+=@($previous.removed)}
foreach($entry in $plan.targets) {
    try {
        if ($entry.directory) { Remove-Item -LiteralPath $entry.path -Recurse -Force } else { Remove-Item -LiteralPath $entry.path -Force }
        if(Test-Path -LiteralPath $entry.path){throw 'Target still exists.'}
        $removed+=$entry
    } catch { $failures+=[pscustomobject]@{path=$entry.path;error=$_.Exception.Message} }
}
$report=[pscustomobject]@{status=if($failures.Count){'PARTIAL'}else{'COMPLETE'};date=(Get-Date).ToString('o');deletedTargets=$removed.Count;deletedFiles=($removed|Measure-Object files -Sum).Sum;deletedBytes=($removed|Measure-Object bytes -Sum).Sum;removed=$removed;failures=$failures;preserved=$plan.preserved}
$report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $final '정리기록.json') -Encoding utf8
[pscustomobject]@{status=$report.status;deletedTargets=$report.deletedTargets;deletedFiles=$report.deletedFiles;MiB=[math]::Round($report.deletedBytes/1MB,2);failures=$failures} | ConvertTo-Json -Depth 4
if($failures.Count){exit 1}
