# Component 1 attachment foundation — Phase 1

Optional customer problem photos are stored as normalized private files, with metadata in PostgreSQL. Phase 3 transports bounded normalized evidence internally to Python; Phase 4 adds real multimodal provider reasoning and bounded audit results. Flutter/React attachment endpoints are unchanged.

## Dependency

The repository had no suitable image decoder or additional dependency approval policy. Infrastructure uses **Magick.NET-Q8-AnyCPU 14.17.1**, with its matching transitive Core package; no competing image library was added. Q8 uses 8-bit channels appropriate for normalized JPEG photographs. The package targets .NET 8 and bundles native Windows/Linux libraries, avoiding System.Drawing.Common and a separately installed ImageMagick service.

The [Apache 2.0 license](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) permits this academic application and redistribution subject to retaining applicable license/notice files. Preserve the package's native dependency notices when distributing the backend. [.NET package metadata](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/14.17.1) and [cross-platform deployment documentation](https://github.com/dlemstra/Magick.NET/blob/main/docs/CrossPlatform.md) were checked before installation. Runtime verification here is Windows; Linux compatibility is based on the upstream package, not a local Linux test run. Package restore reported no vulnerability warnings.

## API contract

All routes require the existing Customer JWT role and resolve the request by its authenticated owner. Provider/Admin receive 403; unknown or other-customer requests and mismatched attachment IDs receive 404.

| Method | Route | Behavior |
|---|---|---|
| POST | `/api/service-requests/{id}/attachments` | One multipart field named `file`; returns 201 metadata |
| GET | `/api/service-requests/{id}/attachments` | Metadata ordered by slot |
| GET | `/api/service-requests/{id}/attachments/{attachmentId}/content` | Authorized JPEG stream; `nosniff`, `private, no-store`, fixed safe inline filename |
| DELETE | `/api/service-requests/{id}/attachments/{attachmentId}` | Removes metadata and increments revision atomically; returns 204 |

Metadata contains `id`, `slot`, `contentType`, `fileSizeBytes`, `width`, `height`, `createdAt`. It contains no original filename, storage key/path, hash, Base64, or public URL. The POST Location header identifies the authorized content endpoint. Existing ServiceRequest JSON does not acquire image fields.

Add/remove is allowed only in **Created** and **AwaitingInformation**. Other lifecycle states return 409. Cancelled requests retain owner read access and files. No new hard-delete endpoint or automatic analysis was added. Concurrent evidence/status edits return conflict; callers should refresh before retrying.

## Validation and normalization

- One file, input at most **5 × 1024 × 1024 bytes**; endpoint-specific request/form limit **6 × 1024 × 1024 bytes**.
- JPEG, PNG and static WebP only. Extension, declared MIME, signature, forced allowlisted decoder, successful decode, frame count and dimensions must agree. SVG/GIF/PDF/HEIC/video and animated PNG/WebP are rejected.
- Ping checks dimensions before full decode: at most 10,000 pixels on either side and 24,000,000 pixels total. A process-wide semaphore permits one normalization at a time. Native decoder resource limits constrain memory (256 MiB), profiles (1 MiB), frame list (2), threads (1), processing time (20 seconds) and disk spill (disabled).
- Apply orientation, flatten transparency onto white, proportionally downsize only when needed to a 2048-pixel long edge, strip metadata, re-encode JPEG quality 85. Smaller images are not upscaled. Approximately 1 MiB is a target, not an output rejection threshold.
- Strip EXIF (including GPS/device data), comments and other profiles. This does **not** remove faces, plates, addresses, or documents visible in pixels.
- SHA-256 covers the exact normalized stored bytes. It supports future integrity/deduplication work; this phase does not implement deduplication and never uses hashes for authorization.

## Schema and revisions

`ServiceRequestAttachment` follows BaseEntity IDs/timestamps. Its metadata columns are ServiceRequestId, StorageKey (36), ContentType (32), FileSizeBytes, Width, Height, Slot and ContentHash (64). PostgreSQL enforces unique StorageKey, unique (ServiceRequestId, Slot), slot range 1–3, positive size/dimensions and cascading metadata deletion. There are no binary columns.

`ServiceRequest.EvidenceRevision` and `ProblemAnalysis.EvidenceRevision` start at **1**. Existing rows receive that baseline; historical revisions cannot be reconstructed, and no old analyses are deleted.

Increment once per accepted mutation when trimmed description, normalized CategoryHint, trimmed LocationText, latitude, longitude or submitted clarification answer content changes, and on each attachment add/remove. A no-op edit does not increment. LocationSource alone does not increment because it is provenance, not part of ProblemUnderstandingInput. Existing description supersession behavior remains intact. Photo changes never answer/supersede questions, reset round counts, create a third round, or start analysis.

Status and EvidenceRevision are EF concurrency tokens. The metadata mutation and revision save share EF's relational transaction; the unique slot constraint is a second database guard. The workflow captures the revision before analysis, checks it at the Analyzing transition, and persists it on the analysis result. ReadyForMatching requires the latest persisted analysis revision to equal the current request revision, in addition to every previous readiness rule. Internal workflow audit input contains the revision and at most three attachment IDs. Phase 3 adds a separate transient visual payload to the explicit Python adapter mapping; the persisted input never contains image content.

## Phase 3 internal visual-evidence contract

`ProblemUnderstandingInput` is the persisted audit model: only its existing text fields, `EvidenceRevision` and bounded `AttachmentIds` are serialized by `AgentWorkflowService`. `AnalysisEvidenceRepository` reads current owned request metadata without EF tracking and retrieves at most four attachment rows so corruption above the three-photo limit can be detected safely. Rows are ordered by slot, then ID.

After the Analyzing transition and metadata capture, the workflow saves its audit snapshot **before** reading any binary. `ProblemVisualEvidenceService` then reads the exact normalized bytes from private storage into transient `VisualEvidencePayloadDto` items. These live only in a `[JsonIgnore]` request-scoped `AgentContext.VisualEvidence` property and the internal HTTP wire DTO. They never enter `ProblemUnderstandingInput`, `Data`, semantic memory, execution output, logs or database columns. Storage keys and original filenames are never sent to Python.

The existing JSON envelope now permits `input.visualEvidence`: an array of `{attachmentId, contentType, dataBase64, width, height}`. Omitted/empty evidence remains text-only. MIME is the server-stored `image/jpeg`, not a client declaration. Internal authentication, cancellation and timeout configuration are unchanged. There is no new retry policy or multipart internal upload.

Hard limits, independently enforced in .NET and Python:

- Maximum three unique attachment UUIDs; dimensions 1–2048 on each axis.
- Maximum **2,097,152 decoded bytes per image** and **4,194,304 decoded bytes total**.
- Maximum **2,796,204 Base64 characters per image**, **5,592,412 total** (padding included), plus bounded metadata and existing text fields in the JSON envelope.
- Backend checks both stored size and actual bounded stream reads; size mismatch, empty content, invalid metadata or excess count fails. The HTTP serializer also validates encoded/decoded budgets.

The ceiling is a transport policy, not a claim that every quality-85 JPEG fits: Phase 1 resizes to a 2048-pixel edge and targets roughly 1 MiB, but has no normalized byte cap. Larger normalized files remain stored and cause safe execution failure, never truncation or silent text-only substitution. Public upload limits and normalization are unchanged.

Missing binaries, storage errors, unsafe budgets, rejected Python payloads and unavailable Python fail the execution and use existing Created/AwaitingInformation recovery. Storage exception details and HTTP validation bodies are not echoed. A fresh revision/status read precedes result application; existing EF concurrency tokens protect the later save. Recovery reloads current persisted state, and semantic memory is stored only after the domain accepts the result. No new lifecycle state or migration was added.

Python validates transport without an image decoder dependency. Preparation reports internal `not_requested`, `available`, `unsupported` or `failed` status; `available` means capability/readiness, never analysis. Gemini, OpenAI and Offline all report `supports_images = false` in Phase 3. Text reasoning continues with unsupported evidence, but the payload never enters text prompts and no visual findings are produced. Status stays internal to graph state; the public analysis response is unchanged. Phase 4 must implement provider-specific conversion and actual vision before claiming image understanding.

Regression coverage: `VisualEvidenceTransportTests` inspects the HTTP payload and persisted audit in the same workflow, verifies exact bytes/identity/order and limits, failure recovery, private-error suppression and stale-revision rejection. The PostgreSQL end-to-end fixture retains its actual normalized synthetic JPEG through analysis and continues checking the audit field allowlist and ReadyForMatching.

### Phase 3 verification and file manifest

- `dotnet build backend/AssistLK.sln`: 0 errors, 0 warnings.
- `dotnet test backend/AssistLK.sln`: 159 API + 339 integration = **498 passed**, 0 failed, 0 skipped (470 baseline + 28 new cases).
- Existing Python venv: `python -m pytest`: **78 passed**, 0 failed, 0 skipped (46 baseline + 32 new cases); no live provider or service required.
- `git diff --check`: pass. No Flutter/React changes, migration, public endpoint change, live vision or commit.

Created (repository-relative paths):

- `agent-services/problem-understanding-agent/app/graphs/visual_evidence.py`
- `agent-services/problem-understanding-agent/app/schemas/visual_evidence.py`
- `agent-services/problem-understanding-agent/tests/test_visual_evidence.py`
- `backend/src/AssistLK.Agents/DTOs/VisualEvidencePayloadDto.cs`
- `backend/src/AssistLK.Application/Attachments/ProblemVisualEvidenceService.cs`
- `backend/src/AssistLK.Infrastructure/Repositories/AnalysisEvidenceRepository.cs`
- `backend/tests/AssistLK.IntegrationTests/TestDoubles/TestAnalysisEvidence.cs`
- `backend/tests/AssistLK.IntegrationTests/VisualEvidenceTransportTests.cs`

Modified (repository-relative paths):

- `agent-services/problem-understanding-agent/README.md`
- `agent-services/problem-understanding-agent/app/graphs/problem_graph.py`
- `agent-services/problem-understanding-agent/app/graphs/state.py`
- `agent-services/problem-understanding-agent/app/main.py`
- `agent-services/problem-understanding-agent/app/providers/base.py`
- `agent-services/problem-understanding-agent/app/schemas/request.py`
- `backend/ATTACHMENTS.md`
- `backend/src/AssistLK.Agents/Adapters/ExternalProblemUnderstandingAgentAdapter.cs`
- `backend/src/AssistLK.Agents/Clients/ProblemUnderstandingHttpClient.cs`
- `backend/src/AssistLK.Agents/Core/AgentContext.cs`
- `backend/src/AssistLK.Agents/DTOs/AgentExecutionWireDtos.cs`
- `backend/src/AssistLK.Agents/Models/ProblemUnderstandingInput.cs`
- `backend/src/AssistLK.Application/Interfaces/IServiceRequestRepository.cs`
- `backend/src/AssistLK.Application/Services/ProblemUnderstandingWorkflowService.cs`
- `backend/src/AssistLK.Application/Services/ServiceRequestService.cs`
- `backend/src/AssistLK.Infrastructure/DependencyInjection.cs`
- `backend/src/AssistLK.Infrastructure/Repositories/ServiceRequestRepository.cs`
- `backend/tests/AssistLK.IntegrationTests/Component1FailureTests.cs`
- `backend/tests/AssistLK.IntegrationTests/Component1WorkflowTests.cs`
- `backend/tests/AssistLK.IntegrationTests/ExternalProblemUnderstandingWorkflowIntegrationTests.cs`
- `backend/tests/AssistLK.IntegrationTests/LocalhostPythonSmokeIntegrationTests.cs`
- `backend/tests/AssistLK.IntegrationTests/PostgreSql/ClarificationWorkflowPostgreSqlTests.cs`
- `backend/tests/AssistLK.IntegrationTests/PostgreSql/Component1EndToEndPostgreSqlTests.cs`

## Private storage configuration

Set `AttachmentStorage__RootPath=private-attachments` (default), relative to API content root, or an operator-chosen absolute private directory. Startup validates/creates the directory. `wwwroot` and its descendants, existing symbolic links/junctions and non-opaque object keys are rejected. Use a service-owned directory and deployment filesystem permissions that deny other users write access; do not configure a reverse proxy/static server to publish it. The default directory is gitignored. The storage is intended for a single backend instance with persistent local disk, not ephemeral or shared multi-instance deployment.

Application defines narrow storage/repository/normalizer interfaces; Infrastructure owns paths, decoding and EF. Objects use random GUID-N `.jpg` keys, never customer filenames. Writes create a `.stage` file with create-new semantics and promote without overwrite **before** the database commit. If that commit fails, cleanup deletes only after the database confirms the key is unreferenced. If commit outcome/reference checking is uncertain, the file is retained for reconciliation to avoid deleting committed evidence. Promotion failure cannot leave a newly committed row.

Delete commits metadata removal/revision first. Binary deletion failure is safely logged and leaves an inaccessible orphan for retry. Internal hard-delete cascades metadata only; filesystem cleanup still requires reconciliation.

`AttachmentReconciliationService.ReconcileAsync()` is an explicitly callable DI maintenance component, with no HTTP endpoint or hosted scheduler. **Quiesce uploads before calling it**, using an operator-controlled maintenance host/scope with the same storage configuration and database. It examines opaque canonical/stage objects older than one day, preserves referenced files, aborts when database absence cannot be verified, and retries inaccessible objects on a later invocation. Residual files provide the durable cleanup work list. Back up the metadata database and private directory together; missing files caused by external deletion/disk loss require operator restoration.

## Migration and testing

Migration: `20260913011940_AddServiceRequestAttachmentsAndEvidenceRevision`. It creates the table and revision fields/checks. It was generated only; the primary application database was not migrated. The existing guarded test database migration chain verifies it on a disposable PostgreSQL database.

Test hosts inject fake attachment storage; filesystem unit tests use unique temporary directories and synthetic images. Coverage includes authorization/ownership, multipart validation, image normalization/privacy, path containment, compensation/reconciliation, lifecycle restrictions, PostgreSQL constraints/concurrent uploads/status races/cascade, revision readiness and audit input safety. Existing clarification, Smart Location and workflow regressions remain part of the full suite. No Python/Nominatim/cloud dependency is required.

The Phase 1 foundation is complete. Flutter selection/upload (Phase 2) and internal transport (Phase 3) are also complete; Phase 4 provider reasoning is documented below. Public visual-result presentation remains deferred.

## Implementation file manifest

Created:

- `backend/ATTACHMENTS.md`
- `backend/src/AssistLK.Api/Controllers/ServiceRequestAttachmentsController.cs`
- `backend/src/AssistLK.Application/Attachments/AttachmentContracts.cs`
- `backend/src/AssistLK.Application/Attachments/AttachmentReconciliationService.cs`
- `backend/src/AssistLK.Application/Attachments/ServiceRequestAttachmentService.cs`
- `backend/src/AssistLK.Domain/Entities/ServiceRequestAttachment.cs`
- `backend/src/AssistLK.Infrastructure/Attachments/AttachmentImageNormalizer.cs`
- `backend/src/AssistLK.Infrastructure/Attachments/AttachmentStorageOptions.cs`
- `backend/src/AssistLK.Infrastructure/Attachments/PrivateFileAttachmentStorage.cs`
- `backend/src/AssistLK.Infrastructure/Data/Migrations/20260913011940_AddServiceRequestAttachmentsAndEvidenceRevision.Designer.cs`
- `backend/src/AssistLK.Infrastructure/Data/Migrations/20260913011940_AddServiceRequestAttachmentsAndEvidenceRevision.cs`
- `backend/src/AssistLK.Infrastructure/Repositories/ServiceRequestAttachmentRepository.cs`
- `backend/tests/AssistLK.Api.Tests/ServiceRequestAttachmentApiTests.cs`
- `backend/tests/AssistLK.IntegrationTests/AttachmentNormalizationTests.cs`
- `backend/tests/AssistLK.IntegrationTests/AttachmentServiceTests.cs`
- `backend/tests/AssistLK.IntegrationTests/AttachmentStorageTests.cs`
- `backend/tests/AssistLK.IntegrationTests/PostgreSql/AttachmentPostgreSqlTests.cs`
- `backend/tests/Shared/FakeAttachmentStorage.cs`

Modified:

- `.gitignore`
- `backend/src/AssistLK.Agents/Models/ProblemUnderstandingInput.cs`
- `backend/src/AssistLK.Api/Middleware/ExceptionHandlingMiddleware.cs`
- `backend/src/AssistLK.Api/Program.cs`
- `backend/src/AssistLK.Application/ServiceRequests/DTOs/ApplyProblemAnalysisResult.cs`
- `backend/src/AssistLK.Application/ServiceRequests/DTOs/ServiceRequestResponse.cs`
- `backend/src/AssistLK.Application/Services/ProblemUnderstandingWorkflowService.cs`
- `backend/src/AssistLK.Application/Services/ServiceRequestService.cs`
- `backend/src/AssistLK.Domain/Entities/ProblemAnalysis.cs`
- `backend/src/AssistLK.Domain/Entities/ServiceRequest.cs`
- `backend/src/AssistLK.Infrastructure/AssistLK.Infrastructure.csproj`
- `backend/src/AssistLK.Infrastructure/Data/AssistLKDbContext.cs`
- `backend/src/AssistLK.Infrastructure/Data/Migrations/AssistLKDbContextModelSnapshot.cs`
- `backend/src/AssistLK.Infrastructure/DependencyInjection.cs`
- `backend/tests/AssistLK.Api.Tests/AssistLK.Api.Tests.csproj`
- `backend/tests/AssistLK.Api.Tests/AssistLKApiTestFactory.cs`
- `backend/tests/AssistLK.Api.Tests/PostgreSql/PostgreSqlAssistLKApiTestFactory.cs`
- `backend/tests/AssistLK.IntegrationTests/AssistLK.IntegrationTests.csproj`
- `backend/tests/AssistLK.IntegrationTests/PostgreSql/Component1EndToEndPostgreSqlTests.cs`
- `backend/tests/AssistLK.IntegrationTests/PostgreSql/MigrationPostgreSqlTests.cs`

## Phase 1 verification history

- `dotnet build backend/AssistLK.sln`: succeeded, 0 warnings, 0 errors.
- `dotnet test backend/AssistLK.sln`: 159 API + 311 integration = 470 passed; 0 failed, 0 skipped. Includes 75 new test cases.
- `git diff --check`: exit 0; new files also checked for trailing whitespace.
- No Python processes running during final verification. No mobile/, web/, or agent-services/ changes.
- Migration verified through the existing disposable PostgreSQL migration test; not applied to the primary database.
- No commit or Phase 2 implementation performed.

## Phase 4 structured visual-result contract

Gemini and OpenAI now implement one multimodal reasoning request containing the bounded ordered JPEG set; Offline remains text-only with `unsupported`. No upload, normalization, ownership, revision or lifecycle rules change.

Python result fields `visionStatus`, `attachmentIdsUsed`, `visualObservations` and `visualLimitations` map through `ProblemUnderstandingOutputPayloadDto` to a compact `ProblemUnderstandingOutput.VisualResult` value. Validation permits `not_requested`, `used`, `unsupported`, `failed`; rejects fabricated `partial`; and requires `used` to acknowledge the entire supplied set. Observations reference only known inspected IDs: maximum five total, two per image, 240 characters each; limitations maximum three, 240 characters each. Duplicate/unknown IDs, excess limits, payload echoes and listed unsafe content fail safely. A legacy omitted status maps to `not_requested` with no images, or conservatively `unsupported` with images; it can never claim successful vision.

**Persistence decision:** the existing `AgentWorkflowService` serialized execution output is a clean audit location for bounded `VisualResult`. No database schema change or migration is required. ProblemAnalysis-specific visual evidence persistence and public response/client presentation are deferred to Phase 5. Existing semantic memory mappings do not acquire image content or arbitrary provider metadata.

Image-enhanced output/error paths reject or suppress image payload echoes; additional metadata is allowlisted and provider metadata is bounded to implemented provider names. No Base64, raw bytes, storage keys, vendor request/response payloads or hidden reasoning belongs in persisted results. Regressions inspect successful and rejected persisted execution fields and existing semantic memory. Previous stale-revision, two-round re-analysis, ReadyForMatching, ownership and recovery tests remain intact.

Provider failures exhaust only the existing retry budget and return unsuccessful execution through existing ASP.NET recovery; no extra fresh-budget text fallback or per-image retry loop. Normal backend tests continue to use the fake internal service and do not require Python. See the [canonical agent README](../agent-services/problem-understanding-agent/README.md) for provider support, mock coverage, gated live verification and Phase 4 results.

## Phase 5 authoritative analysis evidence

`ProblemAnalysis.VisualEvidence` now owns the accepted, provider-neutral structured result: `visionStatus`, `attachmentIdsUsed`, `observations` (`attachmentId`, `observation`), and `limitations`. One JSONB column stores this bounded aggregate, using an EF value converter and structural change comparer. This keeps the small per-analysis value together without child-table joins or unrelated prose columns. No binary content, paths, provider payload, metadata hashes or hidden reasoning is part of the value.

Migration **20260913083439_AddProblemAnalysisVisualEvidence** adds only that column with a `not_requested`/empty-array default for legacy rows. It does not modify the attachment migration. The migration is generated and verified on guarded test PostgreSQL, including upgrade, legacy data and Down/re-upgrade. It is **not applied to the developer's primary database**.

The workflow maps its Phase 4 result and captured attachment IDs into the existing authoritative analysis application. Domain validation checks status, supplied/owned identities, observation membership, count/string limits and listed unsafe/payload content before changing request state. Visual metadata is written with the rest of ProblemAnalysis in the existing SaveChanges transaction. Existing current-evidence and concurrency checks still reject stale results before they can become authoritative.

Customer Request Details expose `latestAnalysis.visualEvidence` only when that analysis's EvidenceRevision equals the request revision. Historical analysis remains stored; stale visual metadata is omitted from current details. Customer detail-shaped command responses use the same mapping. Customer and Admin list responses omit visual evidence entirely, and Admin detail remains unchanged in this phase. Detail data comes from ProblemAnalysis, never the operational execution audit. The C2 `ServiceRequestForMatchingResponse` is unchanged.

The existing two-round clarification, optional-photo readiness, attachment mutation restrictions and cancellation retention rules remain. Valid successful `unsupported`/`failed` values can be represented, but actual Phase 4 live vision failure still returns failed execution with no new ProblemAnalysis. `partial` remains invalid. Flutter displays only the safe persisted wording; no new public image URL or image endpoint is introduced.

Phase 5 verification is separate from the Phase 4 Gemini synthetic-image smoke test. Full physical-device/mobile Gemini E2E has **not** been performed in Phase 5; OpenAI remains not live-verified. Later Phase 6 verification can exercise the complete selection/upload/analyze/persist/detail flow after the primary database migration is deliberately applied.

### Phase 5 verification (2026-09-13)

- `dotnet build backend/AssistLK.sln`: 0 warnings, 0 errors.
- `dotnet test backend/AssistLK.sln`: **529 passed** (159 API + 370 integration), 0 failed, 0 skipped. All 514 baseline cases retained, with 15 new persistence/migration cases; Phase 4 audit tests additionally assert domain persistence.
- Existing Python venv `python -m pytest`: **162 passed**, 0 failed, 0 skipped. No Python runtime/test changes or live model call in Phase 5.
- `flutter pub get`: succeeded without a dependency upgrade; `flutter analyze`: no issues; `flutter test --reporter expanded`: **411 passed**, 0 failed, 0 skipped (386 baseline + 25 new cases).
- Migration upgrade, legacy defaults, rollback/re-upgrade and JSONB round-trip/change tracking pass on disposable PostgreSQL. Primary database not updated.
- Responsive regressions at 320px/412px and 1�/2� text exposed existing header/status and readiness-chip overflow. These received wrapping-only fixes; no lifecycle behavior change.
- `git diff --check`: passed. No web/, provider-runtime, public attachment API, normalization/storage, C2 handoff or matching-rule changes. No commit/staging performed.
- Physical-device/live mobile E2E: **not performed**. Phase 6 should verify the full customer image-to-persisted-insight flow after an explicitly authorized primary-database migration. Historical Phase 4 Gemini inference evidence does not establish this mobile E2E.
