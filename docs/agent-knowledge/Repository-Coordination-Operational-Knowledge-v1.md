# Repository Coordination Operational Knowledge

## Purpose

Prevent agents sharing one local checkout from colliding during operations that require exclusive ownership of the
Unity project or Git index and history.

This document does not serialize ordinary reading, editing, diagnostics, or independent builds. It does not grant
permission to commit, publish, tag, open interactive applications, or change external state.

## Coordination Model

Use resource-scoped locks rather than one global repository lock:

| Resource | Exclusive scope |
|---|---|
| `unity-project` | opening the shared Unity project, importing, resolving packages, compiling in Unity, and exporting |
| `git-transaction` | staging, validating the staged diff, creating a commit, and verifying the resulting commit |

The tracked helper `tools/repository-coordination.psm1` owns the protocol. It combines a process-owned named mutex with
an ignored diagnostic record under `.tools/repository-locks/`.

The mutex is authoritative. The JSON record exists so another agent or the user can see the owner, operation, process,
host, acquisition time, and ownership token. Do not infer that the resource is free merely because the JSON record is
missing, and do not delete a record while another process owns the mutex.

## Acquisition And Recovery

Acquire the lock before checking whether the exclusive operation can start. Keep it until the operation and its
immediate verification finish. On contention, fail or report the recorded owner instead of waiting indefinitely.

The helper releases the mutex in `finally`. If the owner process terminates unexpectedly, the operating system releases
its ownership. A surviving named mutex is reported as abandoned; if no handle remains, the next caller creates or
acquires the mutex normally and recognizes the leftover JSON as an orphaned diagnostic record. In either case, a
record without a mutex owner is diagnostic debris, not an active lease.

Do not break a lock based only on elapsed time. A live but slow or hung operation still owns the resource. Report its
owner and process; terminate or override it only with explicit user direction. The ownership token prevents an old
owner from deleting a newer owner's record.

## Unity Operations

Acquire `unity-project` before starting any agent-controlled interactive or batch Unity operation against
`ModsUnityProject`, including first-open import, package resolution, compilation, and export.

The repository export wrapper acquires this lock automatically. Keep Unity's own `Temp/UnityLockfile` and process
checks as a second guard: they detect a manually opened Editor or a surviving Unity child process even when that
process did not participate in repository coordination.

Do not acquire separate locks per mod. Unity imports, compiles, and writes shared project state, so exports from the
same project remain mutually exclusive even when they target different packages.

## Git Commit Transactions

Read-only Git operations do not require the coordination lock. Ordinary file editing also remains unlocked.

Before asking the helper to commit, inspect the complete intended working-tree diff and obtain any user authorization
required by the current task. Then use `tools/commit-repository-changes.ps1` with an exact list of files. The helper
holds `git-transaction` across the complete mutation window:

1. Verify the expected `HEAD` when the caller supplied one.
2. Reject any pre-existing staged paths rather than altering another task's index state.
3. Recheck that every requested exact file has a current change.
4. Stage only those files.
5. Run `git diff --cached --check` and show the staged summary.
6. Create the commit.
7. Verify that `HEAD` changed, the commit contains exactly the staged paths, and the index is clean afterward.

Example:

```powershell
tools/commit-repository-changes.ps1 `
  -Path AGENTS.md,docs/agent-knowledge/Repository-Coordination-Operational-Knowledge-v1.md `
  -Message "Coordinate shared repository operations" `
  -Owner "mentor-thread"
```

Do not pass a directory or broad pathspec. The helper intentionally requires exact files. If it finds foreign staged
content, stop and coordinate; do not unstage, commit, or otherwise repair another task's index state.

Git's `.git/index.lock` remains a low-level protection for one Git command. It does not replace the repository lock,
which covers the whole multi-command commit transaction.

## Boundaries

- A lock grants temporary resource ownership, not authorization for the operation itself.
- Do not hold a lock while waiting for user review or real-game validation.
- Keep the Git transaction short; perform research and semantic diff review before acquiring it.
- Do not create locks for ordinary work unless a repeated collision establishes a genuinely shared resource.
- Separate worktrees may isolate ordinary file and index changes, but they do not make one shared Unity project safe to
  run concurrently.
