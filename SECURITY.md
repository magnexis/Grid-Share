# Security Policy

GridShare is a simulation and prototype platform. It is not production grid-control software and must not be used to operate real energy infrastructure without independent engineering, security, safety, and regulatory review.

## Supported Versions

Security fixes are accepted for the current default branch.

## Reporting a Vulnerability

Please do not disclose exploitable vulnerabilities in public issues.

Report security concerns by opening a private GitHub security advisory if the repository supports it. If private advisories are unavailable, contact the maintainers through a private channel and include:

- A description of the issue.
- Steps to reproduce.
- Affected components.
- Potential impact.
- Any suggested remediation.

## Security-Relevant Areas

Pay particular attention to:

- Ledger integrity and hash-chain validation.
- MongoDB connection handling and credentials.
- SignalR/API exposure.
- MQTT or IoT ingestion adapters.
- Telemetry exports that may contain node identifiers.
- Currency and tariff feeds if replaced with live external providers.

## Secrets

Never commit real credentials, tokens, connection strings, broker passwords, or private keys. Use environment variables such as:

```powershell
$env:ConnectionStrings__MongoLedger="mongodb://localhost:27017"
```

The `.env.example` file documents supported environment values without real secrets.
