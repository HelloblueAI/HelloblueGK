# Developer Guide

Get from zero to a running API and passing tests in about five minutes.

This repository is the **Community Edition** — see [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md) for what is public vs commercial.

**Naming:** Clone into a folder named `HelloblueGK`. This project is **not** [LEAP 71's PicoGK](https://github.com/leap71/PicoGK) and is not a fork of that repository. See the [Project identity](README.md#project-identity) section in the README.

If an older clone directory is still named `PicoGK`, rename it when convenient: `mv PicoGK HelloblueGK` (paths in systemd examples use `/opt/hellobluegk`).

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0.x | Required |
| Git | any recent | Required |
| Docker | optional | For containerized runs — see [Docs/Project/DEMO.md](Docs/Project/DEMO.md) |

Install .NET on Linux if needed: [Docs/Technical/INSTALL_DOTNET.md](Docs/Technical/INSTALL_DOTNET.md)

## Clone and build

```bash
git clone https://github.com/HelloblueAI/HelloblueGK.git
cd HelloblueGK

dotnet restore HelloblueGK.sln
dotnet build HelloblueGK.sln
dotnet test Tests/HelloblueGK.Tests.csproj
```

Open `HelloblueGK.sln` in Visual Studio, Rider, or VS Code with the C# extension.

## Run the Web API

```bash
cd WebAPI
dotnet run
```

| URL | Description |
|-----|-------------|
| http://localhost:5000/swagger | Interactive API docs |
| http://localhost:5000/Health | Health check (no auth) |

For local auth/registration, copy `appsettings.Development.json.example` to `appsettings.Development.json` and adjust settings as needed.

## Project layout

```
HelloblueGK/
├── HelloblueGK.sln          # Solution — open this in your IDE
├── HelloblueGK.csproj       # Core library (physics, AI, certification helpers)
├── WebAPI/                  # ASP.NET Core REST API (main entry point for contributors)
├── Tests/                   # xUnit tests — run before every PR
├── Core/                    # Engine kernel, telemetry, health, control systems
├── Physics/                 # CFD, thermal, structural solvers
├── AI/                      # Optimization and ML engines
├── Certification/           # Flight-software certification subsystems
├── PlasticityDemo/          # Optional 3D demo (requires Plasticity app)
├── Docker/                  # Dockerfiles
└── Docs/                    # Extended documentation
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for how components connect.

## Common tasks

### Run one test class

```bash
dotnet test Tests/HelloblueGK.Tests.csproj --filter "FullyQualifiedName~DigitalTwinEngineTests"
```

### Build Release (matches CI)

```bash
dotnet build HelloblueGK.sln --configuration Release
dotnet test Tests/HelloblueGK.Tests.csproj --configuration Release --no-build
```

### Run with Docker

```bash
docker build -t hellobluegk:latest -f Docker/Dockerfile .
docker run -p 8080:8080 -e Jwt__Key="dev-key-at-least-32-characters-long" hellobluegk:latest
```

### Plasticity demo (optional)

The Plasticity 3D workflow is **optional**. You do not need it for API, tests, or most contributions.

```bash
cd PlasticityDemo
dotnet run
```

## Environment variables (local)

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Development` for local dev |
| `Jwt__Key` | JWT signing key (32+ chars) — required in production |
| `ConnectionStrings__DefaultConnection` | Database — SQLite by default in dev |

Never commit real secrets. Use `appsettings.Development.json` (gitignored) or environment variables.

## CI before you push

CI runs the same core steps:

```bash
dotnet restore
dotnet build --configuration Release
dotnet test Tests/HelloblueGK.Tests.csproj --configuration Release --no-build
```

See [Docs/internal/github-runbooks/TEST_LOCALLY.md](Docs/internal/github-runbooks/TEST_LOCALLY.md) for the full CI simulation.

## Where to contribute

| If you want to… | Start here |
|-----------------|------------|
| Fix API endpoints | `WebAPI/Controllers/` |
| Add physics logic | `Physics/`, `Core/` |
| Improve tests | `Tests/Unit/` |
| Certification features | `Certification/`, `WebAPI/Controllers/Certification/` |
| Documentation | `Docs/`, `README.md`, `API_DOCUMENTATION.md` |

## Getting help

- [CONTRIBUTING.md](CONTRIBUTING.md) — PR process and code style
- [GitHub Discussions](https://github.com/HelloblueAI/HelloblueGK/discussions)
- [good first issues](https://github.com/HelloblueAI/HelloblueGK/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22)
