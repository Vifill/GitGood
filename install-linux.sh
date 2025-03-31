#!/bin/bash

# Exit on error
set -e

echo "🚀 Installing GitGood for Linux..."

# Build the project
echo "📦 Building GitGood..."
dotnet publish GitGood/GitGood.csproj -c Release -r linux-x64

# Create installation directory
INSTALL_DIR="/usr/local/bin"
echo "📁 Installing to $INSTALL_DIR"

# Copy the executable to the installation directory
sudo cp GitGood/bin/Release/net9.0/linux-x64/publish/GitGood "$INSTALL_DIR/gitgood"

# Make the executable executable
sudo chmod +x "$INSTALL_DIR/gitgood"

echo "✅ GitGood has been installed successfully!"
echo "You can now use 'gitgood' from anywhere in your terminal."
echo ""
echo "Try it out with:"
echo "  gitgood config    # Configure your API keys"
echo "  gitgood commit    # Start a commit" 