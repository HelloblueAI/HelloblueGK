# Contributing to HelloblueGK

Thank you for your interest in contributing! This repository is the **Community Edition** of HelloblueGK — **Apache 2.0** open source for integration, learning, and reference implementations.

**Read first:** [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md) — what belongs in this public repo and what does not.

## Quick links

| Resource | Purpose |
|----------|---------|
| [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md) | Community vs hosted vs enterprise tiers |
| [DEVELOPERS.md](DEVELOPERS.md) | Clone → build → test in 5 minutes |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How the codebase is organized |
| [Docs/Project/DEMO.md](Docs/Project/DEMO.md) | Run the Web API locally |
| [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) | Community standards |
| [SECURITY.md](SECURITY.md) | Report vulnerabilities privately |

## Good first contributions

Look for issues labeled [`good first issue`](https://github.com/HelloblueAI/HelloblueGK/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) or [`help wanted`](https://github.com/HelloblueAI/HelloblueGK/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22).

Great starter tasks:

- Fix broken links or typos in docs
- Add unit tests for uncovered code paths
- Improve XML doc comments on public APIs
- Clarify setup steps in `DEVELOPERS.md` or `DEMO.md`

**Do not contribute:** API keys, connection strings, export-controlled data, customer proprietary models, or production certification artifacts. See [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md).

## Development workflow

1. **Fork** the repository on GitHub
2. **Clone** your fork and create a branch:
   ```bash
   git clone https://github.com/YOUR_USERNAME/HelloblueGK.git
   cd HelloblueGK
   git checkout -b feature/short-description
   ```
3. **Set up** — see [DEVELOPERS.md](DEVELOPERS.md)
4. **Make changes** — follow code style below
5. **Test** locally:
   ```bash
   dotnet build
   dotnet test Tests/HelloblueGK.Tests.csproj
   ```
6. **Commit** with clear messages (e.g. `fix: correct DEMO.md path in CONTRIBUTING`)
7. **Push** and open a **Pull Request** against `main`
8. Wait for **CI** to pass and address review feedback

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- Git
- Docker (optional — for containerized runs)

## Getting started

```bash
git clone https://github.com/HelloblueAI/HelloblueGK.git
cd HelloblueGK

dotnet restore HelloblueGK.sln
dotnet build HelloblueGK.sln

dotnet test Tests/HelloblueGK.Tests.csproj

cd WebAPI
dotnet run
# Swagger: http://localhost:5000/swagger
```

For detailed setup, environment variables, and project layout, see [DEVELOPERS.md](DEVELOPERS.md).

## Code style

- Follow standard C# conventions and existing patterns in the file you edit
- Use meaningful names; keep methods focused
- Add XML documentation for new **public** APIs
- Match indentation and formatting (see `.editorconfig`)
- Write unit tests for new behavior in `Tests/`

## Testing

| Command | Purpose |
|---------|---------|
| `dotnet test Tests/HelloblueGK.Tests.csproj` | All unit/integration tests |
| `dotnet test --filter Category=Integration` | Integration tests only |
| `dotnet test --filter Category=Performance` | Performance benchmarks |

CI runs on every pull request — see [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Pull request checklist

- [ ] Branch is up to date with `main`
- [ ] `dotnet build` and `dotnet test` pass locally
- [ ] Documentation updated if behavior or setup changed
- [ ] No secrets, credentials, export-controlled data, or `.env` files committed
- [ ] PR description explains **what** and **why**
- [ ] Changes are appropriate for **Community Edition** scope ([OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md))

## Reporting issues

Use the [bug report](https://github.com/HelloblueAI/HelloblueGK/issues/new?template=bug_report.yml) or [feature request](https://github.com/HelloblueAI/HelloblueGK/issues/new?template=feature_request.yml) templates.

Include:

- Clear title and description
- Steps to reproduce (for bugs)
- .NET version and OS
- Expected vs actual behavior

**Security issues:** Do not open public issues. See [SECURITY.md](SECURITY.md).

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

Report conduct concerns to **conduct@helloblue.ai**.

## License

By contributing, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).

## Questions?

- [GitHub Discussions](https://github.com/HelloblueAI/HelloblueGK/discussions) — questions and ideas
- [GitHub Issues](https://github.com/HelloblueAI/HelloblueGK/issues) — bugs and feature requests

Thank you for helping make HelloblueGK better!
