<#
    BluePeak Operator — produce and verify the distributable archives.

    Creates:
        dist/BluePeak-Operator-Windows-x64.zip                     self-contained, no runtime needed
        dist/BluePeak-Operator-Windows-x64-FrameworkDependent.zip  needs .NET 10 Desktop Runtime
        dist/BluePeak-Operator-Source.zip                          reproducible source tree

    Each archive is then verified by extracting it to a fresh directory: the Windows
    packages are launched from the extracted copy and must render every workspace, and
    the source archive must restore and build from clean.

    Usage:
        pwsh scripts/package.ps1              # build, package and verify
        pwsh scripts/package.ps1 -SkipVerify  # package only
#>
[CmdletBinding()]
param(
    [switch]$SkipVerify,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$artifacts = Join-Path $root 'artifacts'
Push-Location $root

function Step($text) {
    Write-Host ''
    Write-Host "=== $text" -ForegroundColor Cyan
}

function New-CleanDirectory($path) {
    Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $path | Out-Null
}

try {
    New-CleanDirectory $dist

    # ---------------------------------------------------------------- publish
    Step 'Publish self-contained win-x64'
    $sc = Join-Path $artifacts 'publish/self-contained-x64'
    New-CleanDirectory $sc
    dotnet publish src/BluePeak.App/BluePeak.App.csproj -c $Configuration -r win-x64 `
        --self-contained true -o $sc
    if ($LASTEXITCODE -ne 0) { throw 'self-contained publish failed' }

    Step 'Publish framework-dependent win-x64'
    $fd = Join-Path $artifacts 'publish/framework-dependent'
    New-CleanDirectory $fd
    dotnet publish src/BluePeak.App/BluePeak.App.csproj -c $Configuration -r win-x64 `
        --self-contained false -o $fd
    if ($LASTEXITCODE -ne 0) { throw 'framework-dependent publish failed' }

    # A publish folder can carry debug symbols and the .NET publish manifest; neither
    # belongs in a download.
    foreach ($folder in @($sc, $fd)) {
        Get-ChildItem $folder -Filter *.pdb -Recurse | Remove-Item -Force
        Get-ChildItem $folder -Filter *.staticwebassets.endpoints.json -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    # ---------------------------------------------------------------- windows archives
    Step 'Archive — BluePeak-Operator-Windows-x64.zip'
    Compress-Archive -Path (Join-Path $sc '*') -DestinationPath (Join-Path $dist 'BluePeak-Operator-Windows-x64.zip') -CompressionLevel Optimal

    Step 'Archive — BluePeak-Operator-Windows-x64-FrameworkDependent.zip'
    Compress-Archive -Path (Join-Path $fd '*') -DestinationPath (Join-Path $dist 'BluePeak-Operator-Windows-x64-FrameworkDependent.zip') -CompressionLevel Optimal

    # ---------------------------------------------------------------- source archive
    Step 'Archive — BluePeak-Operator-Source.zip'
    $staging = Join-Path $artifacts 'source-staging'
    New-CleanDirectory $staging

    # git archive gives exactly the tracked tree: no .git, no bin, no obj, no artifacts,
    # no IDE state, and nothing machine-specific.
    $tar = Join-Path $artifacts 'source.tar'
    git archive --format=tar --output=$tar HEAD
    if ($LASTEXITCODE -ne 0) { throw 'git archive failed — is the working tree committed?' }
    tar -xf $tar -C $staging
    Remove-Item $tar -Force

    # Compare paths relative to the staging root: the staging directory itself sits under
    # artifacts/, so matching against full paths would flag every file.
    $stagingPrefix = (Resolve-Path $staging).Path.TrimEnd('\') + '\'
    $leaked = Get-ChildItem $staging -Recurse -Force | ForEach-Object {
        $relative = $_.FullName.Substring($stagingPrefix.Length)
        if ($relative -match '(^|\\)(bin|obj|\.vs|\.idea|TestResults|artifacts|dist)(\\|$)' -or
            $relative -match '(^|\\)\.git(\\|$)' -or
            $relative -match '\.(user|suo|trx|pdb)$') { $relative }
    }
    if ($leaked) { throw "source archive would contain build output: $($leaked[0])" }
    Write-Host "source tree staged clean: $((Get-ChildItem $staging -Recurse -File).Count) files" -ForegroundColor Green

    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath (Join-Path $dist 'BluePeak-Operator-Source.zip') -CompressionLevel Optimal
    Remove-Item -Recurse -Force $staging

    # ---------------------------------------------------------------- verification
    if (-not $SkipVerify) {
        $verify = Join-Path $artifacts 'zip-verify'
        New-CleanDirectory $verify

        Step 'Verify — launch the self-contained package from a fresh extraction'
        $scOut = Join-Path $verify 'windows-x64'
        Expand-Archive -Path (Join-Path $dist 'BluePeak-Operator-Windows-x64.zip') -DestinationPath $scOut
        $scExe = Join-Path $scOut 'BluePeak.Operator.exe'
        if (-not (Test-Path $scExe)) { throw 'BluePeak.Operator.exe missing from the Windows archive' }
        $shots = Join-Path $verify 'windows-x64-captures'
        New-Item -ItemType Directory -Force -Path $shots | Out-Null
        Start-Process -FilePath $scExe -ArgumentList '--capture', $shots, '--capture-exit' -Wait -NoNewWindow
        $n = @(Get-ChildItem $shots -Filter *.png -ErrorAction SilentlyContinue).Count
        Write-Host "self-contained package rendered $n surfaces from a fresh extraction" -ForegroundColor Green
        if ($n -lt 17) { throw "self-contained package rendered $n surfaces, expected 17" }

        Step 'Verify — launch the framework-dependent package from a fresh extraction'
        $fdOut = Join-Path $verify 'framework-dependent'
        Expand-Archive -Path (Join-Path $dist 'BluePeak-Operator-Windows-x64-FrameworkDependent.zip') -DestinationPath $fdOut
        $fdExe = Join-Path $fdOut 'BluePeak.Operator.exe'
        $fdShots = Join-Path $verify 'framework-dependent-captures'
        New-Item -ItemType Directory -Force -Path $fdShots | Out-Null
        Start-Process -FilePath $fdExe -ArgumentList '--capture', $fdShots, '--capture-exit' -Wait -NoNewWindow
        $m = @(Get-ChildItem $fdShots -Filter *.png -ErrorAction SilentlyContinue).Count
        Write-Host "framework-dependent package rendered $m surfaces from a fresh extraction" -ForegroundColor Green
        if ($m -lt 17) { throw "framework-dependent package rendered $m surfaces, expected 17" }

        Step 'Verify — restore, build and test the source archive from a fresh extraction'
        $srcOut = Join-Path $verify 'source'
        Expand-Archive -Path (Join-Path $dist 'BluePeak-Operator-Source.zip') -DestinationPath $srcOut
        Push-Location $srcOut
        try {
            dotnet restore BluePeakOperator.slnx
            if ($LASTEXITCODE -ne 0) { throw 'restore failed in the extracted source tree' }
            dotnet build BluePeakOperator.slnx -c $Configuration --no-restore -warnaserror
            if ($LASTEXITCODE -ne 0) { throw 'build failed in the extracted source tree' }
            dotnet test tests/BluePeak.Tests/BluePeak.Tests.csproj -c $Configuration --no-build
            if ($LASTEXITCODE -ne 0) { throw 'engine tests failed in the extracted source tree' }
            dotnet test tests/BluePeak.UiTests/BluePeak.UiTests.csproj -c $Configuration --no-build
            if ($LASTEXITCODE -ne 0) { throw 'ui tests failed in the extracted source tree' }
        }
        finally { Pop-Location }
        Write-Host 'source archive restores, builds and tests from clean' -ForegroundColor Green
    }

    # ---------------------------------------------------------------- manifest
    Step 'Archive manifest'
    $manifest = foreach ($file in Get-ChildItem $dist -Filter *.zip | Sort-Object Name) {
        [pscustomobject]@{
            File   = $file.Name
            Bytes  = $file.Length
            Size   = '{0:N1} MB' -f ($file.Length / 1MB)
            SHA256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
        }
    }
    $manifest | Format-Table -AutoSize | Out-String | Write-Host
    $commit = (git rev-parse HEAD).Trim()
    $lines = @("BluePeak Operator — distribution manifest", "commit $commit", "built $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')", "")
    foreach ($m in $manifest) { $lines += "$($m.File)`n  bytes  $($m.Bytes)`n  sha256 $($m.SHA256)`n" }
    $lines -join "`n" | Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding utf8
    Write-Host "manifest written to dist/SHA256SUMS.txt" -ForegroundColor Green

    Step 'Done'
}
finally {
    Pop-Location
}
