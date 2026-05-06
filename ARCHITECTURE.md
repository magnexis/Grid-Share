# Architecture

GridShare uses a hexagonal architecture to keep market behavior independent from UI, storage, transport, and simulation adapters.

## Project Layout

- `GridShare.Domain`: core records such as `EnergySnapshot`, `TradeOrder`, `MatchedTrade`, `TradeBlock`, settlement accounts, currencies, and energy market profiles.
- `GridShare.Application`: business services such as `MarketOperator`, `MatchmakerEngine`, `OrderBook`, `SettlementService`, forecasting, anomaly detection, carbon accounting, metrics, and ports.
- `GridShare.Simulation`: simulated smart-house actors, diurnal cycle, weather, reliability, and neighborhood generation.
- `GridShare.Infrastructure`: MongoDB ledger, IoT JSON ingestion, replay source, and telemetry exporters.
- `GridShare.Cli`: Spectre.Console dashboard and export runner.
- `GridShare.Api`: ASP.NET Core endpoints, SignalR hub, background simulation worker, and static Three.js dashboard.
- `GridShare.Tests`: xUnit tests for core market behavior.

## Data Flow

1. A source emits `EnergySnapshot` records.
2. `MarketOperator` converts snapshots into asks and bids using location-aware energy market profiles.
3. `OrderBook` retains unmatched orders across ticks.
4. `MatchmakerEngine` matches compatible asks and bids by proximity and price.
5. `DistanceLineLossModel` adjusts delivered energy.
6. `SettlementService` updates wallet balances, battery credits, earnings, and obligations.
7. `ITradeLedger` appends immutable `TradeBlock` records.
8. Telemetry exporters and dashboards consume the resulting `MarketSnapshot`.

## Adapter Boundary

`MarketOperator` depends on `EnergySnapshot`, not on `SmartHouse`, MQTT, SignalR, MongoDB, or UI code. A real IoT adapter can replace the simulation by producing equivalent snapshots.

## Ledger Integrity

Each `TradeBlock` contains:

- transaction ID
- prosumer and consumer IDs
- market codes
- traded and delivered energy
- line loss
- price and settlement amount
- carbon offset
- timestamp
- previous hash
- current hash

Integrity is verified by replaying the previous-hash chain and recomputing each block hash.

## Pricing

GridShare keeps USD as the base settlement unit and allows presentation in multiple world currencies. Market bids and asks use each node's `EnergyMarketProfile`, which includes local retail and solar export prices.

The seeded energy price catalog is for simulation. Production systems should replace it with a tariff and FX provider.

## Telemetry

Telemetry exports are intentionally split:

- node time series
- market aggregates
- trades
- settlement accounts
- anomalies
- JSONL full-fidelity envelopes

This avoids one wide file becoming the only source of truth while still supporting quick spreadsheet analysis.
