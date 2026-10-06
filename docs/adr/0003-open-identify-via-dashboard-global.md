# 0003. Open Jellyfin's Identify dialog via the `Dashboard.itemIdentifier` global

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-R7.2)

## Context
The nice-to-have is to bring up Jellyfin's matching (Identify) box for unmatched items. The dialog
is bundled inside jellyfin-web, but in `v12.2` `src/utils/dashboard.js` imports it (line 17),
places it on the `Dashboard` object (line 261) and assigns `window.Dashboard` (line 267), marked
"TODO: Remove once plugins don't need it". `itemidentifier.show(itemId, serverId)` returns a
promise that resolves on apply and rejects on cancel. jellyfin-web offers Identify only for
Movie, Trailer, Series, BoxSet, Person, Book, MusicAlbum, MusicArtist and MusicVideo.

## Decision
The page calls `Dashboard.itemIdentifier.show(itemId, ApiClient.serverId())` for Movie, Series,
MusicAlbum and MusicArtist rows. The button is hidden if `Dashboard.itemIdentifier` is missing at
run time. Every row also offers "Edit metadata" (`#/metadata?id=<id>`, new tab) and "Open"
(`#/details?id=<id>&serverId=<serverId>`, new tab).

## Alternatives Considered
- Re-implement Identify with `POST /Items/RemoteSearch/{Type}` and
  `POST /Items/RemoteSearch/Apply/{id}`: duplicates a working Jellyfin UI; much more code.
- Link to the details page only: works, but one extra click and loses the "jump straight in" ask.

## Consequences
Relies on an undocumented global that jellyfin-web intends to remove eventually; the feature
check degrades to Edit metadata / Open. Episodes cannot be identified (Jellyfin limitation).
Verified from source only; runtime confirmation is acceptance criterion AC-13.
