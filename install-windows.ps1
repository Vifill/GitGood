# Exit on error
$ErrorActionPreference = "Stop"

Write-Host "🚀 Installing GitGood for Windows..."

# Build the project
Write-Host "📦 Building GitGood..."
dotnet publish GitGood/GitGood.csproj -c Release -r win-x64

# Create installation directory
$INSTALL_DIR = "$env:ProgramFiles\GitGood"
Write-Host "📁 Installing to $INSTALL_DIR"

# Create the installation directory if it doesn't exist
if (-not (Test-Path $INSTALL_DIR)) {
    New-Item -ItemType Directory -Path $INSTALL_DIR -Force | Out-Null
}

# Copy the executable to the installation directory
Copy-Item "GitGood\bin\Release\net9.0\win-x64\publish\GitGood.exe" "$INSTALL_DIR\gitgood.exe"

# Add to PATH if not already there
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($currentPath -notlike "*$INSTALL_DIR*") {
    $newPath = $currentPath + ";$INSTALL_DIR"
    [Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")
}

Write-Host "✅ GitGood has been installed successfully!"
Write-Host "You can now use 'gitgood' from anywhere in your terminal."
Write-Host ""
Write-Host "Try it out with:"
Write-Host "  gitgood config    # Configure your API keys"
Write-Host "  gitgood commit    # Start a commit" 