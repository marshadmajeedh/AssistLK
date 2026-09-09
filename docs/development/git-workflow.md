# AssistLK Team Git Workflow & Branching Strategy

**Status:** Authoritative Team Workflow Standard  
**Applies To:** All 4 team members across all components  
**Related Documents:** [Component Ownership](../COMPONENT_OWNERSHIP.md), [Testing Guide](testing-guide.md)

---

## 1. Branching Topology

AssistLK uses a unified Git workflow where `develop` is the shared integration trunk:

```text
develop (Authoritative Trunk)
  │
  ├── feature/c2-provider-matching-agent   (Member 2)
  ├── feature/c2-provider-onboarding-ui    (Member 2)
  ├── feature/c3-quotation-service         (Member 3)
  ├── feature/c3-booking-screen            (Member 3)
  ├── feature/c4-service-tracking-api      (Member 4)
  └── feature/c4-safety-validation-agent   (Member 4)
```

---

## 2. Core Rules for Team Members

### 1. Always Branch From Latest `develop`
```bash
git checkout develop
git pull origin develop
git checkout -b feature/component-X-short-description
```

### 2. Never Build on Another Member's Unfinished Feature Branch
- Do not branch off another developer's unmerged feature branch.
- If you depend on contracts from another component (e.g. Component 2 depending on Component 1's `ReadyForMatching`), branch from `develop` where Component 1's contracts have already been merged.

### 3. Sync With `develop` Before PR
Before submitting your PR for final review, rebase or merge the latest `develop` into your feature branch to resolve any merge conflicts locally:
```bash
git checkout develop
git pull origin develop
git checkout feature/component-X-short-description
git merge develop   # or git rebase develop
```

### 4. Run All Quality Gates Locally
Never open a PR with failing tests or lint errors:
```bash
# Backend
dotnet test backend/AssistLK.sln

# Web
cd web && npm test && npm run lint && npm run build

# Mobile (if touched)
cd mobile && flutter test
```

### 5. Never Commit Secrets or Build Artifacts
Strictly verify your `git status` before committing. Never add:
- ❌ `GOOGLE_API_KEY` or Gemini secrets
- ❌ PostgreSQL connection passwords
- ❌ JWT signing keys
- ❌ `.env` or `.env.local` files
- ❌ `bin/`, `obj/`, `node_modules/`, `build/`, `.dart_tool/`

---

## 3. Commit Message Conventions

AssistLK follows the **Conventional Commits** specification:

```text
<type>(<scope>): <short imperative description>
```

### Allowed Types:
- `feat`: A new user-facing feature or domain capability.
- `fix`: A bug fix in existing code.
- `test`: Adding or updating automated tests.
- `refactor`: Code change that neither fixes a bug nor adds a feature.
- `docs`: Documentation updates or guides.
- `chore`: Build scripts, dependencies, or tool configuration.

### Scope Tags:
- `c1`: Component 1 (Service Request & Problem Understanding)
- `c2`: Component 2 (Provider Management & Matching)
- `c3`: Component 3 (Quotation, Booking & Coordination)
- `c4`: Component 4 (Tracking, Completion & Safety)
- `agents`: Shared agent infrastructure or tools
- `web`: React frontend
- `mobile`: Flutter mobile app
- `backend`: ASP.NET Core API or database infrastructure

### Examples of Good Commit Messages:
- `feat(c2): implement provider distance calculation tool`
- `feat(c3): add quotation status state machine in application service`
- `fix(c1): correct confidence score boundary validation in workflow`
- `test(c4): add unit tests for validation safety agent completion rules`
- `docs(api): publish component 2 to 3 quotation handoff contract`
- `chore(deps): update npm packages in web project`

---

## 4. Pull Request Checklist

When opening a PR targeting `develop`:
1. **Title:** Use conventional commit style (e.g. `feat(c2): provider matching engine`).
2. **Component Label:** Tag the PR with your component tag (`Component 1`, `Component 2`, etc.).
3. **Automated Verification:** Confirm all CI checks pass.
4. **Peer Review:** Request review from at least one other team member.
5. **Merge Strategy:** Prefer **Squash and Merge** or **Rebase and Merge** to keep the `develop` commit history clean.
