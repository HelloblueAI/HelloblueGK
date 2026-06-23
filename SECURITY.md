# Security Policy

HelloblueGK is a **public open-source project** (Apache 2.0). Production secrets and credentials are never stored in this repository.

See also [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md).

## Export control

This project may be used in aerospace contexts subject to export control laws (e.g. U.S. ITAR/EAR). **Do not** commit export-controlled or classified material to this public repository. Users are responsible for compliance in their jurisdiction. See [OPEN_SOURCE_SCOPE.md](OPEN_SOURCE_SCOPE.md).

## Supported versions

| Version | Supported |
|---------|-----------|
| `main` / latest release | ✅ |

## Reporting a vulnerability

**Please do not open public GitHub issues for security vulnerabilities.**

### Preferred: GitHub private vulnerability reporting

1. Open the repository **Security** tab → **Report a vulnerability**
2. Submit a private security advisory report (enabled for this public repo)

### Alternative contact

If GitHub reporting is unavailable, email **security@helloblue.ai** with:
- Description of the issue
- Steps to reproduce
- Impact assessment (if known)

We aim to acknowledge within **72 hours** and coordinate disclosure after a fix is available.

## Secrets and deployments

- Never commit `.env`, connection strings, JWT keys, or Azure client secrets.
- Use platform environment variables (Render, etc.) for production.
- Rotate credentials if they are ever exposed.

## Hosted demo

The public demo at `https://hellobluegk.onrender.com` is a reference deployment with authentication enabled. It is not an open registration sandbox.
