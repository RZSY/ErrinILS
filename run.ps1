param(
    [string]$Target = "app"
)

$root = $PSScriptRoot
if (-not $root) { $root = Get-Location }

if ($Target -eq "selftest") {
    Write-Host "== Building and Running Self-Tests ==" -ForegroundColor Cyan
    & "$root\ErrinILS-source\build.bat" selftest
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$root\bin\Debug\selftest.exe"
} else {
    Write-Host "== Building ErrinILS (Debug) ==" -ForegroundColor Cyan
    & "$root\ErrinILS-source\build.bat" debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "== Launching ErrinILS ==" -ForegroundColor Green
    Start-Process "$root\bin\Debug\ErrinILS.exe"
}
