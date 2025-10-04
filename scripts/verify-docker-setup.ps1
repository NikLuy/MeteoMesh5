#!/usr/bin/env pwsh
# MeteoMesh5.0 Docker Setup Verification Script
# Tests Docker builds for AMD64 only to avoid QEMU issues

param(
    [switch]$SkipBuild,
    [switch]$Verbose
)

# Colors for output
$ErrorColor = "Red"
$SuccessColor = "Green"
$InfoColor = "Cyan"
$WarningColor = "Yellow"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Test-Command {
    param([string]$Command)
    try {
        Invoke-Expression "$Command --version" | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

Write-ColorOutput "?? MeteoMesh5.0 Docker Setup Verification" $InfoColor
Write-ColorOutput "===========================================" $InfoColor

# Check prerequisites
Write-ColorOutput "`n?? Checking Prerequisites..." $InfoColor

if (Test-Command "dotnet") {
    $dotnetVersion = dotnet --version
    Write-ColorOutput "? .NET CLI found: $dotnetVersion" $SuccessColor
} else {
    Write-ColorOutput "? .NET CLI not found. Please install .NET 9 SDK." $ErrorColor
    exit 1
}

if (Test-Command "docker") {
    $dockerVersion = docker --version
    Write-ColorOutput "? Docker found: $dockerVersion" $SuccessColor
} else {
    Write-ColorOutput "? Docker not found. Please install Docker Desktop." $ErrorColor
    exit 1
}

# Check Docker is running
try {
    docker ps | Out-Null
    Write-ColorOutput "? Docker daemon is running" $SuccessColor
} catch {
    Write-ColorOutput "? Docker daemon is not running. Please start Docker Desktop." $ErrorColor
    exit 1
}

# Set Docker to use AMD64 platform only
$env:DOCKER_DEFAULT_PLATFORM = "linux/amd64"
Write-ColorOutput "? Set Docker platform to AMD64 only" $SuccessColor

# Check solution file
if (Test-Path "MeteoMesh.MeteringStation.sln") {
    Write-ColorOutput "? Solution file found" $SuccessColor
} else {
    Write-ColorOutput "? Solution file not found. Please run from project root." $ErrorColor
    exit 1
}

# List available Dockerfiles
Write-ColorOutput "`n?? Available Dockerfiles:" $InfoColor
$dockerfiles = Get-ChildItem -Path "." -Recurse -Name "Dockerfile" | Where-Object { $_ -notlike "*/.git/*" }
foreach ($dockerfile in $dockerfiles) {
    if ($dockerfile -like "*Host*") {
        Write-ColorOutput "  ?? $dockerfile (Aspire AppHost - skipped in builds)" $WarningColor
    } else {
        Write-ColorOutput "  ?? $dockerfile" $InfoColor
    }
}

if (-not $SkipBuild) {
    Write-ColorOutput "`n?? Building .NET Solution..." $InfoColor
    
    # Clean and restore
    dotnet clean MeteoMesh.MeteringStation.sln --verbosity quiet
    dotnet restore MeteoMesh.MeteringStation.sln
    
    # Build solution
    $buildResult = dotnet build MeteoMesh.MeteringStation.sln --configuration Release --no-restore
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "? .NET build successful" $SuccessColor
    } else {
        Write-ColorOutput "?? .NET build had issues but continuing..." $WarningColor
    }
    
    Write-ColorOutput "`n?? Testing Docker Builds (AMD64 only)..." $InfoColor
    Write-ColorOutput "Note: Aspire AppHost (MeteoMesh5.Host) is excluded - not intended for containerization" $WarningColor
    
    $components = @(
        @{Name="CentralServer"; Dockerfile="MeteoMesh5.CentralServer/Dockerfile"; Tag="meteomesh5-central"},
        @{Name="LocalNode"; Dockerfile="MeteoMesh5.LocalNode/Dockerfile"; Tag="meteomesh5-localnode"},
        @{Name="HumidityStation"; Dockerfile="MeteoMesh5.HumidityStation/Dockerfile"; Tag="meteomesh5-humidity"},
        @{Name="LidarStation"; Dockerfile="MeteoMesh5.LidarStation/Dockerfile"; Tag="meteomesh5-lidar"},
        @{Name="PressureStation"; Dockerfile="MeteoMesh5.PressureStation/Dockerfile"; Tag="meteomesh5-pressure"},
        @{Name="TemperatureStation"; Dockerfile="MeteoMesh5.TemperatureStation/Dockerfile"; Tag="meteomesh5-temperature"}
    )
    
    $successCount = 0
    $totalCount = $components.Count
    
    foreach ($component in $components) {
        if (Test-Path $component.Dockerfile) {
            Write-ColorOutput "?? Building $($component.Name)..." $InfoColor
            
            $buildCmd = "docker build -f `"$($component.Dockerfile)`" -t `"$($component.Tag):latest`" . --platform linux/amd64"
            
            if ($Verbose) {
                Write-ColorOutput "Command: $buildCmd" $InfoColor
            }
            
            try {
                Invoke-Expression $buildCmd
                if ($LASTEXITCODE -eq 0) {
                    Write-ColorOutput "? $($component.Name) build successful" $SuccessColor
                    $successCount++
                } else {
                    Write-ColorOutput "? $($component.Name) build failed" $ErrorColor
                }
            } catch {
                Write-ColorOutput "? $($component.Name) build failed: $($_.Exception.Message)" $ErrorColor
            }
        } else {
            Write-ColorOutput "?? Dockerfile not found: $($component.Dockerfile)" $WarningColor
        }
    }
    
    Write-ColorOutput "`n?? Build Summary:" $InfoColor
    Write-ColorOutput "Successful builds: $successCount/$totalCount" $InfoColor
    Write-ColorOutput "Aspire AppHost: Available for local development (not containerized)" $InfoColor
    
    if ($successCount -eq $totalCount) {
        Write-ColorOutput "?? All Docker builds completed successfully!" $SuccessColor
    } elseif ($successCount -gt 0) {
        Write-ColorOutput "?? Some Docker builds failed. Check logs above." $WarningColor
    } else {
        Write-ColorOutput "? All Docker builds failed." $ErrorColor
        exit 1
    }
    
    # List built images
    Write-ColorOutput "`n?? Built Docker Images:" $InfoColor
    docker images | Select-String "meteomesh5" | ForEach-Object {
        Write-ColorOutput "  ?? $_" $InfoColor
    }
}

Write-ColorOutput "`n? Docker setup verification completed!" $SuccessColor
Write-ColorOutput "`n?? Next Steps:" $InfoColor
Write-ColorOutput "  - Test individual builds: docker build -f <dockerfile> -t <tag> . --platform linux/amd64" $InfoColor
Write-ColorOutput "  - Start Aspire orchestration: dotnet run --project MeteoMesh5.Host" $InfoColor
Write-ColorOutput "  - Run services: docker-compose up" $InfoColor
Write-ColorOutput "  - Monitor logs: docker-compose logs -f" $InfoColor