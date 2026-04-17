# Risks and Design Notes

## Biggest risk
Trying to make this simultaneously:
- a full logistics simulator
- a tactical shooter
- a live intel platform
- a command-and-control suite

That will explode scope.

## Correct framing
This should be:
**a logistics/operations simulator with tactical visualization modules**

Not:
**a full combat simulator with some logistics attached**

## Practical design rules
1. Backend owns truth
2. 3D client renders truth
3. Tactical feeds are contextual overlays
4. Replays use recorded state + events
5. Every complex subsystem gets a fake/mock mode first

## Smart simplifications
- start with route graphs, not freeform driving AI
- use probability/state-machine incidents before advanced AI adversaries
- use mock video feeds first
- use generic asset classes before exact vehicle fidelity
- use time compression controls early

## First user stories
- As a planner, I can build a supply route from depot to FOB.
- As a controller, I can launch a convoy and monitor ETA.
- As an operator, I can see stock depletion at a base.
- As a controller, I receive an alert when an ambush delays a convoy.
- As an operator, I can switch to a drone/helmet camera view for that incident.
- As an analyst, I can replay the event sequence after the scenario ends.
