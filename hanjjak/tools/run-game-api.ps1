# 로컬 game-api 실행 스크립트.
#
# 이 앱은 환경변수 없이 띄우면 두 가지가 조용히 어긋난다.
#   - DB_URL 기본값이 `hanjjak`이라 개발용 DB(`hanjjak_ui`)가 아닌 빈 DB에 붙는다.
#   - 치장 뽑기 HMAC 키가 비어 있으면 뽑기 API가 GACHA_CONTENT_UNAVAILABLE(503)을 내고
#     화면에 뽑기 보드가 통째로 사라진다.
#
# 값은 저장소에 커밋하지 않는 `.env.local`(.gitignore의 `.env.*`)에서 읽고,
# 뽑기 시크릿이 없으면 32바이트를 새로 만들어 적어 둔다.
#
#   pwsh tools/run-game-api.ps1
#   pwsh tools/run-game-api.ps1 -Rebuild      # 실행 전 jar 다시 빌드

[CmdletBinding()]
param(
    [switch]$Rebuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot ".env.local"
$jar = Join-Path $repoRoot "apps/game-api/build/libs/game-api.jar"

# .env.local 읽기 (KEY=VALUE, # 주석 허용)
$settings = [ordered]@{}
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile -Encoding UTF8) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) { continue }
        $split = $trimmed.IndexOf("=")
        if ($split -lt 1) { continue }
        $settings[$trimmed.Substring(0, $split).Trim()] = $trimmed.Substring($split + 1).Trim()
    }
}

# 기본값 채우기
if (-not $settings["DB_URL"]) { $settings["DB_URL"] = "jdbc:postgresql://localhost:5432/hanjjak_ui" }
if (-not $settings["COSMETIC_GACHA_HMAC_KEY_ID"]) { $settings["COSMETIC_GACHA_HMAC_KEY_ID"] = "local-dev" }
if (-not $settings["COSMETIC_GACHA_HMAC_SECRET"]) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $settings["COSMETIC_GACHA_HMAC_SECRET"] = [Convert]::ToBase64String($bytes)
    Write-Host "새 치장 뽑기 시크릿을 만들어 .env.local에 저장합니다." -ForegroundColor Yellow
}

# 되돌려 쓰기 (다음 실행에도 같은 시크릿을 쓴다)
$lines = @("# 로컬 실행 설정. 커밋하지 않는다. tools/run-game-api.ps1 이 읽고 쓴다.")
foreach ($key in $settings.Keys) { $lines += "$key=$($settings[$key])" }
[System.IO.File]::WriteAllLines($envFile, $lines)

if ($Rebuild -or -not (Test-Path $jar)) {
    Write-Host "game-api jar 빌드 중..." -ForegroundColor Cyan
    & (Join-Path $repoRoot "gradlew.bat") ":apps:game-api:bootJar"
    if ($LASTEXITCODE -ne 0) { throw "빌드 실패" }
}
if (-not (Test-Path $jar)) { throw "jar을 찾을 수 없습니다: $jar (-Rebuild 로 빌드하세요)" }

foreach ($key in $settings.Keys) { Set-Item -Path "Env:$key" -Value $settings[$key] }

Write-Host ("DB_URL = {0}" -f $settings["DB_URL"]) -ForegroundColor Green
Write-Host ("치장 뽑기 HMAC = {0} (키 ID: {1})" -f "설정됨", $settings["COSMETIC_GACHA_HMAC_KEY_ID"]) -ForegroundColor Green

Set-Location $repoRoot
& java -jar $jar
