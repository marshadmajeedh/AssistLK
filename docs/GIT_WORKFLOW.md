# Git Workflow & Standards

> **Authoritative Specification:** For complete team branching rules, conventional commit examples, and PR quality gates across all 4 team members, see **[Team Git Workflow](development/git-workflow.md)**.

## Quick Reference

### 1. Start Feature
```bash
git checkout develop
git pull origin develop
git checkout -b feature/component-X-short-task
```

### 2. Commit With Conventional Format
```bash
git add .
git commit -m "feat(cX): short meaningful description"
```

### 3. Local Verification Before PR
```bash
dotnet test backend/AssistLK.sln
cd web && npm test && npm run lint && npm run build
cd mobile && flutter test
```

### 4. Push & Open PR
```bash
git push -u origin feature/component-X-short-task
```
Target `develop` for all component feature PRs.
