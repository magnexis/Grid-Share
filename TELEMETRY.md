# Telemetry

GridShare telemetry is designed for operations monitoring, data science, replay, and audit inspection.

## Export Command

```powershell
dotnet run --project GridShare.Cli -- --ticks 96 --csv telemetry/gridshare.csv --jsonl telemetry/gridshare.jsonl
```

## CSV Files

When you pass `--csv telemetry/gridshare.csv`, GridShare writes:

- `telemetry/gridshare.csv`: node-level time series, one row per house per tick.
- `telemetry/gridshare.market.csv`: one market aggregate row per tick.
- `telemetry/gridshare.trades.csv`: one row per recorded trade block.
- `telemetry/gridshare.accounts.csv`: one settlement account snapshot per node per tick.
- `telemetry/gridshare.anomalies.csv`: anomaly events.

## JSONL

When you pass `--jsonl telemetry/gridshare.jsonl`, each line is a full tick envelope with:

- `schemaVersion`
- `runId`
- `sequence`
- `generatedAt`
- aggregate metrics
- market snapshot
- node snapshots
- trades
- open orders
- accounts
- anomalies
- forecast

JSONL is best for replay and data pipelines.

## Schema Compatibility

If an existing CSV file has an old header, GridShare archives it as:

```text
gridshare.legacy-YYYYMMDDHHMMSS.csv
```

Then it starts a clean file with the current schema.

## Recommended Analysis Questions

- Which market regions create the highest bid pressure?
- Which nodes repeatedly emit stale readings?
- How often does demand pressure exceed blackout thresholds?
- What percentage of traded energy is lost to line loss?
- Which nodes accumulate unpaid obligations?
- How much carbon is offset per simulated day?
