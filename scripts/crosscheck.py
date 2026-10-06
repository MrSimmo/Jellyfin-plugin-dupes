#!/usr/bin/env python3
"""Live API checks for the Duplicate & Unmatched Finder plugin (AC-7, AC-9, AC-10, AC-11).

Configuration comes only from environment variables and is never written anywhere (DF-C4):
  JF_URL            server base URL
  JF_ADMIN_USER     administrator user name       JF_ADMIN_PW     (optional, default empty)
  JF_NONADMIN_USER  non-administrator user name   JF_NONADMIN_PW  (optional, default empty)
  EXPECT_VERSION    optional: plugin version that must be installed and Active (AC-7)
Exits 1 if any check fails.
"""
import json
import os
import sys
import urllib.error
import urllib.request
from collections import defaultdict

BASE = os.environ["JF_URL"].rstrip("/")
CLIENT = 'MediaBrowser Client="DupeFinderCheck", Device="crosscheck", DeviceId="dupefinder-crosscheck", Version="1.0"'
PLUGIN_NAME = "Duplicate & Unmatched Finder"
# Collections and playlists hold links, not their own media; Jellyfin's REST API follows links there
# and the plugin (by design) does not, so they are not comparable.
SKIP_TYPES = {"boxsets", "playlists"}
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message)
    if not ok:
        failures.append(message)


def call(method, path, token=None, body=None):
    # Jellyfin 12.x accepts only the standard Authorization header (docs/lessons/jellyfin-12-auth-header.md).
    headers = {"Authorization": CLIENT + (f', Token="{token}"' if token else "")}
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=900) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        try:
            return error.code, json.loads(raw)
        except ValueError:
            return error.code, raw.decode("utf-8", "replace")


def login(user, password):
    status, body = call("POST", "/Users/AuthenticateByName", body={"Username": user, "Pw": password})
    if status != 200:
        sys.exit(f"login failed: HTTP {status}")
    return body["AccessToken"], body["User"]["Id"]


def unmatched(item):
    return not any((value or "").strip() for value in (item.get("ProviderIds") or {}).values())


def norm(item_id):
    return item_id.replace("-", "").lower()


def check_plugin(admin):
    status, plugins = call("GET", "/Plugins", admin)
    mine = [p for p in plugins or [] if p.get("Name") == PLUGIN_NAME]
    check(status == 200 and len(mine) == 1, f"plugin installed once: {[(p.get('Version'), p.get('Status')) for p in mine]}")
    expected = os.environ.get("EXPECT_VERSION")
    if expected and mine:
        check(mine[0].get("Version") == expected and mine[0].get("Status") == "Active", f"AC-7 version {expected} Active")


def check_access(admin, nonadmin, library_id):
    body = {"LibraryIds": [library_id], "Checks": ["UnmatchedMoviesSeries"]}
    for label, token, expected in (("admin", admin, 200), ("non-admin", nonadmin, 403), ("anonymous", None, 401)):
        status, _ = call("GET", "/DupeFinder/Libraries", token)
        check(status == expected, f"AC-9 GET /DupeFinder/Libraries as {label} -> {status} (want {expected})")
        status, _ = call("POST", "/DupeFinder/Scan", token, body)
        check(status == expected, f"AC-9 POST /DupeFinder/Scan as {label} -> {status} (want {expected})")


def check_validation(admin, library_id):
    cases = (
        ({"LibraryIds": [], "Checks": ["DuplicateMovies"]}, "Select at least one library."),
        ({"LibraryIds": ["not-a-library"], "Checks": ["DuplicateMovies"]}, "Unknown library id 'not-a-library'."),
        ({"LibraryIds": [library_id], "Checks": []}, "Select at least one check."),
        ({"LibraryIds": [library_id], "Checks": ["Bogus"]}, "Unknown check 'Bogus'."),
    )
    for body, detail in cases:
        status, payload = call("POST", "/DupeFinder/Scan", admin, body)
        got = payload.get("detail") if isinstance(payload, dict) else payload
        check(status == 400 and got == detail, f"AC-10 {json.dumps(body)} -> {status} {got!r}")


def rest_items(admin, user_id, path):
    # Explicit false: otherwise /Items folds movies and series in collections into BoxSet items whenever the
    # server groups them into collections (Folder.CollapseBoxSetItems). /Artists ignores the parameter.
    status, body = call("GET", path + f"&userId={user_id}&Fields=ProviderIds&CollapseBoxSetItems=false", admin)
    if status != 200:
        check(False, f"REST {path} -> HTTP {status}")
        return []
    return body["Items"]


def check_counts(admin, user_id, libraries):
    for library in libraries:
        lid, name = library["Id"], library["Name"]
        status, scan = call("POST", "/DupeFinder/Scan", admin, {"LibraryIds": [lid], "Checks": ["UnmatchedMoviesSeries", "UnmatchedMusic"]})
        if status != 200:
            check(False, f"AC-11 scan of {name} -> HTTP {status}")
            continue
        movies_series = rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=Movie,Series")
        albums = rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=MusicAlbum")
        artists = rest_items(admin, user_id, f"/Artists/AlbumArtists?ParentId={lid}")
        rest_ms = sum(unmatched(i) for i in movies_series)
        rest_music = sum(unmatched(i) for i in albums) + sum(unmatched(i) for i in artists)
        plugin_ms = scan["Summary"]["UnmatchedMoviesSeries"]
        plugin_music = scan["Summary"]["UnmatchedMusic"]
        check(plugin_ms == rest_ms, f"AC-11 {name}: unmatched movies+series plugin={plugin_ms} rest={rest_ms}")
        check(plugin_music == rest_music, f"AC-11 {name}: unmatched albums+artists plugin={plugin_music} rest={rest_music}")


def check_tmdb_groups(admin, user_id, libraries):
    ids = [library["Id"] for library in libraries]
    status, scan = call("POST", "/DupeFinder/Scan", admin, {"LibraryIds": ids, "Checks": ["DuplicateMovies"]})
    if status != 200:
        check(False, f"AC-11 duplicate scan -> HTTP {status}")
        return
    group_of = {norm(f["ItemId"]): f.get("Group") for f in scan["Findings"]}
    by_tmdb = defaultdict(set)
    for lid in ids:
        for movie in rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=Movie"):
            # ADR-0009: provider keys and ids compare trimmed and case-insensitively, as the plugin does;
            # the first non-blank value under any key equal to "tmdb" counts.
            tmdb = next((value.strip().lower() for key, value in (movie.get("ProviderIds") or {}).items()
                         if key.lower() == "tmdb" and (value or "").strip()), "")
            if tmdb:
                by_tmdb[tmdb].add(norm(movie["Id"]))
    shared = {tmdb: items for tmdb, items in by_tmdb.items() if len(items) > 1}
    print(f"INFO {len(shared)} TMDb ids shared by 2+ movies; plugin reported {scan['Summary']['DuplicateMovies']} DuplicateMovies groups in {scan['DurationMs']} ms")
    # REST -> plugin: movies that REST lists under one TMDb id sit in one plugin group.
    for tmdb, items in sorted(shared.items()):
        groups = {group_of.get(item) for item in items}
        check(None not in groups and len(groups) == 1, f"AC-11 TMDb {tmdb}: {len(items)} movies share one plugin group {sorted(map(str, groups))}")
    # Plugin -> REST (AC-11 as written): every finding the plugin grouped by "Same TMDb id <id>" (DF-R3.6)
    # is a movie that REST lists under that TMDb id.
    prefix = "Same TMDb id "
    plugin_tmdb = defaultdict(set)
    for finding in scan["Findings"]:
        for reason in (finding.get("Reason") or "").split("; "):
            if reason.startswith(prefix):
                plugin_tmdb[reason[len(prefix):].strip().lower()].add(norm(finding["ItemId"]))
    for tmdb, items in sorted(plugin_tmdb.items()):
        missing = sorted(items - by_tmdb.get(tmdb, set()))
        check(not missing, f"AC-11 plugin TMDb {tmdb}: {len(items)} findings all listed by REST under that id; not listed: {missing}")


def run_checks(admin, admin_user_id, nonadmin):
    check_plugin(admin)
    status, libraries = call("GET", "/DupeFinder/Libraries", admin)
    check(status == 200 and bool(libraries), f"libraries listed: {[lib['Name'] for lib in libraries or []]}")
    if status != 200 or not libraries:
        return
    comparable = [lib for lib in libraries if (lib.get("CollectionType") or "").lower() not in SKIP_TYPES]
    check_access(admin, nonadmin, libraries[0]["Id"])
    check_validation(admin, libraries[0]["Id"])
    check_counts(admin, admin_user_id, comparable)
    check_tmdb_groups(admin, admin_user_id, comparable)


def main():
    admin, admin_user_id = login(os.environ["JF_ADMIN_USER"], os.environ.get("JF_ADMIN_PW", ""))
    nonadmin, _ = login(os.environ["JF_NONADMIN_USER"], os.environ.get("JF_NONADMIN_PW", ""))
    try:
        run_checks(admin, admin_user_id, nonadmin)
    finally:
        for token in (admin, nonadmin):
            call("POST", "/Sessions/Logout", token)
    print(f"{len(failures)} failure(s)")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
