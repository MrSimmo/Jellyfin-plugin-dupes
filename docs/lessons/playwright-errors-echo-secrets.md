Playwright error messages contain the page URL and the literal value passed to `fill()`, so live-check output can leak the server address or a password (spec DF-C4) unless it is redacted.

`tests/e2e/ui-check.mjs` wraps the run in try/catch and prints one `ERROR` line with the base URL, user names and
passwords (raw and JSON-escaped) replaced by placeholders, and no stack. When adding steps to it, keep every failure
inside that catch, keep stderr out of saved logs, and never paste raw Playwright tracebacks into files.
Observed 2026-10-06 with Playwright 1.63.
