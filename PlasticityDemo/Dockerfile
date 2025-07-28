# Dockerfile for HB-NLP PlasticityDemo
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy project file
COPY PlasticityDemo.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
RUN dotnet build --no-restore --configuration Release
RUN dotnet publish --no-restore --configuration Release --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime

WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy design files
COPY HB-NLP-REV-001* ./
COPY open_in_plasticity.py ./

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Set entry point
ENTRYPOINT ["dotnet", "PlasticityDemo.dll"] 