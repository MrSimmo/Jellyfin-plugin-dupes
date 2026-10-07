Node decides whether a `.js` file is an ES module from the nearest `package.json` above it, even one outside the repository; `src/Jellyfin.Plugin.DupeFinder/Web/package.json` pins `"type": "module"` for that reason.

`tests/web/dupefinder.test.mjs` imports named exports from `Web/dupefinder.js`. When the repository was cloned under
a directory whose own `package.json` declared `"type": "commonjs"`, Node 24 loaded `dupefinder.js` as CommonJS and the
imports failed, although the same code passed in a clean directory. Keep the 3-line `Web/package.json`; it is not
embedded in the plugin DLL and has no effect on Jellyfin, which serves the file as a plugin page (ADR-0007).
Observed 2026-10-06 with Node 24.16.
