# Build stage
# NOTE: This Dockerfile assumes the build context is at the repository root
# Build with: docker build -f WebAPI/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for dependency restoration
COPY HelloblueGK.csproj ./
COPY WebAPI/HelloblueGK.WebAPI.csproj ./WebAPI/

# Restore dependencies (restore main project first, then WebAPI)
RUN dotnet restore HelloblueGK.csproj
RUN dotnet restore WebAPI/HelloblueGK.WebAPI.csproj

# Copy all source files
COPY . .

# Build and publish WebAPI (includes main project as dependency)
WORKDIR /src/WebAPI
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/Health || exit 1

# Start the app
ENTRYPOINT ["dotnet", "HelloblueGK.WebAPI.dll"]

