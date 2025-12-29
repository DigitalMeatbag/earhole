# Build and Publish Script for earhole
# This script builds the project in Release mode, publishes it as framework-dependent,
# and creates a ZIP archive of the publish directory.
# NOTE: Users will need .NET 6.0 Runtime installed to run the application.

Write-Host "Starting earhole release build process..." -ForegroundColor Green

# Step 0: Verify we're on the main branch
Write-Host "Checking git branch..." -ForegroundColor Yellow
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "ERROR: Releases can only be built from the 'main' branch!" -ForegroundColor Red
    Write-Host "Current branch: $currentBranch" -ForegroundColor Red
    exit 1
}
Write-Host "On main branch - proceeding with build" -ForegroundColor Green

# Step 1: Clean up publish directory
Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
if (Test-Path publish) {
    Remove-Item publish\* -Recurse -Force
} else {
    New-Item -ItemType Directory -Path publish | Out-Null
}

# Step 2: Build in Release mode
Write-Host "Building in Release mode..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Step 3: Publish as framework-dependent (requires .NET 6.0 Runtime)
Write-Host "Publishing framework-dependent executable..." -ForegroundColor Yellow
dotnet publish earhole.csproj -c Release -r win-x64 --no-self-contained `
    -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Step 4: Create ZIP archive of full publish output (keeps native deps intact)
$zipName = "earhole-v$(Get-Date -Format 'yyyy-MM-dd').zip"
Write-Host "Creating ZIP archive: $zipName" -ForegroundColor Yellow

# Zip everything in publish (excluding existing zips)
$itemsToZip = Get-ChildItem publish | Where-Object { $_.Extension -ne '.zip' }
Compress-Archive -Path $itemsToZip.FullName -DestinationPath $zipName -CompressionLevel Optimal

# Move ZIP to publish directory and get its size
Move-Item $zipName publish\ -Force
$zipSize = (Get-Item "publish\$zipName").Length / 1MB

# Keep published files for local testing; ZIP is in publish\

Write-Host "Release build completed successfully!" -ForegroundColor Green
Write-Host "ZIP file created: publish\$zipName" -ForegroundColor Cyan
Write-Host ("ZIP file size: {0:N2} MB" -f $zipSize) -ForegroundColor Cyan
Write-Host "NOTE: Users will need .NET 6.0 Runtime or later installed!" -ForegroundColor Yellow
Write-Host "Download link: https://dotnet.microsoft.com/download/dotnet/6.0" -ForegroundColor Yellow
if ($zipSize -lt 100) {
    Write-Host "Size is under 100MB limit - ready for distribution!" -ForegroundColor Green
} else {
    Write-Host "WARNING: Size exceeds 100MB limit!" -ForegroundColor Red
}
Write-Host "Upload this ZIP to GitHub Releases for distribution." -ForegroundColor Cyan
Write-Host "Publish directory: $(Resolve-Path publish)" -ForegroundColor Cyan