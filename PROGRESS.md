Project progress log. Status: [x] done · [!] failed (retry) · [ ] not started (carry forward) · [~] deferred (reason).

## 2026-10-06 12:35 — Research, design and spec for Duplicate & Unmatched Finder

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

## 2026-10-06 13:05 — Implementation plan and kickoff prompt

**Scope:** Spec approved by the user; write the implementation plan (writing-plans) and a kickoff prompt for Opus 5.5 with Opus 5.5 subagents. Spec: `specs/dupe-finder.md` (all requirements); amendment §8.
**Model:** Opus 5.5 @ session default.

**Tasks:**
- [x] T5 — User review of `specs/dupe-finder.md` (approved: "Looks good")
- [x] T6 — Implementation plan `docs/superpowers/plans/2026-10-06-dupe-finder.md` (8 tasks) and kickoff prompt `docs/superpowers/plans/2026-10-06-dupe-finder-kickoff.md`
- [x] T6a — Spec amended before implementation (item set keeps owned alternate versions; Jellyfin JSON conventions; `ItemType`); ADR-0008
- [ ] T7 — Implementation per plan (carried forward; run the kickoff prompt in a new session)

**Changes:** `docs/superpowers/plans/2026-10-06-dupe-finder.md`, `docs/superpowers/plans/2026-10-06-dupe-finder-kickoff.md`, `specs/dupe-finder.md` (§4, DF-R2, DF-R7.2, DF-R8.3, AC-10, §8), `docs/adr/0008-follow-jellyfin-data-and-json-conventions.md`, `PROGRESS.md` (also corrected the previous entry's time to the real commit time, 12:35).
**Spec:** Amended §4 Item set, DF-R2.1–R2.4 (PascalCase, omit-null, problem details, group numbering, member order), DF-R7.2, DF-R8.3 wording, AC-10. Recorded in §8.
**Decisions:** ADR-0008. 12.2 stores auto-detected alternate versions as owned items (`LibraryManager.cs:534`), so the scan uses `IncludeOwnedItems` and filters extras/parts itself; JSON follows `JsonDefaults.PascalCaseOptions`; CA1721 forces `ItemType`.
**Verified:** The plan's JavaScript, page tests, Python and shell were extracted to the scratchpad and run. `node --test` passed 12/12, `crosscheck.py` compiles, both shell scripts parse, `ui-check.mjs` parses, the page HTML has 0 `${`, and there is exactly one static `innerHTML`. Running them also exposed a plan bug, now fixed: Node 24 needs `node --test tests/web/*.test.mjs` rather than a directory argument. **Not verified:** the plan's C# has not been compiled (.NET SDK not installed yet; install is gated on approval in Task 1). Analyser findings are expected to be fixed during implementation.
**Next:** Start a new Opus 5.5 session and paste the kickoff prompt (fill in server URL and account names in the chat only).

## 2026-10-07 10:25 — Implementation of Duplicate & Unmatched Finder (plan Tasks 1–8)

**Scope:** Execute `docs/superpowers/plans/2026-10-06-dupe-finder.md` with subagent-driven development: one Opus implementer and one Opus reviewer per task, scoped re-review after each fix round. Spec: `specs/dupe-finder.md` (all requirements), `specs/design.md`.
**Model:** Opus 5.5 @ session default (controller); implementers and reviewers on Opus. No fallback.

**Tasks:**
- [x] T7 — Implementation per plan (carried forward), every task reviewed against the spec:
  - [x] Task 1 — scaffold, analysers, text normaliser (fix round: spec §4 order NFKD before `&`; letters outside the BMP kept)
  - [x] Task 2 — duplicate grouping and reasons (spec amended, ADR-0009)
  - [x] Task 3 — version groups, unmatched and incomplete detection
  - [x] Task 4 — scan orchestration, ordering, validation
  - [x] Task 5 — Jellyfin item source, admin controller, plugin class, packaging scripts, `scripts/crosscheck.py` (fix round: AC-11 checked in both directions, REST box-set collapsing disabled)
  - [x] Task 6 — dashboard page, row actions, CSV export (fix round: raised row actions per `specs/design.md`)
  - [x] Task 7 — Playwright UI check script (fix round: every check can fail on its defect; error output redacted for DF-C4)
  - [x] Task 8 — README, `.gitignore`, privacy audit, this entry
- [x] T8 — Live verification of the installed plugin on the test server, done by the user by hand ("fully tested the plugin. It works great", 2026-10-07). No live output was captured in this session.
- [~] T9 — Scripted live checks not run: `scripts/crosscheck.py` (AC-7, AC-9, AC-10, AC-11) and `tests/e2e/ui-check.mjs` (AC-8, AC-12, AC-13, DF-R1 guard). Reason: the user tested by hand and asked to close. Unblock: run both with the env vars against an installed build.
- [~] T10 — Final whole-branch code review (plan Task 8 Step 1) not run; its DF-C3/DF-C4 audit part was run (see Verified). Reason: the user asked to close. Unblock: run before a public release and triage the follow-ups below.
- [~] T11 — 1.0.0.0 release package (plan Task 8 Step 3) not built or installed; the user tested a test build from this session's local repository. Unblock: `scripts/build-repo.sh <public base URL>` when the repository is hosted.

**Changes:** `src/Jellyfin.Plugin.DupeFinder/` (plugin, controller, scanning, model, `Web/` page), `tests/Jellyfin.Plugin.DupeFinder.Tests/`, `tests/web/`, `tests/e2e/`, `scripts/`, `README.md`, `CLAUDE.md`, `.editorconfig`, `.gitignore`, `Directory.Build.props`, `Jellyfin.Plugin.DupeFinder.slnx`, `specs/dupe-finder.md`, `docs/adr/0009-empty-names-never-match-and-id-comparison.md`, `docs/lessons/` (two new lessons). History was rewritten before the first push to drop a server detail from this file (see Verified).
**Spec:** §8 amendment (Task 2): empty normalised names never match by name; provider ids compared trimmed and case-insensitively; DF-R2.3 `ProviderIds` trimmed. Status line set to Implemented.
**Decisions:** ADR-0009 (empty names, id comparison). Smaller rulings, recorded in commits and code comments: `TextNormalizer` follows spec §4 order; `ProviderIds` copied with `TryAdd` because Jellyfin rebuilds that dictionary case-sensitively from database rows; `Web/package.json` pins ES modules for the page tests (lesson); the AC-11 cross-check runs in both directions.
**Verified:** At the last code commit: `dotnet build -c Release --no-incremental` → `0 Warning(s)`, `0 Error(s)`; `dotnet test` → `Passed: 49, Failed: 0`; `node --test tests/web/*.test.mjs` → `pass 12`, `fail 0`. Privacy audit of tracked files and full history: the only IP-like strings are `0.0.0.0` and version numbers; no server address, account names, e-mail or local paths; commit authors use a GitHub noreply address. Live behaviour: user-reported only.
**Follow-ups (deferred minor findings from the task reviews):**
- Robustness: `string.Normalize` in `TextNormalizer` throws on invalid UTF-16, which would fail a whole scan; `VersionGroups` does not guard self-referencing or chained `PrimaryVersionId`; the page's `errorMessage` can reject and skip the alert; a failed library load is not retried while the page is cached.
- Performance: the item query uses default `DtoOptions` (all fields, user data and images joined); measure `DurationMs` on the full library against ADR-0002's 120 s.
- Docs: ADR-0002 still mentions `IncludeAlternateVersions` and "reduced DTO options"; ADR-0008 and the code use `IncludeOwnedItems`.
- Spec observations: normalisation folds Japanese dakuten (バス, パス → ハス), so unmatched same-year titles differing only by dakuten group as duplicates; the album-artist key depends on artist order.
- Tests and tooling: coverage gaps (mixed title + year bucket, path tie-breaks, collapse wiring per check, CSV CR/`0` cases, BOM written as a literal); `build-repo.sh` lacks an empty-version guard; README's Requires line omits Node and Playwright.
**Next:** Run `scripts/crosscheck.py` and `tests/e2e/ui-check.mjs` (env vars only) against an installed build to capture AC-7–AC-13 evidence, then triage the follow-ups.
