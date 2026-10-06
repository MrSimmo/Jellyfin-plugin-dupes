# 0008. Follow Jellyfin 12.2's data model and JSON conventions

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (§4 Item set, DF-R2; amendment §8)

## Context
Planning against the `v12.2` source found three places where the approved spec assumed the wrong
platform behaviour:
- Jellyfin stores auto-detected alternate versions as owned items (`LibraryManager.cs:534`
  sets `OwnerId` to the primary), and the default query drops owned non-extra items even with
  `IncludeAlternateVersions = true` (`BaseItemRepository.TranslateQuery.cs:798-811`). Excluding
  every owned item would hide exactly the versions DF-R3.5 must report.
- Jellyfin's MVC JSON options use `JsonDefaults.PascalCaseOptions` (no naming policy, string
  enums, `WhenWritingNull`), so plugin controllers emit PascalCase and omit nulls unless every
  property is attributed.
- With `AnalysisMode=AllEnabledByDefault`, CA1721 rejects a public `Type` property on a type that
  also has `GetType()`.

## Decision
Query with `IncludeOwnedItems = true` and drop extras (`ExtraType` set) and additional parts
(owned, no `PrimaryVersionId`) in plugin code. Use Jellyfin's JSON conventions as-is (PascalCase,
null properties omitted, enums as strings) and return validation errors with
`ControllerBase.Problem(detail, 400)`. Name the finding's type field `ItemType`.

## Alternatives Considered
- `[JsonPropertyName]` on every DTO property to keep camelCase: noise on every property, and the
  page would differ from every other Jellyfin API it sees.
- Plain-text 400 bodies: content type depends on Jellyfin's registered output formatters;
  problem details is deterministic JSON.
- Suppress CA1721: keeps a misleading name purely to match a draft.

## Consequences
The page reads PascalCase fields and treats missing properties as null. Including owned items
returns extras too, so the extra/part filter in `JellyfinLibraryItemSource` is load-bearing and
is checked live by AC-11.
