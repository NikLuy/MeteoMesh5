#!/bin/bash

echo "?? Running MeteoMesh5 CI/CD Validation Script"
echo "============================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print status
print_status() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}? $2${NC}"
    else
        echo -e "${RED}? $2${NC}"
        exit 1
    fi
}

print_warning() {
    echo -e "${YELLOW}??  $1${NC}"
}

echo ""
echo "1. Checking .NET SDK..."
dotnet --version
print_status $? ".NET SDK check"

echo ""
echo "2. Restoring NuGet packages..."
dotnet restore
print_status $? "Package restore"

echo ""
echo "3. Building solution..."
dotnet build --configuration Release --no-restore
print_status $? "Solution build"

echo ""
echo "4. Running unit tests..."

echo "  - Testing CentralServer..."
if [ -d "MeteoMesh5.CentralServer.Tests" ]; then
    dotnet test MeteoMesh5.CentralServer.Tests --configuration Release --no-build --verbosity minimal
    print_status $? "CentralServer tests"
else
    print_warning "CentralServer.Tests directory not found"
fi

echo "  - Testing LocalNode..."
if [ -d "MeteoMesh5.LocalNode.Tests" ]; then
    dotnet test MeteoMesh5.LocalNode.Tests --configuration Release --no-build --verbosity minimal
    print_status $? "LocalNode tests"
else
    print_warning "LocalNode.Tests directory not found"
fi

echo "  - Testing MeteringStation..."
if [ -d "MeteoMesh5.MeteringStation.Tests" ]; then
    dotnet test MeteoMesh5.MeteringStation.Tests --configuration Release --no-build --verbosity minimal
    print_status $? "MeteringStation tests"
else
    print_warning "MeteringStation.Tests directory not found"
fi

echo ""
echo "5. Checking Docker setup..."

if command -v docker &> /dev/null; then
    echo "  - Docker version:"
    docker --version
    print_status $? "Docker availability"
    
    echo "  - Building Docker images (this may take a while)..."
    
    # Build one image as a test
    echo "    Building CentralServer image..."
    docker build -f MeteoMesh5.CentralServer/Dockerfile -t meteomesh5-test:central-server . --quiet
    print_status $? "Docker build test"
    
    echo "    Cleaning up test image..."
    docker rmi meteomesh5-test:central-server --force > /dev/null 2>&1
    
else
    print_warning "Docker not found - skipping Docker tests"
fi

echo ""
echo "6. Validating CI/CD files..."

# Check CI/CD workflow file
if [ -f ".github/workflows/ci-cd.yml" ]; then
    print_status 0 "CI/CD workflow file exists"
else
    print_status 1 "CI/CD workflow file missing"
fi

# Check Dockerfiles
DOCKERFILES=(
    "MeteoMesh5.CentralServer/Dockerfile"
    "MeteoMesh5.LocalNode/Dockerfile"
    "MeteoMesh5.TemperatureStation/Dockerfile"
    "MeteoMesh5.HumidityStation/Dockerfile"
    "MeteoMesh5.PressureStation/Dockerfile"
    "MeteoMesh5.LidarStation/Dockerfile"
    "MeteoMesh5.Host/Dockerfile"
)

for dockerfile in "${DOCKERFILES[@]}"; do
    if [ -f "$dockerfile" ]; then
        echo -e "${GREEN}? $dockerfile${NC}"
    else
        echo -e "${RED}? $dockerfile${NC}"
    fi
done

# Check docker-compose
if [ -f "docker-compose.yml" ]; then
    print_status 0 "docker-compose.yml exists"
else
    print_status 1 "docker-compose.yml missing"
fi

echo ""
echo "7. Project structure validation..."

PROJECTS=(
    "MeteoMesh5.CentralServer"
    "MeteoMesh5.LocalNode"
    "MeteoMesh5.Host"
    "MeteoMesh5.TemperatureStation"
    "MeteoMesh5.HumidityStation"
    "MeteoMesh5.PressureStation"
    "MeteoMesh5.LidarStation"
    "MeteoMesh5.Shared"
)

for project in "${PROJECTS[@]}"; do
    if [ -d "$project" ] && [ -f "$project/$project.csproj" ]; then
        echo -e "${GREEN}? $project${NC}"
    else
        echo -e "${RED}? $project${NC}"
    fi
done

echo ""
echo "8. Testing docker-compose setup..."
if command -v docker-compose &> /dev/null; then
    echo "  - Validating docker-compose.yml..."
    docker-compose config > /dev/null 2>&1
    print_status $? "docker-compose configuration"
else
    print_warning "docker-compose not found - skipping compose validation"
fi

echo ""
echo -e "${GREEN}?? All validations completed successfully!${NC}"
echo ""
echo "Next steps:"
echo "1. Commit and push your changes to trigger the CI/CD pipeline"
echo "2. Check GitHub Actions for build status"
echo "3. Use 'docker-compose up --build' for local testing"
echo ""
echo "For more information, see docs/CI-CD-Setup.md"