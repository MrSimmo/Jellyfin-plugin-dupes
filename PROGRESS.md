Project progress log. Status: [x] done · [!] failed (retry) · [ ] not started (carry forward) · [~] deferred (reason).

## 2026-10-06 13:30 — Research, design and spec for Duplicate & Unmatched Finder

**Scope:** Research Jellyfin 12.2 plugin capabilities; agree design; write spec, design spec and ADRs. Spec: `specs/dupe-finder.md` (all requirements new).
**Model:** Opus 5.5 @ session default.

**Tasks:**
- [x] T1 — Research server-side plugin API for 12.2 (target framework, packages, controllers, ILibraryManager, versions) — verified against `jellyfin` tag `v12.2` source
- [x] T2 — Research jellyfin-web 12.2 plugin page hosting, globals, Identify dialog — verified against `jellyfin-web` tag `v12.2` source
- [x] T3 — Design agreed with user (server-side scan + admin endpoint; all four duplicate types; merged versions reported; all unmatched checks selectable; plugin-repository delivery from a local dev server)
- [x] T4 — Spec, design spec, ADR-0001…0007, lesson on 12.x auth header written
- [ ] T5 — User review of `specs/dupe-finder.md` (awaiting)
- [ ] T6 — Implementation plan via writing-plans, after spec approval
- [ ] T7 — Implementation per plan

**Changes:** `specs/dupe-finder.md`, `specs/design.md`, `docs/adr/0001`…`0007`, `docs/lessons/jellyfin-12-auth-header.md`, `PROGRESS.md`; `git init` (branch `main`).
**Spec:** Added DF-C1…C5, DF-R1…R9, AC-1…AC-14; design spec DS-AC1…3.
**Decisions:** ADR-0002 server-side scan behind admin endpoint; ADR-0003 Identify via `Dashboard.itemIdentifier`; ADR-0004 self-hosted plugin repo; ADR-0005 net10.0 / 12.2.0 / targetAbi 12.2.0.0; ADR-0006 CSV built in browser; ADR-0007 page JS as `data-controller` module.
**Verified:** Server public info reports `"Version":"12.2.0"`. Disputed Identify finding settled by `src/utils/dashboard.js:17,261,267` in jellyfin-web `v12.2`. `DashboardController.GetDashboardConfigurationPage` has no `[Authorize]`. Album artists are library-scoped via `GetAlbumArtists` + `AncestorIds` (`ArtistsController.cs:326-354`). Plugin name absent from the official stable manifest (36 plugins checked).
**Next:** User reviews `specs/dupe-finder.md`; then write the implementation plan.
