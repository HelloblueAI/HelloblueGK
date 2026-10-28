{
  "context": "HelloblueGK — Aerospace Engine Simulation Platform",
  "description": "Fixes the issue of not including the verification scope in the `Docs/VERIFICATION_SCOPE.md` file.",
  "fixes": "docs: list the RL10 validation points in Docs/VERIFICATION_SCOPE.md",
  "details": "## Context\n\n`Docs/VERIFICATION_SCOPE.md` is the page that tells a reviewer what is verified. It says the ideal-nozzle solver is validated against Merlin 1D (sea level), Raptor (sea level), and RS-25 (sea level and vacuum) — in the \"What is verified\" table (line ~29) and again in \"One solver now computes from its inputs\" (lines ~112–113).\n\nThe test data has two more points: `rl10a-4-2-vacuum` and `rl10a-4-2-10bar`.\n\nFile (README.md):\n[![CI/CD Pipeline](https://github.com/HelloblueAI/HelloblueGK/actions/workflows/ci.yml/badge.svg)](https://github.com/HelloblueAI/HelloblueGK/actions/workflows/ci.yml)\n[![License](https://img.shields.io/badge/license-Apache%202.
