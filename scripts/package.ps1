param(
    [string]$RepoRoot = "",
    [string]$PublishDir = "", # Will default to RepoRoot\publish if empty
    [string]$DotNet = "",
    [string]$Configuration = "Release",
    [string]$LegacyDir = ""
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
}

if ([string]::IsNullOrEmpty($PublishDir)) {
    $PublishDir = Join-Path $RepoRoot "publish"
}

if ([string]::IsNullOrWhiteSpace($DotNet)) {
    $dotNetCandidates = @(
        "dotnet"
    )

    foreach ($candidate in $dotNetCandidates) {
        if (Test-Path $candidate) {
            $DotNet = $candidate
            break
        }

        if ($candidate -eq "dotnet") {
            $dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
            if ($dotnetCmd) {
                $DotNet = "dotnet"
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($DotNet)) {
    throw "Could not find dotnet. Install .NET SDK or pass -DotNet explicitly."
}

$distDir = Join-Path $RepoRoot "installer\\dist"
$zipPath = Join-Path $distDir "TapScribe.zip"

if (Test-Path $PublishDir) {
    Write-Host "Cleaning $PublishDir"
    Remove-Item -Path $PublishDir -Recurse -Force
}

Write-Host "Publishing to $PublishDir"
& $DotNet publish (Join-Path $RepoRoot "WsprPc\\WsprPc.csproj") -c $Configuration -o $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

Write-Host "Cleaning up non-essential files..."
Get-ChildItem -Path $PublishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
if (Test-Path (Join-Path $PublishDir "createdump.exe")) {
    Remove-Item (Join-Path $PublishDir "createdump.exe") -Force
}

# Optional copy to legacy location
if (![string]::IsNullOrWhiteSpace($LegacyDir)) {
    if (Test-Path $LegacyDir) { Remove-Item $LegacyDir -Recurse -Force }
    Copy-Item -Path $PublishDir -Destination $LegacyDir -Recurse -Force
    Write-Host "Copied to legacy path: $LegacyDir"
}

if (!(Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip: $zipPath"
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $zipPath -Force

# Inno Setup location check
$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $possiblePaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "${env:LocalAppData}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }
}

if ($iscc) {
    Write-Host "Found Inno Setup compiler at $iscc"
    $issPath = Join-Path $RepoRoot "installer\\TapScribe.iss"
    
    # Update ISS output path to match script
    # Note: ISS has OutputDir=.\dist which is relative to ISS file location
    
    Write-Host "Compiling setup..."
    & $iscc $issPath
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Setup compilation failed."
    }
    else {
        Write-Host "Setup created successfully."
    }
}
else {
    Write-Warning "Inno Setup Compiler (ISCC.exe) not found. Skipping setup creation."
    Write-Warning "Please install Inno Setup 6+ to build the installer executable."
}

Write-Host "Done."
