# GitHub configuration

This folder contains **automation and templates** for contributors.

## For contributors

| File | Purpose |
|------|---------|
| [../OPEN_SOURCE_SCOPE.md](../OPEN_SOURCE_SCOPE.md) | Community vs hosted vs enterprise tiers |
| [../CONTRIBUTING.md](../CONTRIBUTING.md) | How to contribute |
| [../DEVELOPERS.md](../DEVELOPERS.md) | Local setup |
| [ISSUE_TEMPLATE/](ISSUE_TEMPLATE/) | Bug and feature request forms |
| [pull_request_template.md](pull_request_template.md) | PR checklist |
| [workflows/](workflows/) | CI/CD (runs on every PR) |
| [dependabot.yml](dependabot.yml) | Automated dependency updates |
| [CODEOWNERS](CODEOWNERS) | Code review routing |

## Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | Push to `main`, `develop`, `production-deployment`, `fix/**`; PRs to the first three; manual dispatch | Build, test, coverage floors, and the DO-178C Level A certification gate |
| `release.yml` | Push of a `v*` tag | Release automation |
| `security-audit.yml` | Daily at 06:00 UTC; manual dispatch | Vulnerable-package audit and security checks |

`scripts/fail-on-vulnerable-packages.sh` is called by both `ci.yml` and `security-audit.yml`; it
fails the build when NuGet reports a vulnerable direct or transitive package.

### What CI enforces on a pull request

Beyond build and test, `ci.yml` runs `Tools/CertificationGate`, which fails the build if per-directory
coverage drops below [`coverage-floors.json`](../Certification/Artifacts/coverage-floors.json) or if the
declared certification boundary in
[`certification-boundary.json`](../Certification/Artifacts/certification-boundary.json) regresses. Both
files are the authoritative source for those thresholds.

## Operational runbooks

Internal CI/CD setup notes, branch-protection administration, and other operational runbooks are
**not published here**. See [OPEN_SOURCE_SCOPE.md](../OPEN_SOURCE_SCOPE.md) for what is in and out of
scope for this repository.
