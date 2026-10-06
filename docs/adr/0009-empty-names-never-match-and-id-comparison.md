# 0009. Empty normalised names never match; provider ids compare trimmed and case-insensitively

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-R2.3, DF-R3.1, DF-R3.2, DF-R3.4; amendment §8)

## Context
Normalisation (spec §4) keeps only letters and digits, so names such as `""`, `!!!` or `???`
normalise to the empty string. Read literally, DF-R3.1(c) would then match every unmatched item
with an empty normalised name and the same year, and collapse them into one large false
"duplicate" group; DF-R3.4 had the same gap for album artist + album. The approved plan already
excluded these (Review Focus 1, pinned by `Movies_UnmatchedWithBlankOrPunctuationNames_AreNeverGrouped`),
but the spec did not say so. The plan's implementation also compares every provider id after
trimming and ignoring case, while the spec mentioned case-insensitivity for IMDb only.

## Decision
An empty normalised name, or an empty normalised joined album-artist string, produces no title
key and never matches. Every provider id value is trimmed and compared case-insensitively, and
findings report the trimmed values.

## Alternatives Considered
- Group empty names literally as the old wording allowed: produces one meaningless group of
  every unnamed item.
- Compare TMDb, TVDb and MusicBrainz ids case-sensitively: no practical difference (numeric or
  lower-case UUID values), and a separate rule per key adds code for nothing.

## Consequences
Items whose names contain no letters or digits are never reported as title or album-name
duplicates; they can still group by provider id, and still appear in the unmatched, incomplete
and merged-versions checks. Ids differing only by surrounding whitespace or letter case are treated as the same id.
