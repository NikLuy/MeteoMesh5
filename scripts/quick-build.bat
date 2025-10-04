@echo off
setlocal enabledelayedexpansion

REM MeteoMesh5.0 Quick Build Script for Windows
REM Builds all projects and Docker images for local development

echo ?? Starting MeteoMesh5.0 Quick Build...

REM Check if .NET 9 is installed
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] .NET CLI not found. Please install .NET 9 SDK.
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo [INFO] Using .NET version: %DOTNET_VERSION%

REM Check if Docker is available
docker --version >nul 2>&1
if errorlevel 1 (
    echo [WARN] Docker not found. Skipping container builds.
    set SKIP_DOCKER=true
)

REM Clean previous builds
echo [INFO] Cleaning previous builds...
dotnet clean MeteoMesh.MeteringStation.sln --verbosity quiet

REM Restore dependencies
echo [INFO] Restoring NuGet packages...
dotnet restore MeteoMesh.MeteringStation.sln

REM Build all projects
echo [INFO] Building solution...
dotnet build MeteoMesh.MeteringStation.sln --configuration Release --no-restore

REM Run tests
echo [INFO] Running tests...
dotnet test MeteoMesh.MeteringStation.sln --configuration Release --no-build --verbosity normal

if not "%SKIP_DOCKER%"=="true" (
    echo [INFO] Building Docker images ^(AMD64 only^)...
    echo [INFO] Note: Skipping Aspire AppHost ^(MeteoMesh5.Host^) - not intended for containerization
    
    REM Set Docker to build for AMD64 only to avoid QEMU issues
    set DOCKER_DEFAULT_PLATFORM=linux/amd64
    
    REM Build each service individually (excluding AppHost)
    echo [INFO] Building CentralServer image...
    docker build -f MeteoMesh5.CentralServer/Dockerfile -t meteomesh5-central:latest . --platform linux/amd64
    
    echo [INFO] Building LocalNode image...
    docker build -f MeteoMesh5.LocalNode/Dockerfile -t meteomesh5-localnode:latest . --platform linux/amd64
    
    echo [INFO] Building HumidityStation image...
    docker build -f MeteoMesh5.HumidityStation/Dockerfile -t meteomesh5-humidity:latest . --platform linux/amd64
    
    echo [INFO] Building LidarStation image...
    docker build -f MeteoMesh5.LidarStation/Dockerfile -t meteomesh5-lidar:latest . --platform linux/amd64
    
    echo [INFO] Building PressureStation image...
    docker build -f MeteoMesh5.PressureStation/Dockerfile -t meteomesh5-pressure:latest . --platform linux/amd64
    
    echo [INFO] Building TemperatureStation image...
    docker build -f MeteoMesh5.TemperatureStation/Dockerfile -t meteomesh5-temperature:latest . --platform linux/amd64
    
    echo [INFO] Docker images built successfully!
    docker images | findstr meteomesh5
)

echo [INFO] ? Build completed successfully!
echo [INFO] ?? Build Summary:
echo   - Solution: MeteoMesh.MeteringStation.sln
echo   - Target: .NET 9
echo   - Aspire AppHost: Available for local orchestration ^(not containerized^)
if not "%SKIP_DOCKER%"=="true" (
    echo   - Docker Images: 6 services built ^(AppHost excluded^)
)

echo.
echo [INFO] ?? Next steps:
echo   - Run tests: dotnet test
echo   - Start Aspire orchestration: dotnet run --project MeteoMesh5.Host
echo   - Start individual services: docker-compose up
echo   - View logs: docker-compose logs

pause