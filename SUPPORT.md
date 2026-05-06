# Support

GridShare is a prototype micro-grid marketplace and simulation engine.

## Getting Help

Use GitHub issues for:

- Bugs
- Feature requests
- Documentation improvements
- Questions about running the CLI, API, dashboard, MongoDB, telemetry, or tests

For security-sensitive issues, follow `SECURITY.md` instead of opening a public issue.

## Useful Commands

Build:

```powershell
dotnet build GridShare.slnx
```

Test:

```powershell
dotnet test GridShare.slnx
```

Run dashboard:

```powershell
dotnet run --project GridShare.Api -- --urls http://localhost:5077
```

Run CLI telemetry export:

```powershell
dotnet run --project GridShare.Cli -- --ticks 96 --csv telemetry/gridshare.csv --jsonl telemetry/gridshare.jsonl
```

## Known Limits

- Tariff and FX data are seeded simulation references, not live billing-grade rates.
- MongoDB persistence is optional and disabled unless configured.
- The dashboard is an operations visualization, not a safety-critical grid control surface.
