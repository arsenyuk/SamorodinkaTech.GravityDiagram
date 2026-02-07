# SamorodinkaTech.GravityDiagram

A small .NET (net9.0) library + Avalonia demo for laying out rectangular node diagrams using a simple “gravity + springs + repulsion” simulation.

## What’s inside

- **Core** (`SamorodinkaTech.GravityDiagram.Core`):
  - Diagram model (nodes, ports, arcs)
  - Physics layout engine with:
    - pair attraction (background gravity)
    - connected-node attraction along arcs
    - overlap/min-spacing repulsion
    - optional hard constraint solver for `MinNodeSpacing`
- **Demo** (`SamorodinkaTech.GravityDiagram.Demo`):
  - Avalonia renderer and interactive settings panel
  - Orthogonal (polyline) arc routing with basic overlap avoidance
  - Debug overlays (min-spacing bounds + force vectors)
- **Tests** (`SamorodinkaTech.GravityDiagram.Core.Tests`): xUnit tests for core behavior.

## Build

```bash
dotnet build -v minimal
```

## Run demo

```bash
dotnet run --project "SamorodinkaTech.GravityDiagram.Demo/SamorodinkaTech.GravityDiagram.Demo.csproj"
```

## Notes

- Layout settings can be changed live in the demo’s left panel.
- If you disable the hard min-spacing solver, use the **soft overlap boost** slider to make intersecting rectangles separate faster.

## License

MIT — see [LICENSE](LICENSE).
