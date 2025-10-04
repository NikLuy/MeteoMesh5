#!/bin/bash

# MeteoMesh5.0 Quick Build Script
# Builds all projects and Docker images for local development

set -e  # Exit on any error

echo "?? Starting MeteoMesh5.0 Quick Build..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if .NET 9 is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET CLI not found. Please install .NET 9 SDK."
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
print_status "Using .NET version: $DOTNET_VERSION"

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    print_warning "Docker not found. Skipping container builds."
    SKIP_DOCKER=true
fi

# Clean previous builds
print_status "Cleaning previous builds..."
dotnet clean MeteoMesh.MeteringStation.sln --verbosity quiet

# Restore dependencies
print_status "Restoring NuGet packages..."
dotnet restore MeteoMesh.MeteringStation.sln

# Build all projects
print_status "Building solution..."
dotnet build MeteoMesh.MeteringStation.sln --configuration Release --no-restore

# Run tests
print_status "Running tests..."
dotnet test MeteoMesh.MeteringStation.sln --configuration Release --no-build --verbosity normal

if [ "$SKIP_DOCKER" != true ]; then
    print_status "Building Docker images (AMD64 only)..."
    print_status "Note: Skipping Aspire AppHost (MeteoMesh5.Host) - not intended for containerization"
    
    # Set Docker to build for AMD64 only to avoid QEMU issues
    export DOCKER_DEFAULT_PLATFORM=linux/amd64
    
    # Build each service individually (excluding AppHost)
    print_status "Building CentralServer image..."
    docker build -f MeteoMesh5.CentralServer/Dockerfile -t meteomesh5-central:latest . --platform linux/amd64
    
    print_status "Building LocalNode image..."
    docker build -f MeteoMesh5.LocalNode/Dockerfile -t meteomesh5-localnode:latest . --platform linux/amd64
    
    print_status "Building HumidityStation image..."
    docker build -f MeteoMesh5.HumidityStation/Dockerfile -t meteomesh5-humidity:latest . --platform linux/amd64
    
    print_status "Building LidarStation image..."
    docker build -f MeteoMesh5.LidarStation/Dockerfile -t meteomesh5-lidar:latest . --platform linux/amd64
    
    print_status "Building PressureStation image..."
    docker build -f MeteoMesh5.PressureStation/Dockerfile -t meteomesh5-pressure:latest . --platform linux/amd64
    
    print_status "Building TemperatureStation image..."
    docker build -f MeteoMesh5.TemperatureStation/Dockerfile -t meteomesh5-temperature:latest . --platform linux/amd64
    
    print_status "Docker images built successfully!"
    docker images | grep meteomesh5
fi

print_status "? Build completed successfully!"
print_status "?? Build Summary:"
echo "  - Solution: MeteoMesh.MeteringStation.sln"
echo "  - Projects: $(find . -name "*.csproj" | wc -l)"
echo "  - Target: .NET 9"
echo "  - Aspire AppHost: Available for local orchestration (not containerized)"
if [ "$SKIP_DOCKER" != true ]; then
    echo "  - Docker Images: 6 services built (AppHost excluded)"
fi

echo ""
print_status "?? Next steps:"
echo "  - Run tests: dotnet test"
echo "  - Start Aspire orchestration: dotnet run --project MeteoMesh5.Host"
echo "  - Start individual services: docker-compose up"
echo "  - View logs: docker-compose logs"