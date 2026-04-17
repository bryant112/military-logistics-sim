# Project Notes

## Preferred GitHub Integration

- For GitHub work in this repository, prefer using `[$github](app://connector_76869538009648d5b282a4bb21c3d157)` when the connector is available in the current Codex session.
- Use the GitHub connector first for repository, issue, pull request, and metadata tasks.
- Fall back to local `git` and `gh` only when the connector cannot perform the needed action.

## Repository Identity

- GitHub repository: `https://github.com/bryant112/military-logistics-sim`
- Default branch: `main`
- Local path used during bootstrap: `C:\dev\military-logistics-sim`

## Testing Briefs

- For feature-branch testing handoffs, prefer using the `feature-branch-test-brief` skill to generate a concise Markdown + HTML + PDF testing sheet in `docs\` with `Status Overview`, `Testing Targets`, `TODONE`, `TODO`, `TODONTS`, `TOCANTS`, and `Feedback Prompts`.

## Project Context

- This is a first-look military logistics simulator prototype.
- The current slice includes a .NET simulation core, REST API, mock enrichment/population modeling, a thin Avalonia operator client, and xUnit tests.
- Real-world data integrations are still mock-backed in this version; live OSM/FHWA/Census/RIDB ingestion is not implemented yet.


