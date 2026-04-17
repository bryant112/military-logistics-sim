# Feature Branch Testing Brief

Date: 2026-04-17
Project: Military Logistics Sim
Primary branch context: first-look prototype with RWD, weather, route severity, SITREP UI, and external MTS support.

## Status Overview

- Core sim loop, REST API, and Avalonia operator client exist and are usable for smoke testing.
- Upper Cumberland RWD is seeded in the prototype and used for AO planning, support-zone framing, transport context, and realism experimentation.
- Real weather is first-pass only: use it as a useful realism layer, not a final environment model.
- MTS now supports feature-by-feature or combination testing, so branch slices can be exercised alone or together.
- Several systems are intentionally prototype-grade and should be judged for direction, not final fidelity.

## Testing Targets

- [ ] Startup and session flow: create a session, start it, pause it, reset it, and make sure state stays coherent.
- [ ] AO and planning flow: select the Upper Cumberland AO and confirm the planning context feels plausible.
- [ ] RWD snapshot behavior: static RWD should feel frozen during the run unless manually refreshed.
- [ ] Weather behavior: weather should load at start, refresh on demand, and not create nonsense transitions.
- [ ] Route realism: rougher routes should feel harsher on speed, fatigue, wear, and cargo risk.
- [ ] Support/hostility framing: county and local-support signals should feel directionally believable, not random.
- [ ] SITREP clarity: movement state, incidents, pressure, and risk should be easy to scan quickly.
- [ ] Feature combo coverage: test baseline, each feature alone, and at least one mixed combination in MTS.
- [ ] UI vibe check: the dashboard should feel cinematic and usable, not just themed.

Feature Combo Note:
- Baseline only
- Route severity only
- Weather only
- Dashboard only
- Route severity + weather
- Route severity + dashboard
- Weather + dashboard
- All active

## TODONE

- Core first-look sim/API/UI slice is in place.
- AO planning and SITREP scaffolding exist.
- Upper Cumberland RWD seed data is wired into planning.
- Election-weighted support posture exists at a first-pass level.
- Transport realism profiles exist for civilian and military platforms.
- Route Severity Index / Surface Attrition Factor first pass exists.
- Real-weather first pass exists.
- MTS exists as a standalone reusable testing harness with feature-matrix support.

## TODO

- Tie MTS feature selections directly into all backend feature controls end to end.
- Deepen weather effects into dust, traction, and surface transformation.
- Replace mock or blended non-weather RWD with stronger live import pipelines where appropriate.
- Merge political terrain, support posture, and route chokepoint logic more tightly.
- Tune vehicle-specific road-severity coefficients with more grounded data.
- Add a later-version simulated weather system.

## TODONTS

- Do not treat static RWD as live-changing every few minutes during a run.
- Do not assume public military maintenance numbers are authoritative just because they look specific.
- Do not judge realism from a single perfect-path test.
- Do not let cosmetic UI theme wins hide poor operator workflow.

## TOCANTS

- Cannot yet claim full live OSM/FHWA/Census/RIDB ingestion for all RWD paths.
- Cannot yet claim exact full-fidelity county polygon political mapping everywhere.
- Cannot yet claim a complete dynamic weather simulation system.
- Cannot yet treat all route hostility and road-surface interactions as final-tuned doctrine-grade behavior.

## Feedback Prompts

- What broke?
- What felt fake?
- What confused you?
- Which feature combo was active?
- What data looked wrong?
- What should become easier before the next pass?
