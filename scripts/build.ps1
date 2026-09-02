<#
    BluePeak Operator — build, test and package.

    Usage:
        pwsh scripts/build.ps1                 # restore, build Release, run all tests
        pwsh scripts/build.ps1 -Package        # also produce both Windows packages
        pwsh scripts/build.ps1 -Capture        # also run the app and write design captures
#>
[CmdletBinding()]
param(
    [switch]$Package,
    [switch]$Capture,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
Push-Location $root

function Step($text) {
    Write-Host ''
    Write-Host "=== $text" -ForegroundColor Cyan
}

try {
    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

    Step 'Restore'
    dotnet restore BluePeakOperator.slnx
    if ($LASTEXITCODE -ne 0) { throw 'restore failed' }

    Step "Build ($Configuration)"
    dotnet build BluePeakOperator.slnx -c $Configuration --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'build failed' }

    Step 'Test — domain and simulation engine'
    dotnet test tests/BluePeak.Tests/BluePeak.Tests.csproj -c $Configuration --no-build `
        --logger "trx;LogFileName=engine-tests.trx" --results-directory $artifacts
    if ($LASTEXITCODE -ne 0) { throw 'engine tests failed' }

    Step 'Test — user interface and lifecycle'
    dotnet test tests/BluePeak.UiTests/BluePeak.UiTests.csproj -c $Configuration --no-build `
        --logger "trx;LogFileName=ui-tests.trx" --results-directory $artifacts
    if ($LASTEXITCODE -ne 0) { throw 'ui tests failed' }

    if ($Package) {
        Step 'Package — framework dependent (requires .NET 10 Desktop Runtime)'
        $fd = Join-Path $artifacts 'publish/framework-dependent'
        Remove-Item -Recurse -Force $fd -ErrorAction SilentlyContinue
        dotnet publish src/BluePeak.App/BluePeak.App.csproj -c $Configuration -r win-x64 `
            --self-contained false -p:PublishSingleFile=false -o $fd
        if ($LASTEXITCODE -ne 0) { throw 'framework-dependent publish failed' }

        Step 'Package — self contained x64 (no runtime required)'
        $sc = Join-Path $artifacts 'publish/self-contained-x64'
        Remove-Item -Recurse -Force $sc -ErrorAction SilentlyContinue
        dotnet publish src/BluePeak.App/BluePeak.App.csproj -c $Configuration -r win-x64 `
            --self-contained true -p:PublishSingleFile=false -o $sc
        if ($LASTEXITCODE -ne 0) { throw 'self-contained publish failed' }

        $fdSize = [math]::Round((Get-ChildItem $fd -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
        $scSize = [math]::Round((Get-ChildItem $sc -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
        Write-Host "framework-dependent: $fdSize MB" -ForegroundColor Green
        Write-Host "self-contained:      $scSize MB" -ForegroundColor Green
    }

    if ($Capture) {
        Step 'Design captures'
        $captures = Join-Path (Join-Path $root 'docs') 'captures'
        New-Item -ItemType Directory -Force -Path $captures | Out-Null
        $exe = Join-Path $root "src/BluePeak.App/bin/$Configuration/net10.0-windows/BluePeak.Operator.exe"
        if (-not (Test-Path $exe)) { throw "executable not found at $exe" }
        Start-Process -FilePath $exe -ArgumentList '--capture', $captures, '--capture-exit' -Wait -NoNewWindow
        $written = @(Get-ChildItem -Path $captures -Filter *.png -ErrorAction SilentlyContinue)
        Write-Host "$($written.Count) captures written to docs/captures" -ForegroundColor Green
        if ($written.Count -lt 17) { throw "expected 17 design captures, got $($written.Count)" }
    }

    Step 'Done'
}
finally {
    Pop-Location
}
