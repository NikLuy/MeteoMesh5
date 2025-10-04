# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file and project files
COPY MeteoMesh.MeteringStation.sln .
COPY MeteoMesh5.LidarStation/MeteoMesh5.LidarStation.csproj MeteoMesh5.LidarStation/
COPY MeteoMesh5.Shared/MeteoMesh5.Shared.csproj MeteoMesh5.Shared/

# Restore dependencies
RUN dotnet restore MeteoMesh5.LidarStation/MeteoMesh5.LidarStation.csproj

# Copy source code
COPY MeteoMesh5.LidarStation/ MeteoMesh5.LidarStation/
COPY MeteoMesh5.Shared/ MeteoMesh5.Shared/

# Build application
RUN dotnet build MeteoMesh5.LidarStation/MeteoMesh5.LidarStation.csproj -c Release --no-restore
RUN dotnet publish MeteoMesh5.LidarStation/MeteoMesh5.LidarStation.csproj -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -g 1000 meteomesh && \
    adduser -D -s /bin/sh -u 1000 -G meteomesh meteomesh

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R meteomesh:meteomesh /app

# Switch to non-root user
USER meteomesh

# Expose metrics port
EXPOSE 8080

ENTRYPOINT ["dotnet", "MeteoMesh5.LidarStation.dll"]