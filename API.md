# API Reference

Run the API:

```powershell
dotnet run --project GridShare.Api -- --urls http://localhost:5077
```

OpenAPI/Swagger:

```text
http://localhost:5077/swagger
```

Dashboard:

```text
http://localhost:5077
```

## Endpoints

### `GET /api/grid/state`

Returns the latest `MarketSnapshot`.

### `GET /api/grid/frame`

Returns the dashboard frame:

- `market`
- `nodes`
- `flows`

This is the easiest endpoint for external visualization clients.

### `GET /api/grid/ledger`

Returns recorded `TradeBlock` entries.

### `GET /api/grid/accounts`

Returns settlement account snapshots.

### `GET /api/grid/health`

Returns ledger verification and basic market health.

### `GET /api/currencies`

Returns supported display currencies.

### `GET /api/energy-markets`

Returns seeded location-aware energy market profiles.

## SignalR

Hub:

```text
/hubs/grid
```

Event:

```text
grid.tick
```

Payload:

- `market`
- `nodes`
- `flows`
