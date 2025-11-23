# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY *.csproj ./
COPY ../HelloblueGK.csproj ../HelloblueGK.csproj
COPY ../*.csproj ../

# Restore dependencies
RUN dotnet restore HelloblueGK.WebAPI.csproj
RUN dotnet restore ../HelloblueGK.csproj

# Copy everything else
COPY . .
COPY .. ../

# Build and publish
WORKDIR /src
RUN dotnet publish HelloblueGK.WebAPI.csproj -c Release -o /app/publish

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

