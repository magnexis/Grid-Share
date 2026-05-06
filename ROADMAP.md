# Roadmap

GridShare is currently a high-fidelity simulation prototype. The roadmap below tracks how it can become a more complete marketplace platform.

## Near Term

- Add richer tests for telemetry exporters, MongoDB ledger persistence, and SignalR frame shape.
- Add configurable market profiles from JSON or database storage.
- Add dashboard controls for simulation speed, cloud cover, outage injection, and currency.
- Add OpenTelemetry OTLP exporter configuration.
- Add sample notebooks or scripts for telemetry analysis.

## Medium Term

- Add real MQTT broker subscription support with authentication.
- Add tariff and FX provider ports for live pricing feeds.
- Add account settlement export and reconciliation reports.
- Add scenario files for blackout, midday surplus, low battery, and high EV charging load.
- Add Dockerfile and deployable API container.

## Long Term

- Add multi-feeder grid topology instead of simple distance-based line loss.
- Add optimization-based dispatch for batteries and critical loads.
- Add role-based API auth for operators, prosumers, and consumers.
- Add persistent market-order storage.
- Add production-grade audit logs and tamper-evident checkpoints.

## Non-Goals for the Prototype

- Direct real-world grid control.
- Billing-grade settlement without external tariff validation.
- Safety-critical reliability claims.
