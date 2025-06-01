#!/bin/bash

# Remove existing installation
sudo rm -f /usr/bin/dotnet
sudo rm -rf /usr/share/dotnet

# Get script to install .NET if not there
if [ ! -f "dotnet-install.sh" ]; then
    wget https://dot.net/v1/dotnet-install.sh
    chmod +x dotnet-install.sh
fi

# Detect architecture for .NET install
ARCH=$(uname -m)
if [ "$ARCH" = "x86_64" ]; then
    DOTNET_ARCH="x64"
elif [ "$ARCH" = "aarch64" ] || [ "$ARCH" = "arm64" ]; then
    DOTNET_ARCH="arm64"
else
    echo "Unsupported architecture: $ARCH"
    exit 1
fi

# Install .NET
./dotnet-install.sh --install-dir /usr/share/dotnet --channel 9.0 --architecture $DOTNET_ARCH

# Create symbolic link
sudo ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet

# Add to shell configurations
DOTNET_ENV='export DOTNET_ROOT=/usr/share/dotnet\nexport PATH=$PATH:$DOTNET_ROOT'

# Update both bash and zsh configs if they exist
if [ -f "$HOME/.bashrc" ]; then
    echo -e "$DOTNET_ENV" >> "$HOME/.bashrc"
fi

if [ -f "$HOME/.zshrc" ]; then
    echo -e "$DOTNET_ENV" >> "$HOME/.zshrc"
fi

# Set environment variables for current session
export DOTNET_ROOT=/usr/share/dotnet
export PATH=$PATH:$DOTNET_ROOT


dotnet tool restore
dotnet paket restore
dotnet restore

echo "Setup complete. Please restart your shell or run: source ~/.bashrc (or ~/.zshrc)"