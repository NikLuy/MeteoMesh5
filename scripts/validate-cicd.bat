@echo off
echo ?? Running MeteoMesh5 CI/CD Validation Script
echo =============================================

echo.
echo 1. Checking .NET SDK...
dotnet --version
if %errorlevel% neq 0 (
    echo ? .NET SDK not found
    exit /b 1
) else (
    echo ? .NET SDK check
)

echo.
echo 2. Restoring NuGet packages...
dotnet restore
if %errorlevel% neq 0 (
    echo ? Package restore failed
    exit /b 1
) else (
    echo ? Package restore
)

echo.
echo 3. Building solution...
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ? Solution build failed
    exit /b 1
) else (
    echo ? Solution build
)

echo.
echo 4. Running unit tests...

echo   - Testing CentralServer...
if exist "MeteoMesh5.CentralServer.Tests" (
    dotnet test MeteoMesh5.CentralServer.Tests --configuration Release --no-build --verbosity minimal
    if %errorlevel% neq 0 (
        echo ? CentralServer tests failed
        exit /b 1
    ) else (
        echo ? CentralServer tests
    )
) else (
    echo ??  CentralServer.Tests directory not found
)

echo   - Testing LocalNode...
if exist "MeteoMesh5.LocalNode.Tests" (
    dotnet test MeteoMesh5.LocalNode.Tests --configuration Release --no-build --verbosity minimal
    if %errorlevel% neq 0 (
        echo ? LocalNode tests failed
        exit /b 1
    ) else (
        echo ? LocalNode tests
    )
) else (
    echo ??  LocalNode.Tests directory not found
)

echo   - Testing MeteringStation...
if exist "MeteoMesh5.MeteringStation.Tests" (
    dotnet test MeteoMesh5.MeteringStation.Tests --configuration Release --no-build --verbosity minimal
    if %errorlevel% neq 0 (
        echo ? MeteringStation tests failed
        exit /b 1
    ) else (
        echo ? MeteringStation tests
    )
) else (
    echo ??  MeteringStation.Tests directory not found
)

echo.
echo 5. Checking Docker setup...
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ??  Docker not found - skipping Docker tests
) else (
    echo   - Docker version:
    docker --version
    echo ? Docker availability
    
    echo   - Building Docker images (this may take a while)...
    echo     Building CentralServer image...
    docker build -f MeteoMesh5.CentralServer\Dockerfile -t meteomesh5-test:central-server . --quiet
    if %errorlevel% neq 0 (
        echo ? Docker build test failed
        exit /b 1
    ) else (
        echo ? Docker build test
    )
    
    echo     Cleaning up test image...
    docker rmi meteomesh5-test:central-server --force >nul 2>&1
)

echo.
echo 6. Validating CI/CD files...

if exist ".github\workflows\ci-cd.yml" (
    echo ? CI/CD workflow file exists
) else (
    echo ? CI/CD workflow file missing
    exit /b 1
)

REM Check Dockerfiles
if exist "MeteoMesh5.CentralServer\Dockerfile" (echo ? MeteoMesh5.CentralServer\Dockerfile) else (echo ? MeteoMesh5.CentralServer\Dockerfile)
if exist "MeteoMesh5.LocalNode\Dockerfile" (echo ? MeteoMesh5.LocalNode\Dockerfile) else (echo ? MeteoMesh5.LocalNode\Dockerfile)
if exist "MeteoMesh5.TemperatureStation\Dockerfile" (echo ? MeteoMesh5.TemperatureStation\Dockerfile) else (echo ? MeteoMesh5.TemperatureStation\Dockerfile)
if exist "MeteoMesh5.HumidityStation\Dockerfile" (echo ? MeteoMesh5.HumidityStation\Dockerfile) else (echo ? MeteoMesh5.HumidityStation\Dockerfile)
if exist "MeteoMesh5.PressureStation\Dockerfile" (echo ? MeteoMesh5.PressureStation\Dockerfile) else (echo ? MeteoMesh5.PressureStation\Dockerfile)
if exist "MeteoMesh5.LidarStation\Dockerfile" (echo ? MeteoMesh5.LidarStation\Dockerfile) else (echo ? MeteoMesh5.LidarStation\Dockerfile)
if exist "MeteoMesh5.Host\Dockerfile" (echo ? MeteoMesh5.Host\Dockerfile) else (echo ? MeteoMesh5.Host\Dockerfile)

if exist "docker-compose.yml" (
    echo ? docker-compose.yml exists
) else (
    echo ? docker-compose.yml missing
    exit /b 1
)

echo.
echo 7. Project structure validation...

if exist "MeteoMesh5.CentralServer" if exist "MeteoMesh5.CentralServer\MeteoMesh5.CentralServer.csproj" (echo ? MeteoMesh5.CentralServer) else (echo ? MeteoMesh5.CentralServer)
if exist "MeteoMesh5.LocalNode" if exist "MeteoMesh5.LocalNode\MeteoMesh5.LocalNode.csproj" (echo ? MeteoMesh5.LocalNode) else (echo ? MeteoMesh5.LocalNode)
if exist "MeteoMesh5.Host" if exist "MeteoMesh5.Host\MeteoMesh5.Host.csproj" (echo ? MeteoMesh5.Host) else (echo ? MeteoMesh5.Host)
if exist "MeteoMesh5.TemperatureStation" if exist "MeteoMesh5.TemperatureStation\MeteoMesh5.TemperatureStation.csproj" (echo ? MeteoMesh5.TemperatureStation) else (echo ? MeteoMesh5.TemperatureStation)
if exist "MeteoMesh5.HumidityStation" if exist "MeteoMesh5.HumidityStation\MeteoMesh5.HumidityStation.csproj" (echo ? MeteoMesh5.HumidityStation) else (echo ? MeteoMesh5.HumidityStation)
if exist "MeteoMesh5.PressureStation" if exist "MeteoMesh5.PressureStation\MeteoMesh5.PressureStation.csproj" (echo ? MeteoMesh5.PressureStation) else (echo ? MeteoMesh5.PressureStation)
if exist "MeteoMesh5.LidarStation" if exist "MeteoMesh5.LidarStation\MeteoMesh5.LidarStation.csproj" (echo ? MeteoMesh5.LidarStation) else (echo ? MeteoMesh5.LidarStation)
if exist "MeteoMesh5.Shared" if exist "MeteoMesh5.Shared\MeteoMesh5.Shared.csproj" (echo ? MeteoMesh5.Shared) else (echo ? MeteoMesh5.Shared)

echo.
echo ?? All validations completed successfully!
echo.
echo Next steps:
echo 1. Commit and push your changes to trigger the CI/CD pipeline
echo 2. Check GitHub Actions for build status
echo 3. Use 'docker-compose up --build' for local testing
echo.
echo For more information, see docs\CI-CD-Setup.md
pause