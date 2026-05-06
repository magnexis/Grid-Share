# Changelog

All notable changes to GridShare will be documented in this file.

This project follows a lightweight Keep a Changelog style and uses semantic versioning once releases begin.

## [Unreleased]

### Added

- Hexagonal .NET solution with Domain, Application, Simulation, Infrastructure, CLI, API, and Tests projects.
- Smart-house simulation with hardware profiles, batteries, weather-aware solar production, residential demand curves, reliability faults, and household archetypes.
- Market operator with dynamic pricing, persistent order book, line-loss modeling, blackout mitigation, price elasticity, settlement accounts, and carbon accounting.
- Append-only ledger with SHA-256 previous-hash integrity.
- MongoDB ledger adapter.
- Spectre.Console CLI dashboard.
- ASP.NET Core API with SignalR feed and Three.js operations dashboard.
- Currency display catalog and location-aware energy market tariff profiles.
- CSV and JSONL telemetry exports with node, market, trade, account, anomaly, forecast, and aggregate metrics.
- Unit tests for ledger integrity, pricing behavior, order-book persistence, line loss, diurnal solar production, and location-aware pricing.

### Changed

- Telemetry CSV export now uses schema version 2 style fields and archives old incompatible CSV headers as `.legacy-*`.

### Security

- Added security policy and guidance for secrets, ledger integrity, and external adapters.
