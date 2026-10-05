[CmdletBinding()]
param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$artifactsRoot = Join-Path $repoRoot "artifacts\installer"
$serverPublish = Join-Path $artifactsRoot "server-publish"
$clientPublish = Join-Path $artifactsRoot "client-publish"
$outputRoot = Join-Path $artifactsRoot "out"

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "$Name is required." }
}

function Find-Iscc {
    $candidates = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        (Join-Path $env:RUNNER_TEMP "InnoSetup\ISCC.exe")
    ) | Where-Object { $_ -and (Test-Path $_) }

    if ($candidates.Count -gt 0) { return $candidates[0] }

    $tempInstaller = Join-Path $env:RUNNER_TEMP "innosetup-7.1.0-x64.exe"
    $installDir = Join-Path $env:RUNNER_TEMP "InnoSetup"
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null

    Write-Host "Inno Setup 7.1.0 was not found. Downloading the official signed x64 installer."
    Invoke-WebRequest -Uri "https://github.com/jrsoftware/issrc/releases/download/is-7_1_0/innosetup-7.1.0-x64.exe" -OutFile $tempInstaller

    $signature = Get-AuthenticodeSignature -FilePath $tempInstaller
    if ($signature.Status -ne "Valid" -or $signature.SignerCertificate.Subject -notmatch "Pyrsys B.V.") {
        throw "Inno Setup installer signature validation failed."
    }

    $process = Start-Process -FilePath $tempInstaller -ArgumentList @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/CURRENTUSER",
        "/DIR=$installDir"
    ) -Wait -PassThru

    if ($process.ExitCode -ne 0) {
        throw "Inno Setup bootstrap installer failed with exit code $($process.ExitCode)."
    }

    $iscc = Join-Path $installDir "ISCC.exe"
    if (-not (Test-Path $iscc)) {
        throw "Inno Setup bootstrap completed but ISCC.exe was not found."
    }

    return $iscc
}
Require-Command "dotnet"
Require-Command "node"
Require-Command "npm"
$iscc = Find-Iscc

Remove-Item $artifactsRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $serverPublish,$clientPublish,$outputRoot -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet restore GameNetManager.sln
    npm ci --prefix src/Dashboard
    npm run build --prefix src/Dashboard
    dotnet publish src/Server/GameNetManager.Server.csproj --configuration $Configuration --runtime win-x64 --self-contained true --output $serverPublish --no-restore
    dotnet publish src/Client/GameNetManager.Client.csproj --configuration $Configuration --runtime win-x64 --self-contained true --output $clientPublish --no-restore
    Set-Content -Path (Join-Path $serverPublish "server-version.txt") -Value $Version -Encoding ascii
    Set-Content -Path (Join-Path $clientPublish "client-version.txt") -Value $Version -Encoding ascii
    & $iscc "/DAppVersion=$Version" "/O$outputRoot" (Join-Path $repoRoot "installer\server\GameNetManager-Server.iss")
    if ($LASTEXITCODE -ne 0) { throw "Server Setup compilation failed." }
    & $iscc "/DAppVersion=$Version" "/O$outputRoot" (Join-Path $repoRoot "installer\client\GameNetManager-Client.iss")
    if ($LASTEXITCODE -ne 0) { throw "Client Setup compilation failed." }
    $serverSetup = Join-Path $outputRoot "GameNetManager-Server-Setup-$Version.exe"
    $clientSetup = Join-Path $outputRoot "GameNetManager-Client-Setup-$Version.exe"
    foreach ($file in @($serverSetup,$clientSetup)) {
        if (-not (Test-Path $file)) { throw "Expected installer was not produced: $file" }
        Write-Host ("Installer: {0} ({1} bytes)" -f $file, (Get-Item $file).Length)
    }

    $hashLines = foreach ($file in @($serverSetup,$clientSetup)) {
        $hash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash.ToLowerInvariant()
        "$hash  $(Split-Path $file -Leaf)"
    }
    $hashLines | Set-Content -Path (Join-Path $outputRoot "SHA256SUMS.txt") -Encoding ascii
}
finally { Pop-Location }