# README.md

[![CI/CD Pipeline](https://github.com/HelloblueAI/HelloblueGK/actions/workflows/ci.yml/badge.svg)](https://github.com/HelloblueAI/HelloblueGK/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=.net&logoColor=white)](https://dotnet.microsoft.com/)
[![Line Coverage](https://img.shields.io/badge/line%20coverage-61.1%25-yellow)](Certification/Artifacts/coverage-floors.json)
[![Branch Coverage](https://img.shields.io/badge/branch%20coverage-62.0%25-yellow)](Certification/Artifacts/coverage-floors.json)
[![Tests](https://img.shields.io/badge/tests-1336%20passing-success)](https://github.com/HelloblueAI/HelloblueGK/actions/workflows/ci.yml)
[![Verification Scope](https://img.shields.io/badge/verification%20scope-public%20API-blue)](Certification/Artifacts/verification-scope.json)

## HelloblueGK

HelloblueGK is a .NET 9.0 solution that provides a WebAPI surface for the Helloblue knowledge-graph services. The repository contains the API project, shared domain models, integration tests, and the CI/CD pipeline configuration used to build, test, and publish the service.

## Repository Layout

| Path | Description |
| --- | --- |
| `WebAPI/` | ASP.NET Core WebAPI project exposing the public HTTP endpoints. |
| `WebAPI/Controllers/` | Public controller actions. Each action carries an XML doc summary so that Swagger/OpenAPI output is self-describing. |
| `Domain/` | Shared domain models and contracts consumed by the API. |
| `Tests/` | Unit and integration tests covering the public API surface. |
| `Certification/` | Coverage floors, verification scope, and other certification artifacts. |
| `.github/workflows/` | CI/CD pipeline definitions. |

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker (optional, for containerized runs)

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet
