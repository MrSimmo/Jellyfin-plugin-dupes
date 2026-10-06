# Kickoff prompt: implement Duplicate & Unmatched Finder

Start a new Claude Code session in this repository on **Opus 5.5** (`/model`). Fill in the three bracketed lines, then paste everything below the rule. The filled-in values stay in the conversation; this file keeps only the placeholders (spec DF-C4).

---

Implement the Duplicate & Unmatched Finder Jellyfin plugin by executing `docs/superpowers/plans/2026-10-06-dupe-finder.md` with superpowers:subagent-driven-development.

Test server URL: [JF_URL]
Admin user: [admin user name]   Non-admin user: [non-admin user name]   Passwords: [none / provide]

These values are only ever passed as environment variables on the command line (`JF_URL`, `JF_ADMIN_USER`, `JF_ADMIN_PW`, `JF_NONADMIN_USER`, `JF_NONADMIN_PW`). Never write them, or anything about these accounts, into any file, script, log, commit message, PROGRESS.md or memory (spec DF-C4).

Before anything else:
1. Do the session-start reads my global CLAUDE.md requires: the project `CLAUDE.md` (it is created in Task 1, so it is absent until then), `PROGRESS.md`, `specs/dupe-finder.md`, `specs/design.md`, `docs/adr/`, `docs/lessons/` and the plan. Print the confirmation line.
2. Ask me once to approve every install listed in Task 1 Step 1: the .NET 10 SDK into `~/.dotnet`, the named NuGet packages, and later Playwright plus Chromium in `tests/e2e`. Then stop and wait for my answer.

How to run it:
- **Roles.** You are the controller. For each task, dispatch one implementer subagent with `model: "opus"`, giving it the task's full text, the plan's Global Constraints and Review Focus, and the spec sections the task cites. Then dispatch a fresh reviewer subagent with `model: "opus"` to check that task against the spec and its acceptance criteria before you move on.
- **Order.** The tasks are sequential, because each one uses the interfaces of the one before. Do not run implementers in parallel.
- **Your own steps.** Steps marked 🧑 CONTROLLER + USER are yours alone:
  - approvals;
  - starting `scripts/serve-repo.sh` in the background;
  - asking me to add the repository, install or update the plugin, and restart Jellyfin;
  - running the live checks with the env vars above.

  When a step needs me, ask, end the turn and wait. Never guess my answer.
- **Credentials.** Never write the server URL or account names into a subagent's instructions file. Only the agent running a live check (Tasks 5, 7 and 8) receives them, inside the command it runs.
- **Evidence.** Back every "pass" with pasted command output. If a check fails, report it as failed and debug it with superpowers:systematic-debugging. Never make a check pass by weakening its assertion.
- **Spec conflicts.** If Jellyfin 12.2 behaves differently from the plan, or code would contradict the spec, stop. Amend the spec first (record it in §8 Amendments, plus an ADR if it is a decision), then continue.
- **Commits.** Commit after each task, with spec IDs in the message. Do not push; I will push to the remote.
- **Finish.** At the end of Task 8, update `PROGRESS.md`. Then give me a summary I can read cold covering:
  - what was built;
  - the status of AC-1 to AC-14, with evidence;
  - anything still `[!]` or `[~]`;
  - the repository URL to remove from Jellyfin, if I no longer want it.
