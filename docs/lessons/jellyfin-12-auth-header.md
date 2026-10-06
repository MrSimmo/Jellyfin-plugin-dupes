Jellyfin 12.x rejects the legacy `X-Emby-Authorization` / `X-Emby-Token` headers and `?api_key=`; use `Authorization: MediaBrowser ...`.

When scripting against the test server (curl, Python, Playwright helpers), authenticate with
`POST /Users/AuthenticateByName` and send
`Authorization: MediaBrowser Client="x", Device="x", DeviceId="x", Version="x"` (append
`, Token="<token>"` afterwards). The legacy header returns `Error processing request.`, which looks
like a bad password but is not. `?ApiKey=` (new casing) still works for query-string auth.
Observed 2026-10-06 against 12.2.0; source: research of `v12.2` auth handler.
