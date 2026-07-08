# Architecture

## Overview

SamorodinkaTech.GravityDiagram is a physics-based layout engine for rectangular node diagrams. It uses a "gravity + springs + repulsion" simulation to automatically position nodes and route orthogonal (polyline) arcs between them.

The system is designed for interactive use — the layout updates in real-time as the user adjusts physics parameters.

## Core Concepts

### Diagram Model

The diagram is a graph of three entity types:

```
Diagram
├── RectNode[]   — rectangular boxes with center position, size, and per-side port flow rules
├── Port[]       — connection points on node sides (side + offset 0..1)
└── Arc[]        — connections between ports, rendered as orthogonal polylines
```

**Key design decisions:**
- **Center-based coordinates**: `RectNode.Position` is the center of the rectangle, not the top-left corner. `Bounds` is computed via `RectF.FromCenter(Position, Width, Height)`.
- **Side + offset port positioning**: Ports are placed on a specific side (`Top`, `Right`, `Bottom`, `Left`) with an offset (0..1) along that side. `AutoDistributePorts` evenly spaces ports on the same side.
- **Directional flow**: Each node side has a `PortFlow` rule (`Incoming`, `Outgoing`, or `Both`) that controls which arcs can connect to that side.

### Physics Engine

`GravityLayoutEngine` is the main simulation class. It applies several force types to compute node positions:

1. **Background gravity** — weak pairwise attraction between all nodes (keeps the diagram compact)
2. **Connected arc attraction** — spring-like pull between nodes connected by arcs (pulls related nodes together)
3. **Overlap repulsion** — pushes overlapping nodes apart (prevents visual collisions)
4. **Hard min-spacing constraint** — optional post-step solver that enforces minimum edge-to-edge distance

The engine runs in discrete time steps (`Step(diagram, dt)`) and supports a non-mutating preview mode (`PreviewStep`).

### Arc Routing

Arcs are **orthogonal polylines** — all segments are either horizontal or vertical. The routing algorithm:

1. **Initial route**: Generates an L-shaped or 3-bend path from `MakeOrthogonalPolyline`, respecting port exit directions
2. **Spring forces**: Adjacent internal points attract each other (keeps the route smooth)
3. **Node repulsion**: Internal points are repelled from node bounds (prevents arcs from crossing nodes)
4. **Hard constraints**: Iterative correction to ensure no arc point penetrates a node's clearance zone
5. **Segment repair**: If a segment crosses a node interior, waypoints are inserted to route around it

**Key invariant**: No arc segment should pass through a node interior. This is verified by tests using `ArcRoutingGeometry.AxisAlignedSegmentIntersectsRect`.

### Layout Settings

All physics parameters are exposed through `LayoutSettings`:

| Parameter | Default | Purpose |
|-----------|---------|---------|
| `NodeMass` | 12.8 | Uniform mass for all nodes (affects acceleration) |
| `BackgroundPairGravity` | 0.12 | Weak pairwise attraction strength |
| `ConnectedArcAttractionK` | 2.2 | Spring constant for connected nodes |
| `ArcPointAttractionK` | 6.0 | Spring constant between adjacent arc points |
| `ArcPointNodeRepulsionK` | 1200 | Repulsion strength from node clearance zones |
| `UseHardMinSpacing` | false | Enable hard constraint solver |
| `MinimizeArcLength` | false | Make arcs as short as possible |
| `Drag` | 2.2 | Velocity damping factor |
| `MaxSpeed` | 2500 | Velocity clamp |

## Project Structure

```
SamorodinkaTech.GravityDiagram/
├── Core/                          — Physics engine + diagram model (zero NuGet deps)
│   ├── GravityLayoutEngine.cs     — Main simulation (1500+ lines)
│   ├── Diagram.cs                 — Container for nodes, ports, arcs
│   ├── RectNode.cs                — Rectangle with center-based position
│   ├── Port.cs                    — Connection point on a node side
│   ├── Arc.cs                     — Orthogonal polyline connection
│   ├── ArcRoutingGeometry.cs      — Geometric helpers for routing
│   ├── LayoutSettings.cs          — All tunable physics parameters
│   └── ...
├── Demo/                          — Avalonia 11.3 interactive renderer
│   ├── DiagramView.cs             — Canvas with physics simulation loop
│   ├── MainWindow.axaml           — Settings panel + canvas
│   ├── GravityModelDumpWriter.cs  — Saves diagram snapshots to JSON
│   └── DemoSettingsStore.cs       — Persists settings to disk
├── Core.Tests/                    — xUnit 2.9 regression tests
│   ├── GravityLayoutEngineTests.cs
│   ├── DumpIntersectionCheckerTests.cs
│   └── TestData/                  — JSON dump files for replay tests
└── Tools/
    └── AnalyzeDump/               — CLI: replays dumps, checks for arc-node violations
```

## Data Flow

```
User creates Diagram
  → AddNode() → AddPort() → AddArc()
  → GravityLayoutEngine.Step() runs physics
    → Forces applied to node positions
    → Arc internal points updated (spring + repulsion)
    → Hard constraints enforced (optional)
  → Renderer reads updated positions
  → Loop repeats
```

## Testing Strategy

Tests use **JSON dump files** — complete snapshots of diagram state, settings, and forces at a point in time. Regression tests replay these dumps through the engine and verify:

- Arc segments don't cross node interiors
- Node positions converge correctly
- Arc routing invariants hold after multiple steps

Two dump formats coexist:
- `DumpRoot` (older) — has `Dt` field, no arc InternalPoints
- `FreshDumpRoot` (newer) — full arc InternalPoints/InternalPointForces

## Platform Notes

- All physics values are `float` (no `double` in the layout engine)
- `LayoutSettings` is in the **global namespace** (no explicit namespace declaration)
- Some source files contain Russian comments
- Demo dumps are saved to `~/Library/Application Support/SamorodinkaTech.GravityDiagram/dumps/` on macOS
