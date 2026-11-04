# DEVELOPERS.md

## Project Layout

```
HelloblueGK/
├── src/
│   ├── HelloblueGK.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   ├── Models/
│   │   └── Program.cs
│   ├── HelloblueGK.Core/         # Domain logic & interfaces
│   └── HelloblueGK.Infrastructure/ # Data access, external services
├── tests/
│   ├── HelloblueGK.Api.Tests/
│   ├── HelloblueGK.Core.Tests/
│   └── HelloblueGK.Infrastructure.Tests/
├── Certification/
│   └── Artifacts/
├── docs/
├── .github/workflows/
├── DEVELOPERS.md
└── README.md
```

## Web API Controller Map

| Route / Controller | File | Purpose |
|---|---|---|
| `GET /health` | `src/HelloblueGK.Api/Controllers/HealthController.cs` | Liveness/readiness probe for load balancers and CI smoke tests. |
| `GET /engines` | `src/HelloblueGK.Api/Controllers/EnginesController.cs` | List available inference engines and their runtime status. |
| `POST /engines/{id}/run` | `src/HelloblueGK.Api/Controllers/EnginesController.cs` | Submit an inference job to a specific engine. |
| `GET /certification` | `src/HelloblueGK.Api/Controllers/CertificationController.cs` | Retrieve current certification status and coverage floors. |
| `POST /certification/verify` | `src/HelloblueGK.Api/Controllers/CertificationController.cs` | Trigger a certification verification run. |

> **Tip:** Use the route column to quickly locate the controller file when debugging or extending an endpoint.
