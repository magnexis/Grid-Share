# Deployment

GridShare can run locally as a CLI, as an ASP.NET Core API, or with optional MongoDB persistence.

## Local API

```powershell
dotnet run --project GridShare.Api -- --urls http://localhost:5077
```

## MongoDB

Start MongoDB:

```powershell
docker compose up -d
```

Set the connection string:

```powershell
$env:ConnectionStrings__MongoLedger="mongodb://localhost:27017"
```

Run the API:

```powershell
dotnet run --project GridShare.Api -- --urls http://localhost:5077
```

## Runtime Configuration

Environment variables:

- `ConnectionStrings__MongoLedger`
- `GridShare__HouseCount`
- `GridShare__TickDelayMs`
- `GridShare__SimulatedMinutesPerTick`

See `.env.example`.

## Production Notes

- Put the API behind HTTPS.
- Use a managed MongoDB instance with authentication.
- Replace seeded tariff/FX references with live provider adapters.
- Configure OpenTelemetry export to your observability backend.
- Treat the browser dashboard as visualization, not grid-control software.
