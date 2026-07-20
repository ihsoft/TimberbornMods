# Repository Coordination Operational Knowledge

## Purpose

Prevent agents sharing one local machine and repository workspace from colliding during operations that require
exclusive ownership of Unity execution or the Git index and history.

This document does not serialize ordinary reading, editing, diagnostics, or independent builds. It does not grant
permission to commit, publish, tag, open interactive applications, or change external state.

## Coordination Model

Use resource-scoped locks rather than one global repository lock:

| Resource | Exclusive scope |
|---|---|
| `unity-project` | every agent-controlled Unity Editor or batch-mode process used for this repository's work, regardless of the opened project path |
| `git-transaction` | staging, validating the staged diff, creating a commit, and verifying the resulting commit |

The tracked helper `tools/repository-coordination.psm1` owns the protocol. It combines a process-owned named mutex with
an ignored diagnostic record under `.tools/repository-locks/` in the main repository.

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

Before any agent-controlled Unity Editor or batch-mode launch for this repository's work, acquire `unity-project`
through the coordination helper in the main `<repo-root>`. This includes `ModsUnityProject`, a temporary checkout, and
a separately cloned third-party Unity project. The lock coordinates Unity use on the shared machine; an independent
project path is not exempt.

The mutex identity is derived from the `RepositoryRoot` passed to the helper. Always pass the main `<repo-root>` even
when Unity will open a project elsewhere. Using the temporary or cloned project as `RepositoryRoot` creates a different
mutex and does not satisfy this coordination contract.

The repository export wrapper acquires this lock automatically. Keep Unity's own project lockfile and process checks
as a second guard: they detect a manually opened Editor or a surviving Unity child process even when that process did
not participate in repository coordination.

Do not acquire separate locks per mod or Unity project. Repository work on this shared machine uses one
`unity-project` resource so imports, compilation, package resolution, and exports remain mutually exclusive.

Set command observation and monitoring timeouts long enough for the expected first import or export. A tool timeout
does not prove that the owner or Unity child process stopped. Before retrying, inspect the coordination record and
owner PID, relevant Unity processes, and the opened project's native lockfile. Do not launch another Unity instance
until ownership and process state show that the previous operation ended.

## Git Commit Transactions

Read-only Git operations do not require the coordination lock. Ordinary file editing also remains unlocked.

Before asking the helper to commit, inspect the complete intended working-tree diff and obtain any user authorization
required by the current task. On Windows, use the public `tools/commit-repository-changes.cmd` entrypoint with an exact
list of files. The helper holds `git-transaction` across the complete mutation window:

1. Verify the expected `HEAD` when the caller supplied one.
2. Reject any pre-existing staged paths rather than altering another task's index state.
3. Recheck that every requested exact file has a current change.
4. Stage only those files.
5. Run `git diff --cached --check` and show the staged summary.
6. Create the commit.
7. Verify that `HEAD` changed, the commit contains exactly the staged paths, and the index is clean afterward.

Example:

```powershell
tools/commit-repository-changes.cmd `
  -Path "AGENTS.md,docs/agent-knowledge/Repository-Coordination-Operational-Knowledge-v2.md" `
  -Message "Coordinate shared repository operations" `
  -Owner "mentor-thread"
```

The `.cmd` entrypoint invokes the adjacent tracked `.ps1` implementation with a process-scoped
`-ExecutionPolicy Bypass`, forwards its arguments and exit code, and does not change machine or user policy. In Windows
agent workflows, do not invoke `commit-repository-changes.ps1` directly, manually reconstruct its PowerShell command,
or change persistent ExecutionPolicy. Pass multiple exact paths as one quoted comma-separated `-Path` value.

ExecutionPolicy and environment sandbox authorization are separate concerns. The wrapper prevents the predictable
direct-script policy failure; it does not grant permission to write the Git index or bypass an environment's approval
boundary. Request the normal scoped Git-mutation authorization when the agent environment requires it.

Do not pass a directory or broad pathspec. The helper intentionally requires exact files. If it finds foreign staged
content, stop and coordinate; do not unstage, commit, or otherwise repair another task's index state.

Git's `.git/index.lock` remains a low-level protection for one Git command. It does not replace the repository lock,
which covers the whole multi-command commit transaction.

## Boundaries

- A lock grants temporary resource ownership, not authorization for the operation itself.
- Do not hold a lock while waiting for user review or real-game validation.
- Keep the Git transaction short; perform research and semantic diff review before acquiring it.
- Do not create locks for ordinary work unless a repeated collision establishes a genuinely shared resource.
- Separate worktrees may isolate ordinary file and index changes; separate Unity projects do not exempt repository work
  from the shared-machine Unity lock.
