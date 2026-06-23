# Open Source Scope

HelloblueGK follows the **tiered openness model** used by most enterprise and aerospace-adjacent software companies: a **public community edition** for integration and learning, plus **commercial layers** for production operations, support, and compliance evidence.

This document defines what this repository is — and what it is **not**.

---

## Product tiers

| Tier | What it is | Where it lives |
|------|------------|----------------|
| **Community Edition** | Reference implementation, APIs, docs, tests, sample workflows | **This repository** (Apache 2.0) |
| **Hosted Platform** | Managed deployment, auth, uptime, operations | [hellobluegk.onrender.com](https://hellobluegk.onrender.com) (reference demo; not open signup) |
| **Enterprise / Certification** | Production support, formal compliance packages, custom deployments, SLAs | **Not in this repo** — contact [Helloblue](https://helloblue.com) |

This is the same pattern used by companies that publish SDKs and reference stacks in the open while keeping production certification assets, proprietary models, and managed services commercial.

---

## What is in this public repository

Intended for **research, integration, education, and contribution**:

- Web API source and OpenAPI/Swagger surface
- Core simulation, physics, and AI **framework** code
- Certification **workflow APIs** (requirements traceability, problem reports, configuration management, test coverage tracking, code reviews) as **reference tooling**
- Docker, CI/CD, tests, and deployment guides
- Sample engine designs and Plasticity demo integration

You may fork, modify, and redistribute under [Apache 2.0](LICENSE).

---

## What is NOT in this public repository

The following are **out of scope** for this open-source release and may be proprietary or contract-bound:

- Formal **DO-178C / NASA NPR 7150.2 certification evidence** (audits, signed artifacts, qualification packages)
- Production **flight-software baselines** approved for human-rated or mission-critical use
- Proprietary solver configurations, tuned models, or customer-specific engine data
- **Export-controlled** technical data subject to ITAR or other export regulations
- Hosted production secrets, tenant data, SSO configuration, and operational runbooks
- Enterprise support, SLAs, and professional services deliverables

If you need production certification or enterprise deployment, contact Helloblue directly — do not assume this repository alone satisfies regulatory or mission requirements.

---

## What we do NOT warrant

Software in this repository is provided **"AS IS"** under Apache 2.0. Specifically:

| Topic | Community Edition (this repo) |
|-------|------------------------------|
| **Flight readiness** | **Not warranted.** Not certified for flight, human-rated, or mission-critical operations without your own qualification program. |
| **Simulation accuracy** | Results are **indicative** unless you validate against your own test data and requirements. |
| **Certification APIs** | Provide **workflow tooling** — they do **not** constitute certification by Helloblue, NASA, FAA, or any agency. |
| **Security** | Follow [SECURITY.md](SECURITY.md). You are responsible for securing your own deployment. |
| **Compliance** | You are responsible for export control, ITAR, and all applicable laws in your jurisdiction and use case. |

---

## Certification and compliance language

Documentation in this repo may reference **DO-178C**, **NASA NPR 7150.2**, or **ITAR** as **design targets** or **process orientation**. That means:

- We built APIs and structures that **support** aerospace software lifecycle practices.
- We do **not** claim that downloading or deploying this repository **is** certified flight software.
- Achieving formal certification requires your organization’s own plans, evidence, audits, and authority approval — typically with commercial support and controlled baselines **outside** this public tree.

See [Certification/README.md](Certification/README.md) for what the reference certification modules do locally.

---

## Export control and sensitive use

Aerospace and launch-vehicle technology may be subject to **export control laws** (including U.S. ITAR/EAR). By using this software you agree that:

- You will comply with all applicable export and trade regulations.
- You will not use this repository to distribute controlled technical data without proper authorization.
- You will not commit classified, export-controlled, or customer-confidential material to this public project.

Report export-control concerns via [security@helloblue.ai](mailto:security@helloblue.ai) (see [SECURITY.md](SECURITY.md)).

---

## How to use this project responsibly

| Use case | Recommended approach |
|----------|---------------------|
| Learn aerospace API design | Clone, run locally, read [ARCHITECTURE.md](ARCHITECTURE.md) |
| Build a product integration | Use Community Edition + your own validation; consider hosted/enterprise for production |
| Academic / research | Appropriate — cite the project and validate all simulation claims independently |
| Human-rated or mission-critical flight | **Not appropriate from this repo alone** — engage formal qualification and commercial support |
| Defense or export-controlled programs | **Legal review required** before use or redistribution |

---

## Commercial and enterprise

For hosted production, formal certification support, private modules, or partnership:

- **Website:** [helloblue.com](https://helloblue.com)
- **Security:** [security@helloblue.ai](mailto:security@helloblue.ai)
- **Conduct:** [conduct@helloblue.ai](mailto:conduct@helloblue.ai)

---

## Contributing within scope

Contributions welcome for Community Edition: tests, docs, API improvements, and reference certification workflows.

**Do not contribute:** secrets, production credentials, export-controlled data, or customer-specific proprietary models.

See [CONTRIBUTING.md](CONTRIBUTING.md).

---

*Helloblue, Inc. — HB-NLP Research Lab*  
*This document may be updated as product tiers evolve. When in doubt, this file overrides informal marketing language elsewhere in the repository.*
