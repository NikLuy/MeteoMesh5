# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file and project files first for better layer caching
COPY MeteoMesh5.sln .
COPY MeteoMesh5.LocalNode/MeteoMesh5.LocalNode.csproj MeteoMesh5.LocalNode/
COPY MeteoMesh5.Shared/MeteoMesh5.Shared.csproj MeteoMesh5.Shared/

# Restore dependencies
RUN dotnet restore MeteoMesh5.LocalNode/MeteoMesh5.LocalNode.csproj

# Copy source code
COPY MeteoMesh5.LocalNode/ MeteoMesh5.LocalNode/
COPY MeteoMesh5.Shared/ MeteoMesh5.Shared/

# Build application
RUN dotnet build MeteoMesh5.LocalNode/MeteoMesh5.LocalNode.csproj -c Release --no-restore
RUN dotnet publish MeteoMesh5.LocalNode/MeteoMesh5.LocalNode.csproj -c Release --no-build -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install curl for health checks
RUN apk add --no-cache curl

# Create non-root user
RUN addgroup -g 1000 meteomesh && \
    adduser -D -s /bin/sh -u 1000 -G meteomesh meteomesh

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R meteomesh:meteomesh /app

# Switch to non-root user
USER meteomesh

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose ports
EXPOSE 8080 8443 7200

ENTRYPOINT ["dotnet", "MeteoMesh5.LocalNode.dll"]