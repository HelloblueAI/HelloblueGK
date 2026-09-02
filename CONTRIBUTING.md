# Contributing to HelloblueGK

You do not need to be an aerospace or certification expert to help. A **small, correct PR** is more useful than a large one.

This repository is the **Community Edition** ([Apache 2.0](LICENSE)). Read [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md) before sending production secrets, ITAR/export-controlled data, or formal certification evidence — those do not belong here.

## Your first hour

1. **Talk first (optional but helpful).** Comment `I'd like to work on this` on a [`good first issue`](https://github.com/HelloblueAI/HelloblueGK/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22) so two people do not do the same work. Or ask in [Discussions](https://github.com/HelloblueAI/HelloblueGK/discussions).
2. **Run the project locally** — [DEVELOPERS.md](DEVELOPERS.md) (about five minutes if you already have .NET 9).
3. **Change one thing.** One issue, one folder, one concern.
4. **Open a PR against `main`.** Use the PR template. Keep the description short: what you changed and how you checked it.

We review first-time PRs for **correctness and scope**, not for matching every internal hardening campaign. Docs and test-only PRs should not require you to resolve unrelated CodeQL or Bugbot threads on other files.

## Pick a safe first task

**Good first areas** (low merge conflict, easy to review):

| Area | Examples |
|------|----------|
| Docs newcomers actually use | `DEVELOPERS.md`, `Docs/Project/DEMO.md`, `Docs/README.md`, `API_DOCUMENTATION.md` |
| Broken links | Live docs only — **skip** `Docs/archive/historical/` |
| XML comments | One controller under `WebAPI/Controllers/` (not `Certification/`) |
| Isolated tests | New cases next to an existing test class in `Tests/Unit/WebAPI/` or `Tests/Unit/Core/` |
| Typos / clarity | README quick start, error messages a user would see |

**Wait until later** (these files change often and have merge-blocking review rules):

- `Certification/**` and `WebAPI/Controllers/Certification/**`
- `Core/RateLimiting*.cs` and related middleware
- Auth / registration / SSO paths
- Anything described as “fail-closed,” “Level A,” or “SoD”

If your idea touches those, open a Discussion or issue first. We will either scope a safe slice or take it ourselves.

**Never commit:** API keys, connection strings, `.env` files, customer models, or export-controlled material.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- Git
- Docker is optional

## Local loop

```bash
git clone https://github.com/YOUR_USERNAME/HelloblueGK.git
cd HelloblueGK
git checkout -b docs/fix-broken-docs-readme-link

dotnet restore HelloblueGK.sln
dotnet build HelloblueGK.sln
dotnet test Tests/HelloblueGK.Tests.csproj
```

Run only the tests you touched if the full suite is slow or memory-heavy:

```bash
dotnet test Tests/HelloblueGK.Tests.csproj --filter "FullyQualifiedName~Health"
```

API: `cd WebAPI && dotnet run` → http://localhost:5000/swagger  
Health (no login): http://localhost:5000/Health

The hosted site is a **reference deploy**, not an open sandbox. Develop against your local API.

## Pull request expectations

We will merge a first PR that is:

- **Small** — prefer under ~200 lines unless the issue says otherwise
- **On `main`**, rebased or merged with `main` if CI complains about conflicts
- **Tested** — `dotnet build` plus the tests that cover your change (full suite when you can)
- **Secret-free**
- **In Community Edition scope**

Commit messages can be simple: `docs: fix broken link in Docs/README.md`.

If CI fails on something you did not touch, say so in the PR. We will help.

Maintainers: do not ask first-time contributors to absorb daily certification PRs or to resolve bot comments on files they never edited. Fix or dismiss those on their behalf.

## Code style

- Match the file you are in (see `.editorconfig`)
- Meaningful names; small methods
- XML docs on **new public** APIs
- Tests for new behavior in `Tests/`

## Reporting bugs and ideas

| Kind | Where |
|------|--------|
| Question / “is this a good first PR?” | [Discussions](https://github.com/HelloblueAI/HelloblueGK/discussions) |
| Bug | [Bug report](https://github.com/HelloblueAI/HelloblueGK/issues/new?template=bug_report.yml) |
| Docs fix | [Documentation](https://github.com/HelloblueAI/HelloblueGK/issues/new?template=documentation.yml) |
| Feature | [Feature request](https://github.com/HelloblueAI/HelloblueGK/issues/new?template=feature_request.yml) |
| Security | [SECURITY.md](SECURITY.md) — **not** a public issue |

## Code of Conduct

[Contributor Covenant](CODE_OF_CONDUCT.md). Report concerns to **conduct@helloblue.ai**.

By contributing, you license your work under [Apache 2.0](LICENSE).

Welcome — we want your first PR to land.
