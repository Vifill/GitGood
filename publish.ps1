# Exit on error
$ErrorActionPreference = "Stop"

# Check if NuGet API key is provided
param(
    [Parameter(Mandatory=$true)]
    [string]$NuGetApiKey
)

Write-Host "🚀 Publishing GitGood to NuGet..."

# Build the project
Write-Host "📦 Building GitGood..."
dotnet build

# Create the package
Write-Host "📦 Creating NuGet package..."
dotnet pack

# Push to NuGet
Write-Host "📤 Publishing to NuGet..."
dotnet nuget push "bin/Release/gitgood.1.0.0.nupkg" --api-key $NuGetApiKey --source "https://api.nuget.org/v3/index.json"

Write-Host "✅ GitGood has been published successfully!"
Write-Host ""
Write-Host "Users can now install it with:"
Write-Host "  dotnet tool install -g gitgood" 