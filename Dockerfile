# Multi-stage build for HB-NLP Revolutionary Engine Design Platform
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY *.csproj ./
COPY PlasticityDemo/*.csproj ./PlasticityDemo/

# Restore dependencies
RUN dotnet restore
RUN dotnet restore PlasticityDemo/

# Copy source code
COPY . .

# Build applications
RUN dotnet build --no-restore --configuration Release
RUN dotnet build PlasticityDemo/ --no-restore --configuration Release

# Publish applications
RUN dotnet publish --no-restore --configuration Release --output /app/publish
RUN dotnet publish PlasticityDemo/ --no-restore --configuration Release --output /app/plasticity-demo

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install Python and dependencies
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    wget \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy published applications
COPY --from=build /app/publish .
COPY --from=build /app/plasticity-demo ./plasticity-demo

# Copy design files and scripts
COPY HB-NLP-REV-001* ./
COPY Scripts/Integration/open_in_plasticity.py ./open_in_plasticity.py
COPY README.md ./

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Expose ports
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Default command
CMD ["dotnet", "HelloblueGK.dll"] 