# Git Workflow

## Start a task

```bash
git checkout develop
git pull origin develop
git checkout -b feature/c1-short-task
```

## Commit

```bash
git add .
git commit -m "feat(c1): short meaningful description"
```

## Push

```bash
git push -u origin feature/c1-short-task
```

## Pull Request

Open:

```text
feature/c1-short-task → develop
```

After review and successful tests, merge.

## After merge

```bash
git checkout develop
git pull origin develop
git branch -d feature/c1-short-task
```

Create a new branch for the next task.
