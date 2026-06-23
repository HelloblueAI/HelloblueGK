# GitHub configuration

This folder contains **automation and templates** for contributors and maintainers.

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

## For maintainers

Internal CI/CD setup notes and branch-protection runbooks were moved to:

**[Docs/internal/github-runbooks/](../Docs/internal/github-runbooks/)**

Examples: Codecov setup, branch protection, CI troubleshooting.

## Workflows

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `ci.yml` | Push / PR | Build, test, coverage |
| `release.yml` | Release tag | Release automation |
| `security-audit.yml` | Schedule | Security checks |
