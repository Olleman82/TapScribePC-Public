param(
    [string]$RepoRoot = "D:\\Appar\\wspr-pc",
    [string]$DotNet = "D:\\dotnet\\dotnet",
    [string]$Configuration = "Release"
)

$publishDir = "c:\tapscribe"
$distDir = Join-Path $RepoRoot "installer\\dist"
$zipPath = Join-Path $distDir "TapScribe.zip"

if (Test-Path $publishDir) {
    Write-Host "Cleaning $publishDir"
    Remove-Item -Path $publishDir -Recurse -Force
}

Write-Host "Publishing to $publishDir"
& $DotNet publish (Join-Path $RepoRoot "WsprPc\\WsprPc.csproj") -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

Write-Host "Cleaning up non-essential files..."
Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse | Remove-Item -Force
if (Test-Path (Join-Path $publishDir "createdump.exe")) {
    Remove-Item (Join-Path $publishDir "createdump.exe") -Force
}

if (!(Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating zip: $zipPath"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
Write-Host "Done."
