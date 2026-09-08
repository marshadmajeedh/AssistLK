# AssistLK Unused Files & Redundant Artifacts Audit Report

**Audit Date:** September 2026  
**Scope:** Repository-wide scan for empty folders, `.gitkeep` files, unused components, duplicate documentation, and abandoned prototypes.  
**Policy:** Audit and recommendation only. No files deleted during this phase.

---

## 1. Executive Summary

This audit catalogs all inactive, placeholder, and deprecated files across the AssistLK repository. The items are classified into four main categories:
1. **Abandoned Prototype Directory (`agent-service/`)**: Legacy Python/LangGraph placeholder directory rendered obsolete by native .NET 8 implementation in `AssistLK.Agents`.
2. **Legacy Architectural Folders (`backend/src/AssistLK.Api/Features/`)**: Remnants of initial feature-folder experiments superseded by Clean Architecture controllers and application services.
3. **Redundant `.gitkeep` Files**: Placeholder files located in folders that now contain active code or test files.
4. **Future Component Placeholders (Components 2, 3, 4)**: Valid placeholders reserved for upcoming component implementation milestones.
5. **Archived Documentation (`docs/archive/`)**: Historical draft specifications already superseded by single-source-of-truth documents.

---

## 2. Comprehensive Inventory

| File / Folder Path | Purpose | Currently Used? | Recommended Action |
| :--- | :--- | :---: | :--- |
| **`agent-service/`** (root folder) | Legacy Python/LangGraph prototype scaffold | **No** | **Remove in future cleanup sprint**. All agent logic runs natively in `AssistLK.Agents` (.NET 8). |
| `agent-service/README.md` | Explanatory note on legacy Python agent idea | **No** | Remove when deleting `agent-service/`. |
| `agent-service/agents/*/.gitkeep` (4 files) | Empty agent subdirectories | **No** | Remove when deleting `agent-service/`. |
| `agent-service/orchestration/.gitkeep` | Empty orchestration subdirectory | **No** | Remove when deleting `agent-service/`. |
| `agent-service/schemas/.gitkeep` | Empty schemas subdirectory | **No** | Remove when deleting `agent-service/`. |
| `agent-service/tests/.gitkeep` | Empty tests subdirectory | **No** | Remove when deleting `agent-service/`. |
| `agent-service/tools/.gitkeep` | Empty tools subdirectory | **No** | Remove when deleting `agent-service/`. |
| **`backend/src/AssistLK.Api/Features/`** | Legacy feature-folder structure | **No** | **Remove directory**. Replaced by Clean Architecture (`Controllers/`, `DTOs/`). |
| `.../Features/Bookings/.gitkeep` | Feature folder placeholder | **No** | Remove directory. |
| `.../Features/Providers/.gitkeep` | Feature folder placeholder | **No** | Remove directory. |
| `.../Features/Quotations/.gitkeep` | Feature folder placeholder | **No** | Remove directory. |
| `.../Features/ServiceRequests/.gitkeep` | Feature folder placeholder | **No** | Remove directory. |
| `.../Features/ServiceTracking/.gitkeep` | Feature folder placeholder | **No** | Remove directory. |
| **`backend/tests/AssistLK.Api.Tests/.gitkeep`** | Placeholder in test project | **No** (Folder contains `.cs` tests) | **Remove**. Redundant since folder is populated. |
| **`backend/tests/AssistLK.IntegrationTests/.gitkeep`** | Placeholder in test project | **No** (Folder contains `.cs` tests) | **Remove**. Redundant since folder is populated. |
| `backend/src/AssistLK.Agents/Tools/DemoProviderSearchTool.cs` | Mock tool for Component 2 provider search | **Review** (Not used in Component 1) | **Keep until Component 2 sprint** as reference mock. |
| `backend/src/AssistLK.Application/Services/Providers/.gitkeep` | Component 2 service placeholder | **Reserved** | **Keep** until Component 2 implementation. |
| `backend/src/AssistLK.Application/Services/Quotations/.gitkeep` | Component 3 service placeholder | **Reserved** | **Keep** until Component 3 implementation. |
| `backend/src/AssistLK.Application/Services/ServiceTracking/.gitkeep` | Component 4 service placeholder | **Reserved** | **Keep** until Component 4 implementation. |
| `backend/src/AssistLK.Application/Validators/.gitkeep` | Shared validators placeholder | **Reserved** | **Keep** for custom FluentValidators. |
| `backend/src/AssistLK.Infrastructure/ExternalServices/.gitkeep` | Infrastructure external services | **Reserved** | **Keep** for third-party integrations (SMS/Maps). |
| `mobile/lib/features/providers/.gitkeep` | Component 2 mobile placeholder | **Reserved** | **Keep** until mobile provider matching screen dev. |
| `mobile/lib/features/quotations/.gitkeep` | Component 3 mobile placeholder | **Reserved** | **Keep** until mobile quotation screen dev. |
| `mobile/lib/features/tracking/.gitkeep` | Component 4 mobile placeholder | **Reserved** | **Keep** until mobile tracking screen dev. |
| `mobile/lib/features/auth/*/.gitkeep` (4 files) | Mobile authentication module placeholders | **Reserved** | **Keep** until Flutter auth flow dev. |
| `mobile/lib/shared/*/.gitkeep` (2 files) | Mobile shared widgets placeholder | **Reserved** | **Keep** for shared Flutter UI components. |
| `web/src/features/providers/.gitkeep` | Component 2 web placeholder | **Reserved** | **Keep** until web provider matching feature dev. |
| `web/src/features/quotations/.gitkeep` | Component 3 web placeholder | **Reserved** | **Keep** until web quotation feature dev. |
| `web/src/features/tracking/.gitkeep` | Component 4 web placeholder | **Reserved** | **Keep** until web tracking feature dev. |
| `web/src/features/aiWorkflows/.gitkeep` | Agent workflow frontend placeholder | **Reserved** | **Keep** for shared AI workflow visualizer. |
| **`docs/archive/`** (7 files) | Superseded documentation stubs | **Reference only** | **Keep in archive** for historical traceability. |

---

## 3. Prioritized Action Plan

1. **Immediate Zero-Risk Candidate (Next PR):**
   - Delete `backend/src/AssistLK.Api/Features/` (all 5 `.gitkeep` files).
   - Delete redundant test placeholders: `backend/tests/AssistLK.Api.Tests/.gitkeep` and `backend/tests/AssistLK.IntegrationTests/.gitkeep`.
2. **Secondary Phase (Post Component 1 Freeze):**
   - Safely remove `agent-service/` root directory once the team agrees no external Python microservices will be deployed.
3. **Preserve for Component 2, 3, 4:**
   - Retain all `mobile/lib/features/*/.gitkeep` and `web/src/features/*/.gitkeep` placeholders so git tracks designated feature architecture boundaries.
