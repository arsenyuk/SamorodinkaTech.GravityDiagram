# AGENTS.md

## Quick reference

```bash
# Build (all projects)
dotnet build -v minimal

# Run tests
dotnet test SamorodinkaTech.GravityDiagram.Core.Tests/SamorodinkaTech.GravityDiagram.Core.Tests.csproj

# Run demo (Avalonia GUI)
dotnet run --project SamorodinkaTech.GravityDiagram.Demo/SamorodinkaTech.GravityDiagram.Demo.csproj

# Run dump analyzer CLI
dotnet run --project Tools/AnalyzeDump/AnalyzeDump.csproj
```

## Architecture

Three projects + one tool in a single solution, all targeting net9.0:

| Project | Role | Dependencies |
|---------|------|--------------|
| `SamorodinkaTech.GravityDiagram.Core` | Physics layout engine + diagram model | None (pure .NET) |
| `SamorodinkaTech.GravityDiagram.Demo` | Avalonia 11.3 interactive renderer | Avalonia, Core |
| `SamorodinkaTech.GravityDiagram.Core.Tests` | xUnit 2.9 tests | Core |
| `Tools/AnalyzeDump` | CLI that replays a dump and checks for arc-node crossings | Core |

No NuGet restore surprises — Core has zero external packages.

## Domain model (Core)

- **`Diagram`** — container for `Nodes`, `Ports`, `Arcs`. Call `AddNode` → `AddPort` → `AddArc` in that order (arc validation requires ports to exist).
- **`RectNode`** — position is **center** (`Vector2 Position`), not top-left. `Bounds` is computed via `RectF.FromCenter`.
- **`Port`** — identified by `DiagramId`, references a node side (`RectSide`) + offset (0..1 along that side). `AutoDistributePorts` evenly spaces ports on the same side.
- **`Arc`** — connects two ports. `InternalPoints` are the orthogonal polyline waypoints updated by the physics engine.
- **`DiagramId`** — wraps a string. `DiagramId.New()` generates a GUID-based id.
- **`GravityLayoutEngine`** — the main simulation. Call `Step(diagram, dt)` to advance physics. `PreviewStep` returns a non-mutating snapshot.
- **`LayoutSettings`** — all tunable physics parameters. Key ones: `UseHardMinSpacing`, `MinimizeArcLength`, `ConnectedArcAttractionK`, `ArcPointAttractionK`.

## Arc routing

Arcs are **orthogonal polylines** (only horizontal/vertical segments). The engine:
1. Generates an initial L-shaped or 3-bend route via `MakeOrthogonalPolyline`
2. Applies spring forces between adjacent internal points
3. Repels arc points from node bounds (clearance zone)
4. Runs hard constraint iterations to keep points outside node rectangles
5. Repairs segments that cross node interiors by inserting waypoints

The key invariant: **no arc segment should pass through a node interior**. Tests verify this with `ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect`.

## Testing

- Tests load JSON dump files from `TestData/*.json` (auto-copied to output via `<None Include="TestData\*.json" CopyToOutputDirectory="PreserveNewest" />`).
- Dump files contain a full snapshot of diagram + settings + forces at a point in time.
- Regression tests replay dumps through the engine and verify arc routing invariants.
- `ArcRoutingGeometryTests` is **disabled** (`#if false`) — the routing model is still evolving.
- `DumpIntersectionCheckerTests.CheckUserLatestAppDataDump_*` reads from the user's AppData folder — will pass trivially if no dumps exist.

## Demo app

- Avalonia desktop app with interactive settings panel (sliders for physics params).
- `DiagramView` runs the physics simulation on a 16ms timer with sub-stepping.
- Auto-stops when no pixel-level node movement is detected for 30 ticks.
- `GravityModelDumpWriter` saves dumps to `~/Library/Application Support/SamorodinkaTech.GravityDiagram/dumps/` (macOS) or equivalent AppData path.
- `DemoSettingsStore` persists layout settings to `demo-layout-settings.json` in the same AppData folder.

## Tools

- **AnalyzeDump** — loads the latest dump from AppData, replays 120 engine steps, checks for arc-segment-vs-node-interior violations. Exit code 0 = clean, 1 = violations found.

## Conventions

- Node positions are **center-based** (not top-left). `RectF` stores `(X, Y, Width, Height)` where X,Y is the top-left corner.
- `RectSide` enum: `Top`, `Right`, `Bottom`, `Left`.
- `PortFlow` is a flags enum: `Incoming`, `Outgoing`, `Both`.
- All `float` physics values — no `double` in the layout engine.
- Some source files contain Russian comments (e.g., in `GravityLayoutEngine.cs`).
- `LayoutSettings` class is in the **global namespace** (no explicit namespace declaration).
