# 0006. Build the CSV in the browser from the scan response

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-R8)

## Context
A plain download link cannot carry the admin's token: Jellyfin 12.x disables legacy `api_key`
query auth by default, and a page-generated URL would otherwise need the token embedded.

## Decision
The page keeps the latest scan response and builds the CSV client-side (RFC 4180, UTF-8 BOM,
CRLF, formula-injection prefix), downloading it with `URL.createObjectURL` and `<a download>`.
No CSV endpoint on the server. jellyfin-web 12.2 applies no CSP or sanitiser that blocks this.

## Alternatives Considered
- Server CSV endpoint fetched via `ApiClient` then saved as a Blob: same result, extra endpoint.
- Server CSV endpoint with `?ApiKey=`: puts a token in a URL.

## Consequences
CSV always matches what the table shows. Export logic lives in JS, so it is verified in the
browser (AC-12, AC-14) rather than by C# unit tests.
