# Contributing to AssistLK

Read the root `README.md` first.

## Standard workflow

```bash
git checkout develop
git pull origin develop
git checkout -b feature/cX-short-task
```

Work, test, and commit:

```bash
git add .
git commit -m "feat(cX): describe the feature"
git push -u origin feature/cX-short-task
```

Open a Pull Request to `develop`.

## Branch prefixes

- `feature/` – new functionality
- `fix/` – bug fix
- `test/` – testing work
- `docs/` – documentation
- `refactor/` – code restructuring without changing behaviour
- `chore/` – maintenance/configuration

## Component scopes

- `c1` – Service Request & Problem Management
- `c2` – Provider Management & Intelligent Matching
- `c3` – Quotation, Booking & Service Coordination
- `c4` – Tracking, Completion & Feedback
- `shared` – approved shared work

## Pull Requests

PRs should:

- target `develop`,
- have a clear title,
- link to an issue,
- explain what changed,
- list tests performed,
- include screenshots for UI changes when useful,
- mention database migrations,
- mention API contract changes,
- mention AI workflow changes,
- contain no secrets.

## Review

At least one teammate should review a PR before merge.

Do not approve code you have not read.

## Coursework evidence

Keep regular meaningful commits, issues, PRs, reviews and tests. Every member must be able to explain and modify their contribution during the viva.
