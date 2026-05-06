# Governance

GridShare is currently maintained as a small prototype project.

## Maintainer Responsibilities

Maintainers are responsible for:

- Reviewing pull requests.
- Keeping the default branch buildable.
- Triaging issues.
- Protecting security reports.
- Preserving the architecture boundaries between domain, application, and adapters.

## Decision Making

Project decisions should optimize for:

- Correctness of the market simulation.
- Clear separation of concerns.
- Reproducibility of telemetry and tests.
- Honest labeling of simulation assumptions.
- Safety around real-world energy infrastructure claims.

## Changes Requiring Extra Review

Extra review is recommended for changes to:

- Ledger hashing or append behavior.
- Settlement math.
- Pricing and tariff assumptions.
- Blackout mitigation.
- Real IoT ingestion.
- Public API contracts.
- Telemetry schemas.
