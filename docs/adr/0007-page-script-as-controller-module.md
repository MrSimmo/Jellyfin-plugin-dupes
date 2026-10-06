# 0007. Page script as a separate `data-controller` ES module

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-R1.1, DF-R5–R8)

## Context
jellyfin-web 12.2 passes plugin page HTML through `globalize.translateHtml`, which rewrites every
`${...}` (breaking JS template literals in inline scripts), and inline scripts run in global
scope and can re-run when views are re-fetched (a second top-level `const` throws).
`viewContainer.js` supports `data-controller="__plugin/<pageName>"`, importing that plugin page
as an ES module and calling `new module.default(view, params)`.

## Decision
`dupefinder.html` holds markup only, with `data-controller="__plugin/dupefinderjs"`.
`dupefinder.js` is registered as plugin page `dupefinderjs` (resource name ends `.js` so the
server sends a JavaScript MIME type) and exports `export default function (view) { ... }`
(not an arrow function), wiring everything on `viewshow`. Same pattern as the official
Webhook and Playback Reporting plugins.

## Alternatives Considered
- Inline `<script>` with an IIFE: works, but template literals are silently corrupted.
- Bundled TypeScript app (Intro Skipper style): build tooling not justified for one page.

## Consequences
Two embedded resources instead of one. No build step for the front end.
