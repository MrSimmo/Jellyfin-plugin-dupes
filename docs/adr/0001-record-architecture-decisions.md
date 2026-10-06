# 0001. Record architecture decisions

Date: 2026-10-06
Status: Accepted

## Context
Decisions about this plugin (platform workarounds, delivery, rejected approaches) need to survive
between sessions; the spec records what is built, not why or what was ruled out.

## Decision
Record decisions as ADRs in `docs/adr/NNNN-short-title.md` using the Context / Decision /
Alternatives Considered / Consequences layout. Code comments cite them as `ADR-NNNN`.
Changing a decision means a new ADR that supersedes the old one.

## Alternatives Considered
- Decisions in commit messages only: not discoverable when resuming cold.
- Decisions inline in the spec: mixes behaviour with rationale; spec stays behaviour-only.

## Consequences
Small overhead per decision; `specs/dupe-finder.md` stays a pure behaviour contract.
