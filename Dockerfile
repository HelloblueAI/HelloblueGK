# Multi-stage Dockerfile for HelloblueGK Aerospace Engine API
# Production-ready with security, performance, and enterprise features

# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["HelloblueGK.csproj", "./"]
COPY ["Program.cs", "./"]
COPY ["Core/", "./Core/"]
COPY ["Physics/", "./Physics/"]
COPY ["AI/", "./AI/"]
COPY ["Aerospace/", "./Aerospace/"]
COPY ["Models/", "./Models/"]
COPY ["Visualization/", "./Visualization/"]

# Restore dependencies
RUN dotnet restore "HelloblueGK.csproj"

# Build the application
RUN dotnet build "HelloblueGK.csproj" -c Release -o /app/build

# Stage 2: Publish stage
FROM build AS publish
RUN dotnet publish "HelloblueGK.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r helloblue && useradd -r -g helloblue -s /bin/bash helloblue

# Create necessary directories
RUN mkdir -p /app/logs /app/data /app/cache && \
    chown -R helloblue:helloblue /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Switch to non-root user
USER helloblue

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set entry point
ENTRYPOINT ["dotnet", "HelloblueGK.dll"] 