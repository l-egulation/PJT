$ErrorActionPreference = 'Stop'
$env:PYTHONUTF8 = '1'
try {
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $lockPath = Join-Path $repo 'unity/Temp/UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try {
            $probe = [IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None')
            $probe.Dispose()
        } catch {
            throw '이 프로젝트가 Unity에서 열려 있습니다. Unity를 닫고 에셋 업데이트를 다시 실행하세요.'
        }
    }
    $python = $null
    $prefix = @()
    foreach ($name in @('py', 'python', 'python3')) {
        $candidate = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (!$candidate -or $candidate.Source -like '*\WindowsApps\*') { continue }
        $candidatePrefix = @()
        if ($name -eq 'py') { $candidatePrefix = @('-3') }
        $candidatePath = $candidate.Source
        try {
            & $candidatePath @candidatePrefix -c 'import sys; sys.exit(0 if sys.version_info >= (3, 9) else 1)' 2>$null
        } catch { continue }
        if ($LASTEXITCODE -eq 0) {
            $python = $candidatePath
            $prefix = $candidatePrefix
            break
        }
    }
    if (!$python) {
        throw 'Python 3.9 이상을 한 번 설치하세요: https://www.python.org/downloads/windows/ (Add Python to PATH 선택). pip 패키지는 필요 없습니다.'
    }
    & $python @prefix (Join-Path $PSScriptRoot 'update_assets.py') @args
    exit $LASTEXITCODE
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
