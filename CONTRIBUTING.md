# Contributing to GridShare

Thanks for helping improve GridShare. This project is a simulation-first micro-grid marketplace, so contributions should keep the core market logic decoupled from adapters such as the CLI, API, MongoDB, MQTT, and browser dashboard.

## Development Setup

Requirements:

- .NET 8 SDK or newer
- Docker, only if you want MongoDB persistence
- Node.js/npm, only if you want to run optional browser tooling such as Playwright screenshots

Build and test:

```powershell
dotnet build GridShare.slnx
dotnet test GridShare.slnx
```

Run the CLI:

```powershell
dotnet run --project GridShare.Cli -- --ticks 96 --csv telemetry/gridshare.csv --jsonl telemetry/gridshare.jsonl
```

Run the API dashboard:

```powershell
dotnet run --project GridShare.Api -- --urls http://localhost:5077
```

## Architecture Rules

- Keep `GridShare.Domain` free of infrastructure dependencies.
- Put market behavior and ports in `GridShare.Application`.
- Put replaceable adapters in `GridShare.Infrastructure`, `GridShare.Simulation`, `GridShare.Cli`, or `GridShare.Api`.
- Keep the `MarketOperator` dependent on `EnergySnapshot` data, not on simulated houses or transport-specific APIs.
- Treat `TradeBlock` as append-only. Do not mutate existing ledger blocks.
- Preserve location-aware market pricing and base-ledger settlement semantics.

## Pull Request Checklist

Before opening a pull request:

- Run `dotnet build GridShare.slnx`.
- Run `dotnet test GridShare.slnx`.
- Add or update tests for matching, pricing, ledger, telemetry, or blackout behavior when touched.
- Update `README.md`, `ARCHITECTURE.md`, or `TELEMETRY.md` if public behavior changes.
- Do not commit `bin/`, `obj/`, generated telemetry files, logs, or local secrets.

## Coding Style

- Prefer clear records and small services over deep inheritance.
- Use deterministic simulation inputs in tests.
- Keep comments short and focused on non-obvious behavior.
- Prefer explicit units in names, such as `Kwh`, `Kw`, `Usd`, or `Kg`.

## Reporting Bugs

Please include:

- What command or endpoint you used.
- The expected behavior.
- The actual behavior.
- Any relevant telemetry row, log line, screenshot, or failing test output.
