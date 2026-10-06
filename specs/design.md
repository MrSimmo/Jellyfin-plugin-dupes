# Design Spec — Duplicate & Unmatched Finder

The plugin page lives inside Jellyfin's dashboard and must look native. Jellyfin's active theme
(dark, light, or any user theme) supplies every colour, font and spacing value; this file adds
exactly one token.

## Brand
- Product name: "Duplicate & Unmatched Finder". Voice: plain, factual admin-tool wording.
- No logo. Sidebar icon: Material Icons ligature `content_copy` (via `PluginPageInfo.MenuIcon`).

## Colour Tokens
- Inherit all colours from the active Jellyfin theme. No hex values in plugin markup or CSS.
- `--df-group-band: rgba(127, 127, 127, 0.12)` — background for alternate duplicate groups in
  the results table. Neutral grey at low alpha so it reads on both light and dark themes.

## Typography
- Inherit the dashboard font and sizes. Headings use Jellyfin's `sectionTitle` class.

## Spacing & Layout
- Page structure: `div[data-role="page"].page.type-interior.pluginConfigurationPage` >
  `div.content-primary` > `div.verticalSection` blocks (Libraries, Checks, Results).
- Results table scrolls horizontally inside its own container; the page never scrolls sideways.

## Components
Only components jellyfin-web 12.2 guarantees on a cold plugin-page load:
- Buttons: `<button is="emby-button" class="raised button-submit">` for Scan (primary),
  `class="raised"` for Download CSV, `class="raised"` small row buttons for actions.
- Checkboxes: `<label class="emby-checkbox-label"><input is="emby-checkbox" type="checkbox"><span>…</span></label>`
  inside `div.checkboxContainer`.
- Table: `table.detailTable` with `detailTableHeaderCell` / `detailTableBodyCell` cells.
- Loading: `Dashboard.showLoadingMsg()` / `hideLoadingMsg()`. Errors: `Dashboard.alert()`.
- Not used (not guaranteed loaded in 12.2): `emby-toggle`, `emby-radio`, `emby-collapse`,
  `emby-textarea`.

## Iconography
- Material Icons ligatures already loaded by jellyfin-web. No other icon set.

## Motion
- None beyond Jellyfin's own.

## Accessibility
- Every checkbox and button has a visible text label.
- Table header cells use `scope="col"`.
- All actions are native buttons/links, reachable by keyboard in DOM order.

## Acceptance Criteria
- DS-AC1: the page's CSS and markup contain no colour values other than `--df-group-band`.
- DS-AC2: every interactive element is a native `button`, `a`, or `input` with a text label.
- DS-AC3: in both the default dark theme and the light theme, the page renders legibly
  (screenshots compared during verification).

## Don'ts
- No custom fonts, colour palettes, frameworks or CSS libraries.
- No inline `<script>` in the page HTML (ADR-0007).
