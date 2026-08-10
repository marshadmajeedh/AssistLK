# AssistLK

**Agentic AI-Assisted Emergency & Skilled Service Coordination Platform**

> SE3090 – Software Engineering Frameworks  
> Year 3, Semester 1 – 2026  
> Group project: React + Flutter + ASP.NET Core Web API + PostgreSQL + Agentic AI

---

## 1. Start Here

AssistLK is **one integrated system**, not four separate mini-projects.

A customer can report a real service problem such as:

- Vehicle breakdown
- Plumbing problem
- Electrical problem
- Appliance repair problem

The system then:

1. captures the problem, photo and location,
2. understands the service request,
3. finds suitable verified and available providers,
4. coordinates provider acceptance and quotation,
5. waits for required human approvals,
6. assigns the provider,
7. tracks the service until completion,
8. stores feedback, history and AI execution evidence.

### Core architecture

```text
Flutter Mobile App
(Customer + Provider)
        |
        | HTTPS + JWT
        v
ASP.NET Core Web API  <------  React Web App
        |                     (Admin / Staff)
        |
        +------ PostgreSQL
        |
        +------ Agentic AI internal service
        |
        +------ Maps / Location service
```

**Important:** React and Flutter must use the **same ASP.NET Core API, PostgreSQL database, identity, permissions and business rules**.

If the Agentic AI is implemented as a separate Python service, React and Flutter must **not** call it directly. ASP.NET Core calls it internally.

---

## 2. Team Components

| Member | Primary Component | AI Contribution |
|---|---|---|
| Member 1 | Service Request & Problem Management | Problem Understanding Agent |
| Member 2 | Provider Management & Intelligent Matching | Provider Matching Agent |
| Member 3 | Quotation, Booking & Service Coordination | Service Coordination Agent |
| Member 4 | Service Tracking, Completion & Feedback | Validation & Safety Agent |

### Component 1 – Service Request & Problem Management

Main responsibility:

- Customer creates service request
- Description
- Service category
- Urgency
- Photo upload
- GPS location
- Request history
- Cancellation
- Admin request review
- AI problem classification

Main feature folders:

```text
backend/.../ServiceRequests/
web/src/features/serviceRequests/
mobile/lib/features/service_requests/
agent-service/agents/problem_understanding/
```

### Component 2 – Provider Management & Intelligent Matching

Main responsibility:

- Provider profile
- Skills
- Service categories
- Availability
- Service area
- Provider verification
- Provider search
- Provider matching
- Accept/reject request

Main feature folders:

```text
backend/.../Providers/
web/src/features/providers/
mobile/lib/features/providers/
agent-service/agents/provider_matching/
```

### Component 3 – Quotation, Booking & Service Coordination

Main responsibility:

- Provider accepts service request
- Provider creates quotation
- Customer approves/rejects quotation
- Revised quotation
- Booking confirmation
- Provider assignment
- Coordination history
- Human approval workflow

Main feature folders:

```text
backend/.../Quotations/
backend/.../Bookings/
web/src/features/quotations/
mobile/lib/features/quotations/
agent-service/agents/service_coordination/
```

### Component 4 – Service Tracking, Completion & Feedback

Main responsibility:

- Assigned
- On the way
- Arrived
- In progress
- Completed
- Completion evidence
- Customer confirmation
- Feedback/rating
- Complaints
- Reports
- Safety/business-rule validation

Main feature folders:

```text
backend/.../ServiceTracking/
web/src/features/tracking/
mobile/lib/features/tracking/
agent-service/agents/validation_safety/
```

---

# 3. Repository Structure

```text
AssistLK/
│
├── backend/
│   ├── src/
│   │   ├── AssistLK.Api/
│   │   │   ├── Controllers/
│   │   │   ├── DTOs/
│   │   │   ├── Middleware/
│   │   │   └── Features/
│   │   │       ├── ServiceRequests/
│   │   │       ├── Providers/
│   │   │       ├── Quotations/
│   │   │       ├── Bookings/
│   │   │       └── ServiceTracking/
│   │   │
│   │   ├── AssistLK.Application/
│   │   │   ├── Interfaces/
│   │   │   ├── Services/
│   │   │   └── Validators/
│   │   │
│   │   ├── AssistLK.Domain/
│   │   │   ├── Entities/
│   │   │   └── Enums/
│   │   │
│   │   └── AssistLK.Infrastructure/
│   │       ├── Data/
│   │       ├── Repositories/
│   │       └── ExternalServices/
│   │
│   └── tests/
│       ├── AssistLK.Api.Tests/
│       └── AssistLK.IntegrationTests/
│
├── web/
│   └── src/
│       ├── features/
│       │   ├── serviceRequests/
│       │   ├── providers/
│       │   ├── quotations/
│       │   ├── tracking/
│       │   └── aiWorkflows/
│       └── shared/
│
├── mobile/
│   └── lib/
│       ├── features/
│       │   ├── service_requests/
│       │   ├── providers/
│       │   ├── quotations/
│       │   └── tracking/
│       └── shared/
│
├── agent-service/
│   ├── agents/
│   │   ├── problem_understanding/
│   │   ├── provider_matching/
│   │   ├── service_coordination/
│   │   └── validation_safety/
│   ├── orchestration/
│   ├── tools/
│   ├── schemas/
│   └── tests/
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── COMPONENT_OWNERSHIP.md
│   ├── DEVELOPMENT_PHASES.md
│   ├── GIT_WORKFLOW.md
│   ├── API_CONVENTIONS.md
│   ├── AI_WORKFLOW.md
│   ├── ADR_TEMPLATE.md
│   └── AI_USAGE_LOG_TEMPLATE.md
│
├── .github/
│   ├── ISSUE_TEMPLATE/
│   ├── workflows/
│   └── pull_request_template.md
│
├── .env.example
├── .editorconfig
├── .gitignore
├── CONTRIBUTING.md
└── README.md
```

---

# 4. First-Time GitHub Setup

Only **one person** should do the first repository creation.

Recommended repository name:

```text
AssistLK
```

or:

```text
SE3090_AssistLK
```

## 4.1 Create repository on GitHub

Create an empty repository.

Recommended:

- Visibility: Private during development unless your lecturer requires otherwise.
- Do not upload secrets.
- Add all team members as collaborators.
- Keep `main` stable.

## 4.2 Push this template for the first time

Open a terminal inside this folder:

```bash
git init
git branch -M main
git add .
git commit -m "chore: initialize AssistLK repository structure"
git remote add origin https://github.com/YOUR-USERNAME/AssistLK.git
git push -u origin main
```

Create the development branch:

```bash
git checkout -b develop
git push -u origin develop
```

Then return to:

```bash
git checkout develop
```

---

# 5. Branch Strategy

We use:

```text
main
  |
  └── develop
       |
       ├── feature/c1-...
       ├── feature/c2-...
       ├── feature/c3-...
       ├── feature/c4-...
       ├── fix/...
       ├── test/...
       └── docs/...
```

## `main`

Purpose:

- Stable release
- Final/demo-ready code
- Do not develop directly here

## `develop`

Purpose:

- Integrated development version
- Completed and reviewed features are merged here

## Feature branches

Every task must use its own branch.

### Component 1

```text
feature/c1-create-service-request
feature/c1-photo-upload
feature/c1-request-history
feature/c1-problem-agent
```

### Component 2

```text
feature/c2-provider-profile
feature/c2-provider-availability
feature/c2-provider-verification
feature/c2-provider-matching
```

### Component 3

```text
feature/c3-create-quotation
feature/c3-quotation-approval
feature/c3-booking-confirmation
feature/c3-coordination-agent
```

### Component 4

```text
feature/c4-service-status
feature/c4-completion-feedback
feature/c4-complaints
feature/c4-validation-agent
```

Shared work:

```text
feature/shared-authentication
feature/shared-role-authorization
feature/shared-api-error-handler
feature/shared-database-foundation
```

---

# 🎨 Shared UI Design System

AssistLK is developed by multiple team members, but the final React and
Flutter applications must look like one consistent system.

For this reason, all members must use the shared AssistLK Design System.

The shared design system controls:

1. Colors
2. Typography
3. Spacing
4. Border Radius
5. Buttons
6. Input Fields
7. Cards
8. Status Colors

Do not create a separate theme inside your own component.

---

## 1. Shared Theme Locations

### React

All React theme files are located inside:

```text
web/src/shared/theme/
└── src/
    └── shared/
        └── theme/
            ├── colors.js
            ├── typography.js
            ├── spacing.js
            ├── radius.js
            ├── components.js
            └── index.js
```

### Flutter

All Flutter theme files are located inside:

```text
mobile/lib/shared/theme/
└── lib/
    └── shared/
        └── theme/
            ├── app_colors.dart
            ├── app_text_styles.dart
            ├── app_spacing.dart
            ├── app_radius.dart
            └── app_theme.dart
```

---

# 6. MOST IMPORTANT – How Every Member Should Push Code

## Never do this

```bash
git checkout main
# edit files
git push
```

Do not directly push unfinished features to `main`.

Also avoid directly developing on `develop`.

---

## 6.1 Before starting ANY new task

Always update your local `develop`:

```bash
git checkout develop
git pull origin develop
```

Then create a new branch.

---

## 6.2 Example – Member 1 / Component 1

Task:

> Create the service request API and Flutter form.

Start:

```bash
git checkout develop
git pull origin develop

git checkout -b feature/c1-create-service-request
```

Now work on the feature.

You may change files in more than one application because your component must be a **vertical slice**.

Example:

```text
backend/...
mobile/...
web/...
agent-service/...
tests/...
```

When part of the task is complete:

```bash
git status
git add .
git commit -m "feat(c1): add service request creation API"
```

Continue work:

```bash
git add .
git commit -m "feat(c1): add Flutter service request form"
```

Add tests:

```bash
git add .
git commit -m "test(c1): add service request validation tests"
```

Push your branch:

```bash
git push -u origin feature/c1-create-service-request
```

Then create a **Pull Request**:

```text
FROM: feature/c1-create-service-request
TO:   develop
```

Ask at least one teammate to review it.

After the Pull Request is approved and CI passes, merge it.

Then:

```bash
git checkout develop
git pull origin develop
```

Do **not** continue using the old feature branch for a different task.

For the next task create a new branch:

```bash
git checkout -b feature/c1-photo-upload
```

---

## 6.3 Member 2 example

```bash
git checkout develop
git pull origin develop

git checkout -b feature/c2-provider-availability
```

Work → test → commit:

```bash
git add .
git commit -m "feat(c2): add provider availability management"
```

Push:

```bash
git push -u origin feature/c2-provider-availability
```

Open PR:

```text
feature/c2-provider-availability → develop
```

---

## 6.4 Member 3 example

```bash
git checkout develop
git pull origin develop

git checkout -b feature/c3-quotation-approval
```

Commit examples:

```bash
git commit -m "feat(c3): add quotation creation service"
git commit -m "feat(c3): add customer quotation approval"
git commit -m "test(c3): cover quotation approval rules"
```

Push:

```bash
git push -u origin feature/c3-quotation-approval
```

Open PR to `develop`.

---

## 6.5 Member 4 example

```bash
git checkout develop
git pull origin develop

git checkout -b feature/c4-service-tracking
```

Commit examples:

```bash
git commit -m "feat(c4): add service status transition API"
git commit -m "feat(c4): add Flutter tracking timeline"
git commit -m "test(c4): reject invalid service status transitions"
```

Push:

```bash
git push -u origin feature/c4-service-tracking
```

Open PR to `develop`.

---

# 7. Commit Message Rules

Use:

```text
type(scope): short message
```

Examples:

```text
feat(c1): add service request creation
feat(c2): add provider matching endpoint
feat(c3): add quotation approval flow
feat(c4): add service completion tracking

fix(c2): exclude unavailable providers from matching
test(c3): add quotation rejection integration tests
docs(c1): document request cancellation rules
refactor(shared): simplify JWT authorization service
ci: add backend test workflow
```

## Do NOT use

```text
update
changes
final
new
working
done
my code
fix
```

Every commit should explain what changed.

---

# 8. Pull Request Rules

Every feature must enter `develop` through a Pull Request.

PR title example:

```text
[C1] Add service request creation workflow
```

Another:

```text
[C3] Add quotation approval and booking confirmation
```

Before requesting review:

- [ ] My branch started from latest `develop`
- [ ] Project builds
- [ ] Relevant tests pass
- [ ] No `.env`, passwords, API keys or database passwords committed
- [ ] API changes are documented
- [ ] React/Flutter errors and loading states are handled
- [ ] I tested my own feature
- [ ] I can explain the code in the viva
- [ ] AI-generated code was reviewed and understood
- [ ] My AI usage log is updated when required

Reviewer should check:

- business rule correctness,
- security/authorization,
- code readability,
- duplicate code,
- database effects,
- API contract,
- tests,
- integration effect on other components.

---

# 9. Recommended GitHub Protection

Protect important branches.

Recommended for `main`:

- Require Pull Request before merge
- Require at least 1 approval
- Require status checks to pass
- Block force pushes
- Block deletion

Recommended for `develop`:

- Require Pull Request
- Require at least 1 teammate review
- Require CI when the workflow is ready

No member should force-push shared branches.

---

# 10. Working With Shared Files

Some files belong to the whole team.

Examples:

```text
backend/.../Program.cs
database DbContext
authentication
role definitions
shared API response models
global exception middleware
web routing
Flutter routing
environment configuration
Agent orchestration
```

Before changing a shared file:

1. Tell the team.
2. Create an issue/task.
3. Pull latest `develop`.
4. Make the smallest necessary change.
5. Open a PR.
6. Ask another member to review.

Do not let two people redesign the same shared file at the same time without coordination.

---

# 11. What If `develop` Changed While I Was Working?

First save your work.

Then:

```bash
git checkout develop
git pull origin develop
```

Go back to your branch:

```bash
git checkout feature/c1-create-service-request
```

Merge the latest development branch:

```bash
git merge develop
```

Resolve conflicts carefully.

Then:

```bash
git add .
git commit -m "chore(c1): resolve develop integration conflicts"
git push
```

If you do not understand a conflict, do not randomly choose "Accept Current" or "Accept Incoming". Ask the teammate who changed the conflicting area.

---

# 12. Phase-by-Phase Development Plan

The team should **not** let every member build their component alone until the final week.

Build in phases and integrate continuously.

---

## Phase 0 – Repository + Planning

**Target: 10–12 Aug**

Tasks:

- Create GitHub repository
- Add collaborators
- Create `main` and `develop`
- Add branch rules
- Create GitHub Project board
- Confirm 4 components
- Confirm 3 user roles
- Confirm initial service categories
- Define naming standards
- Create initial ER diagram
- Define API conventions
- Decide React state management
- Decide Flutter state management
- Decide Agentic AI framework
- Start ADRs
- Start individual AI usage logs

Output:

```text
Repository ready
Project board ready
Architecture agreed
Component ownership agreed
```

---

## Phase 1 – Shared Foundation

**Target: 13–18 Aug**

Build shared foundations before feature work becomes large.

Backend:

- ASP.NET Core solution
- PostgreSQL connection
- EF Core
- Base migrations
- User model
- JWT authentication
- Role authorization
- Global error handling
- Swagger
- Logging
- CORS

React:

- Project setup
- Routing
- Login
- Protected routes
- Shared API client
- Basic layout

Flutter:

- Project setup
- Login/logout
- Secure token storage
- Routing
- Shared API client
- Basic customer/provider navigation

Agent service:

- Framework setup
- Workflow state schema
- Tool interface rules
- Structured-output schema
- Error/retry rules

Testing/CI:

- First backend unit test
- First integration test
- GitHub Actions backend CI

**Do not start four large isolated features before this foundation is stable.**

---

## Phase 2 – Build One Vertical Slice Per Component

**Target: 19 Aug – 1 Sep**

Each member now builds their component across the full stack.

### Member 1

Build:

```text
Database
→ ASP.NET service request API
→ Flutter create request
→ React request admin view
→ Agent 1
→ Tests
```

### Member 2

Build:

```text
Database
→ Provider API
→ Flutter provider profile/availability
→ React provider verification
→ Agent 2 matching
→ Tests
```

### Member 3

Build:

```text
Database
→ Quotation/booking API
→ Flutter quotation screens
→ React coordination/approval
→ Agent 3
→ Tests
```

### Member 4

Build:

```text
Database
→ Tracking/completion API
→ Flutter tracking
→ React complaints/reports
→ Agent 4
→ Tests
```

At the end of this phase, all four components should already be merged regularly into `develop`.

---

## Phase 3 – Agentic AI + Third-Party Integration

**Target: 2–10 Sep**

Complete the assessed AI workflow.

Required flow:

```text
Domain objective
    ↓
Structured multi-step plan
    ↓
Problem Understanding Agent
    ↓
Provider Matching Agent
    ↓
Allow-listed tool calls
    ↓
Validation & Safety Agent
    ↓
Human approval pause
    ↓
Service Coordination Agent
    ↓
Auditable result OR safe failure
```

Also integrate:

- GPS/location
- Maps/distance service
- Camera/image picker
- AI workflow persistence
- AI execution summary
- Tool-call logging
- Validation results
- Error/retry handling

---

## Phase 4 – Full Cross-Platform Integration

**Target: 11–17 Sep**

Build the complete demo workflow.

Example:

```text
Customer – Flutter
    ↓
Create plumbing request + photo + GPS
    ↓
ASP.NET Core
    ↓
PostgreSQL
    ↓
Agentic AI
    ↓
Provider recommendation
    ↓
Admin – React
    ↓
Approve / reject recommendation
    ↓
Provider – Flutter
    ↓
Accept + quotation
    ↓
Customer – Flutter
    ↓
Approve quotation
    ↓
Provider assigned
    ↓
On the Way
    ↓
Arrived
    ↓
In Progress
    ↓
Completed
    ↓
Customer feedback
```

Do not move to final documentation until this workflow works.

---

## Phase 5 – Testing + Security + Performance

**Target: 18–23 Sep**

Required work:

- Backend unit tests
- Controller/API tests
- PostgreSQL integration tests
- Authentication/authorization tests
- React component/form/API/error tests
- Flutter unit/widget/navigation/API tests
- End-to-end workflow test
- Performance testing
- Agent golden cases
- Prompt-injection resistance
- Safe-failure tests
- Human approval enforcement tests
- GitHub Actions stable

Every member must understand the tests related to their component.

---

## Phase 6 – Deployment + Documentation

**Target: 24–27 Sep**

Deploy:

- ASP.NET Core API
- PostgreSQL
- React
- Agent service if required
- Build Flutter APK

Document:

- README
- architecture
- Agentic AI architecture
- ER diagram
- API documentation
- testing evidence
- deployment evidence
- ADRs
- individual contributions
- Git/PR evidence
- AI usage logs
- AI reflection
- security considerations
- startup instructions
- test accounts

---

## Phase 7 – Demo + Viva + Release Freeze

**Target: 28–29 Sep**

Practice:

- 10-minute complete demo
- 20-minute viva questions
- Every member explains:
  - controller,
  - service,
  - DTO,
  - database relationship,
  - migration/index,
  - React feature,
  - Flutter feature,
  - AI agent,
  - tool,
  - state,
  - validation,
  - human approval,
  - tests,
  - CI,
  - Git evidence.

Every member should practice making a small code change without using external AI.

Freeze the final release after testing.

---

## Submission – 30 Sep 2026

Before submission:

- verify all links,
- verify APK,
- verify Swagger,
- verify deployed API,
- verify database,
- verify React site,
- verify AI startup,
- verify demo video permissions,
- verify repository access,
- verify consolidated report.

---

# 13. GitHub Project Board

Recommended columns:

```text
Backlog
Ready
In Progress
Code Review
Testing
Done
```

Recommended labels:

```text
component-1
component-2
component-3
component-4
shared
backend
react
flutter
database
agentic-ai
testing
documentation
bug
security
deployment
```

Every task should have:

- clear title,
- owner,
- component,
- acceptance criteria,
- branch name,
- related PR.

Example issue:

```text
Title:
[C1] Create service request API

Owner:
Member 1

Branch:
feature/c1-create-service-request

Acceptance:
- Authenticated customer can create request
- Required fields validated
- Request stored in PostgreSQL
- Other users cannot modify request
- Unit/integration tests pass
```

---

# 14. Environment Variables and Secrets

Never commit:

```text
.env
API keys
database passwords
JWT secret
cloud credentials
private certificates
```

Use:

```text
.env.example
```

Example:

```env
DATABASE_URL=
JWT_SECRET=
MAPS_API_KEY=
AI_API_KEY=
AGENT_SERVICE_URL=
```

Only put variable names/example values in Git.

---

# 15. Database Collaboration Rule

Only create database changes through EF Core migrations.

Recommended migration naming:

```text
InitialIdentitySchema
AddServiceRequestTables
AddProviderManagementTables
AddQuotationBookingTables
AddServiceTrackingTables
AddAgentWorkflowTables
```

If one member creates a migration:

1. push it with the feature,
2. tell the team,
3. others pull latest `develop`,
4. apply the migration locally.

Avoid multiple people independently creating conflicting migrations for the same schema change.

---

# 16. API Collaboration Rules

Use consistent routes.

Examples:

```text
/api/auth
/api/service-requests
/api/providers
/api/quotations
/api/bookings
/api/service-jobs
/api/ai-workflows
/api/reports
```

Use:

- DTOs
- server-side validation
- proper HTTP status codes
- async operations
- authorization policies
- consistent errors
- pagination for list endpoints
- Swagger/OpenAPI

Do not allow React or Flutter to implement important business rules only in the UI.

Business rules belong in the backend.

---

# 17. Agentic AI Collaboration Rules

The four agents must be meaningfully different.

## Agent 1 – Problem Understanding

Question:

> What type of problem is this?

Must not assign providers.

## Agent 2 – Provider Matching

Question:

> Which real provider is suitable?

Must use real provider data and controlled tools.

Must not invent providers.

## Agent 3 – Service Coordination

Question:

> What allowed step should happen next?

Must not approve for the customer.

## Agent 4 – Validation & Safety

Question:

> Is this action valid and allowed?

Must use deterministic business rules.

### Store structured AI workflow information

Store:

- workflow ID,
- objective,
- plan,
- completed steps,
- tool results,
- validation results,
- errors,
- retry information,
- approval status,
- final outcome.

Do not store hidden reasoning.

---

# 18. Human Approval

At least one clearly defined high-impact action should pause.

Recommended assessed workflow:

```text
AI recommends Provider P-18
        ↓
Validation passes
        ↓
WAITING FOR AUTHORIZED APPROVAL
        ↓
Admin/Authorized Staff – React

[Approve] [Reject] [Request Revision]
        ↓
Decision stored
        ↓
Workflow continues
```

Customer quotation approval is another human decision:

```text
Quotation: Rs. 4,500

[Approve] [Reject] [Request Revision]
```

The system must not let the AI approve on behalf of the customer.

---

# 19. Definition of Done for Every Feature

Before saying "my feature is complete":

- [ ] Database change/migration completed if required
- [ ] ASP.NET API implemented
- [ ] Server-side validation implemented
- [ ] Authorization implemented
- [ ] React feature implemented where relevant
- [ ] Flutter feature implemented where relevant
- [ ] Agent contribution integrated where relevant
- [ ] Tests written
- [ ] Tests executed
- [ ] Loading state handled
- [ ] Empty state handled
- [ ] Error state handled
- [ ] Swagger updated
- [ ] No secrets committed
- [ ] Meaningful commits pushed
- [ ] PR reviewed
- [ ] CI passes
- [ ] Documentation updated
- [ ] AI usage log updated
- [ ] Owner can explain and modify the feature

---

# 20. Before You Start Working Each Day

Use this small routine:

```bash
git checkout develop
git pull origin develop
```

Check the project board.

Choose your assigned issue.

Create a fresh branch:

```bash
git checkout -b feature/cX-short-task-name
```

Work in small steps.

Commit meaningful progress.

Push before stopping work.

Open/update the PR.

Update the project board.

---

# 21. Before You Stop Working Each Day

Check:

```bash
git status
```

Do not leave important completed work only on your laptop.

If the work is usable:

```bash
git add .
git commit -m "feat(cX): describe actual progress"
git push
```

If the feature is not ready to merge, keep the PR as a **Draft Pull Request**.

This still gives clear Git evidence of regular development.

---

# 22. Important Team Rules

1. **No direct development on `main`.**
2. **Use one task = one branch.**
3. **Pull `develop` before starting new work.**
4. **Open PRs to `develop`.**
5. **Another member reviews the PR.**
6. **Do not commit secrets.**
7. **Do not create fake commits near the deadline.**
8. **Do not copy code you cannot explain.**
9. **Every member writes tests.**
10. **Every member contributes across backend, database, React, Flutter and Agentic AI for their owned component.**
11. **Keep the system integrated every week.**
12. **Update your AI usage log during development, not at the end.**

---

# 23. Useful Commands

Check branch:

```bash
git branch
```

Check changes:

```bash
git status
```

See recent commits:

```bash
git log --oneline --graph --decorate -15
```

Update develop:

```bash
git checkout develop
git pull origin develop
```

Create branch:

```bash
git checkout -b feature/c1-task-name
```

Push new branch:

```bash
git push -u origin feature/c1-task-name
```

Delete local branch after merge:

```bash
git branch -d feature/c1-task-name
```

---

# 24. Final Goal

The project is successful when the team can demonstrate:

```text
Flutter Customer
    ↓
ASP.NET Core
    ↓
PostgreSQL
    ↓
Agentic AI
    ↓
React Authorized Review
    ↓
Provider / Quotation
    ↓
Customer Approval
    ↓
Service Tracking
    ↓
Completion + Feedback
```

and every team member can explain, test, modify and debug the technical work they own.

---

## Team

Update this table immediately after creating the repository.

| Member | GitHub Username | Component |
|---|---|---|
| Member 1 | `@username` | Service Request & Problem Management |
| Member 2 | `@username` | Provider Management & Intelligent Matching |
| Member 3 | `@username` | Quotation, Booking & Service Coordination |
| Member 4 | `@username` | Service Tracking, Completion & Feedback |

---

**Do not wait until the final week to integrate. Build, review, merge and test continuously.**
