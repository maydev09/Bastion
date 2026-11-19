# 0001. Use trunk-based development

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Bastion is developed by a single maintainer with AI assistance. The goal is high
velocity: ship small increments often and release new versions quickly. The product
is a web application, so there is only ever one live version to support; we never
need to maintain several release lines in parallel.

## Decision

We use trunk-based development with `master` as the trunk.

- `master` is always releasable: it builds and all tests pass.
- All work happens on short-lived branches cut from `master`, named
  `feature-<name>`, `fix-<name>`, `docs-<name>` or `chore-<name>`.
  A branch should live hours or a few days, not weeks.
- Branches are merged back into `master` through a pull request and deleted afterwards.
- Unfinished functionality is merged early and hidden behind a feature flag
  instead of being kept on a long-lived branch.
- Releases are cut from `master`.

## Alternatives considered

- **GitFlow** (`master` + `develop` + `feature/*` + `release/*` + `hotfix/*`):
  designed for versioned software with several supported releases. For a single
  developer shipping one web application it adds ceremony (an extra integration
  branch, release and hotfix branches, double merges) without real benefit, and
  long-lived branches cause bigger merge conflicts.
- **Committing directly to `master`:** fastest, but loses the PR as a checkpoint
  for CI and review, and leaves a less readable history for a public portfolio repo.

## Consequences

- Merges stay small and conflicts rare; changes reach `master` quickly.
- `master` must be protected: pull requests and a green CI build are required.
- We need a feature flag mechanism early, so that half-finished work can be merged safely.
- Every pull request must keep `master` releasable, so CI (build + tests) is a prerequisite.
