#!/bin/bash

# Exit on error
set -e

# Check if NuGet API key is provided
if [ -z "$1" ]; then
    echo "Error: NuGet API key is required"
    echo "Usage: ./publish.sh <nuget-api-key>"
    exit 1
fi

NUGET_API_KEY=$1

echo "🚀 Publishing GitGood to NuGet..."

# Ensure we're in the GitGood directory
cd GitGood

# Create necessary directories
mkdir -p bin/Release

# Build the project
echo "📦 Building GitGood..."
dotnet build -c Release

# Create the package
echo "📦 Creating NuGet package..."
dotnet pack -c Release

# Push to NuGet
echo "📤 Publishing to NuGet..."
dotnet nuget push "bin/Release/gitgood.1.0.0.nupkg" --api-key "$NUGET_API_KEY" --source "https://api.nuget.org/v3/index.json"

echo "✅ GitGood has been published successfully!"
echo ""
echo "You can install GitGood in one of two ways:"
echo "1. Install from NuGet (may take 5-10 minutes to be available):"
echo "   dotnet tool install -g gitgood"
echo ""
echo "2. Install directly from the local package:"
echo "   dotnet tool install -g gitgood --add-source ./bin/Release" 