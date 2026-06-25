# Release Guide

Release materials now live together in the `release/` folder.

Start here:

- `release/README.md`
- `release/cli.md`
- `release/api.md`
- `release/checklist.md`
- `release/notes-template.md`

The old root scripts still work as shims:

```powershell
./release-cli.ps1 -Version 0.1.1
./release-api.ps1 -Version 0.1.1
```

Preferred commands:

```powershell
./release/publish-cli.ps1 -Version 0.1.1
./release/publish-api.ps1 -Version 0.1.1
```
