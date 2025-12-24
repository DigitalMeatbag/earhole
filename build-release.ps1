# Build and Publish Script for earhole
# This script builds the project in Release mode, publishes it as self-contained,
# and creates a ZIP archive of the publish directory.

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

# Step 1: Clean up old ZIP files in publish directory
Write-Host "Cleaning up old ZIP files..." -ForegroundColor Yellow
if (Test-Path publish) {
    Get-ChildItem publish\*.zip | Remove-Item -Force
}

# Step 2: Build in Release mode
Write-Host "Building in Release mode..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Step 3: Publish as self-contained executable with single file
Write-Host "Publishing self-contained executable..." -ForegroundColor Yellow
dotnet publish earhole.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Step 4: Create ZIP archive (only include necessary files)
$zipName = "earhole-v$(Get-Date -Format 'yyyy-MM-dd').zip"
Write-Host "Creating ZIP archive: $zipName" -ForegroundColor Yellow

# Create a temporary directory with only the essential files
$tempDir = "temp_release"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Copy only the executable and essential DLLs
Copy-Item publish\earhole.exe $tempDir\
Copy-Item publish\earhole.pdb $tempDir\ -ErrorAction SilentlyContinue

# Create the ZIP from the temp directory
Compress-Archive -Path $tempDir\* -DestinationPath $zipName -CompressionLevel Optimal

# Clean up temp directory
Remove-Item $tempDir -Recurse -Force

# Move ZIP to publish directory and get its size
Move-Item $zipName publish\ -Force
$zipSize = (Get-Item "publish\$zipName").Length / 1MB

# Clean up intermediate files (keep only the ZIP)
Write-Host "Cleaning up intermediate files..." -ForegroundColor Yellow
Get-ChildItem publish\ | Where-Object { $_.Name -ne $zipName } | Remove-Item -Recurse -Force

Write-Host "Release build completed successfully!" -ForegroundColor Green
Write-Host "ZIP file created: publish\$zipName" -ForegroundColor Cyan
Write-Host ("ZIP file size: {0:N2} MB" -f $zipSize) -ForegroundColor Cyan
if ($zipSize -lt 100) {
    Write-Host "Size is under 100MB limit - ready for distribution!" -ForegroundColor Green
} else {
    Write-Host "WARNING: Size exceeds 100MB limit!" -ForegroundColor Red
}
Write-Host "Upload this ZIP to GitHub Releases for distribution." -ForegroundColor Cyan
Write-Host "Publish directory: $(Resolve-Path publish)" -ForegroundColor Cyan