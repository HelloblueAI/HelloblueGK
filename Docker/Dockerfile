# Primary production image: HelloblueGK WebAPI
# This is what runs in production (Render/Railway) and is validated by CI.
# Build from repo root: docker build -f Docker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY HelloblueGK.csproj ./
COPY WebAPI/HelloblueGK.WebAPI.csproj ./WebAPI/

RUN dotnet restore WebAPI/HelloblueGK.WebAPI.csproj

COPY . .

WORKDIR /src/WebAPI
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Default port when PORT is not set (e.g. local docker run); Render/Railway set PORT at runtime
ENV PORT=8080
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD ["/bin/sh", "-c", "port=${PORT:-8080} && curl -f http://localhost:${port}/Health || exit 1"]

ENTRYPOINT ["dotnet", "HelloblueGK.WebAPI.dll"]
