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

# Get current version from .csproj file
CURRENT_VERSION=$(grep '<PackageVersion>' GitGood/GitGood.csproj | sed 's/.*<PackageVersion>\([^<]*\)<\/PackageVersion>.*/\1/')
echo "Current version: $CURRENT_VERSION"

# Split version into parts
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"
MAJOR=${VERSION_PARTS[0]}
MINOR=${VERSION_PARTS[1]}
PATCH=${VERSION_PARTS[2]}

# Calculate next version
NEXT_PATCH=$((PATCH + 1))
NEXT_VERSION="$MAJOR.$MINOR.$NEXT_PATCH"

# Ask for version
read -p "Enter new version [$NEXT_VERSION]: " NEW_VERSION
NEW_VERSION=${NEW_VERSION:-$NEXT_VERSION}

# Update version in .csproj file
sed -i '' "s/<PackageVersion>$CURRENT_VERSION<\/PackageVersion>/<PackageVersion>$NEW_VERSION<\/PackageVersion>/" GitGood/GitGood.csproj

echo "🚀 Publishing GitGood v$NEW_VERSION to NuGet..."

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
dotnet nuget push "bin/Release/gitgood.$NEW_VERSION.nupkg" --api-key "$NUGET_API_KEY" --source "https://api.nuget.org/v3/index.json"

echo "✅ GitGood v$NEW_VERSION has been published successfully!"
echo ""
echo "You can install GitGood in one of two ways:"
echo "1. Install from NuGet (may take 5-10 minutes to be available):"
echo "   dotnet tool install -g gitgood"
echo ""
echo "2. Install directly from the local package:"
echo "   dotnet tool install -g gitgood --add-source ./bin/Release" 