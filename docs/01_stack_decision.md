# Stack Decision Record

## Chosen architecture
**Hybrid desktop simulation platform**

- **App type:** Windows desktop application with attached 3D visualization
- **Core simulation engine:** .NET 8 class libraries
- **Desktop operator UI:** Avalonia UI or WPF
- **3D renderer / scene client:** Unreal Engine 5.x
- **Persistence:** PostgreSQL + PostGIS
- **Cache/session state:** Redis
- **Transport between sim and renderer:** gRPC + event bus
- **Async event bus:** NATS
- **Telemetry/logging:** OpenTelemetry + Seq/Grafana/Loki
- **Scenario/mod content:** JSON/YAML assets validated by schemas

## Why not pure Unreal
Pros:
- great visuals
- strong camera workflows
- easy 3D interaction

Cons:
- harder to build rich planning-heavy data UI
- harder to keep simulation deterministic and heavily testable
- enterprise-style imports/exports/reporting become awkward

## Why not pure Unity
Unity is still viable, but for this concept Unreal is a better fit for:
- realistic military-style scene presentation
- better out-of-box cinematic/tactical camera feel
- stronger “operations floor + battlefield picture” visual tone

## Why not pure Godot
Godot is attractive for cost and simplicity, but for this specific use case it is weaker on:
- high-end 3D presentation
- large-scene realism
- defense-style operator visualization polish

## Desktop UI choice
### Default: Avalonia UI
Use Avalonia if you want:
- Windows now
- possible Linux support later
- XAML-ish productivity without being locked to WPF

### Alternative: WPF
Use WPF if you want:
- fastest path for Windows-only internal tooling
- mature enterprise desktop patterns
- strongest existing .NET desktop ecosystem familiarity

## 3D/map choice
### Default: Unreal + Cesium for Unreal
Use for:
- geospatial terrain
- large-area scenes
- route overlays
- camera transitions
- tactical playback in-world

## Service boundaries
1. **Sim Engine**
2. **Scenario Service**
3. **Asset & Route Service**
4. **Event Stream Service**
5. **Renderer Bridge**
6. **Replay/After-Action Service**
7. **Media Feed Service**

## Packaging
- Windows desktop installer later
- local dev: Docker Compose for services + Unreal project run separately
