# CodeQL Default Setup Guidance

## Current Status

**Status:** GitHub CodeQL default setup is enabled for this repository.
**Latest Review:** June 19, 2026
**Impact:** Repository-owned advanced CodeQL workflows must not upload SARIF while default setup is enabled.

GitHub rejects CodeQL SARIF uploads from advanced workflow configurations when default setup is enabled. The failing check reported:

```text
Code Scanning could not process the submitted SARIF file:
CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled
```

## Resolution

The standalone `.github/workflows/codeql.yml` workflow has been removed. CodeQL coverage should come from GitHub's default setup, which is managed in the repository security settings instead of in this repository's workflow files.

Do not re-enable or recreate `.github/workflows/codeql.yml` unless repository administrators first disable CodeQL default setup.

## Current Security Scanning

- **CodeQL:** GitHub default setup in repository security settings
- **Package vulnerabilities:** `.github/workflows/security-audit.yml` runs daily and can also be triggered manually
- **Pull request CI:** `.github/workflows/ci.yml` runs build, tests, code quality, and NuGet vulnerability checks

## Verification

Use GitHub's Code scanning UI or read-only GitHub CLI commands to confirm default setup results:

```bash
gh api /repos/HelloblueAI/HelloblueGK/code-scanning/alerts
gh run list --workflow=security-audit.yml --limit 5
```

If CodeQL needs custom query packs or custom build behavior later, switch from default setup to advanced setup in GitHub security settings first, then add a single advanced CodeQL workflow.
