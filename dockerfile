# Build Stage
FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /App

# Install Node.js and Yarn - single layer for system dependencies
RUN apt-get update && apt-get install -y curl && \
    curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
    apt-get install -y nodejs && \
    corepack enable && \

# Copy solution and config files first
COPY *.sln ./
COPY *.config ./

# Copy dependency files
COPY paket.lock paket.dependencies ./
COPY paket-files/ paket-files/
COPY package.json package-lock.json ./

# Install dependencies in a single layer - these are cached unless dependencies change
RUN dotnet tool restore && \
    dotnet paket restore && \
    npm ci


# Copy and build Server project
WORKDIR /App
COPY src/Server ./src/Server
WORKDIR /App/src/Server
RUN dotnet build -c Release

# Final build and publish
WORKDIR /App
COPY Shorten.sln ./
RUN dotnet restore && \
    dotnet build -c Release && \
    dotnet run --project build/ -- -t PublishServer -s

# Verify the publish output
RUN ls -la /App/deploy

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /App

# Copy the published output from the build stage
COPY --from=build-env /App/deploy .

# Expose the necessary port
EXPOSE 8080

# Set the entry point for the application
ENTRYPOINT ["dotnet", "Server.dll"]