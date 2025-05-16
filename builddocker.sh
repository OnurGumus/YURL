#!/bin/bash

# Ensure we're in a git repository
if [ ! -d .git ]; then
    echo "This script must be run from the root of a git repository."
    exit 1
fi

# Get the current date and short commit SHA
DATE=$(date +%Y%m%d)
COMMIT_SHA=$(git rev-parse --short HEAD)

# Combine date and commit SHA for the tag
TAG="${DATE}-${COMMIT_SHA}"
docker buildx build --platform linux/amd64  -t  docker.3dpack.ing/shorten:$TAG --push .
# Build the Docker image with the combined tag


