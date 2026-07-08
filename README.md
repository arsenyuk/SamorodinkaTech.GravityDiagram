# SamorodinkaTech.GravityDiagram

A .NET 9.0 library for automatic layout of rectangular node diagrams using physics simulation.

## Key Features

- **Physics-based layout** — gravity, springs, and repulsion forces position nodes automatically
- **Orthogonal arc routing** — arcs are rendered as clean horizontal/vertical polylines
- **Real-time interaction** — adjust physics parameters and see results instantly
- **Zero dependencies** — core library has no NuGet packages
- **Cross-platform** — runs on Windows, macOS, and Linux via Avalonia

## Quick Start

```bash
# Build
dotnet build -v minimal

# Run interactive demo
dotnet run --project SamorodinkaTech.GravityDiagram.Demo

# Run tests
dotnet test SamorodinkaTech.GravityDiagram.Core.Tests
```

## Architecture

See [architecture.md](architecture.md) for detailed documentation on the physics engine, domain model, and project structure.

## License

MIT — see [LICENSE](LICENSE).
