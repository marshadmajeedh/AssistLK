# Component 1 attachment foundation — Phase 1

Optional customer problem photos are stored as normalized private files, with metadata in PostgreSQL. This phase does **not** use images for AI analysis. The existing Python contract and Flutter/React clients are unchanged.

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

Status and EvidenceRevision are EF concurrency tokens. The metadata mutation and revision save share EF's relational transaction; the unique slot constraint is a second database guard. The workflow captures the revision before analysis, checks it at the Analyzing transition, and persists it on the analysis result. ReadyForMatching requires the latest persisted analysis revision to equal the current request revision, in addition to every previous readiness rule. Only the small revision number was added to internal workflow audit input; the explicit Python adapter mapping is unchanged and carries no attachments.

## Private storage and operation

Set `AttachmentStorage__RootPath=private-attachments` (default), relative to API content root, or an operator-chosen absolute private directory. Startup validates/creates the directory. `wwwroot` and its descendants, existing symbolic links/junctions and non-opaque object keys are rejected. Use a service-owned directory and deployment filesystem permissions that deny other users write access; do not configure a reverse proxy/static server to publish it. The default directory is gitignored. The storage is intended for a single backend instance with persistent local disk, not ephemeral or shared multi-instance deployment.

Application defines narrow storage/repository/normalizer interfaces; Infrastructure owns paths, decoding and EF. Objects use random GUID-N `.jpg` keys, never customer filenames. Writes create a `.stage` file with create-new semantics and promote without overwrite **before** the database commit. If that commit fails, cleanup deletes only after the database confirms the key is unreferenced. If commit outcome/reference checking is uncertain, the file is retained for reconciliation to avoid deleting committed evidence. Promotion failure cannot leave a newly committed row.

Delete commits metadata removal/revision first. Binary deletion failure is safely logged and leaves an inaccessible orphan for retry. Internal hard-delete cascades metadata only; filesystem cleanup still requires reconciliation.

`AttachmentReconciliationService.ReconcileAsync()` is an explicitly callable DI maintenance component, with no HTTP endpoint or hosted scheduler. **Quiesce uploads before calling it**, using an operator-controlled maintenance host/scope with the same storage configuration and database. It examines opaque canonical/stage objects older than one day, preserves referenced files, aborts when database absence cannot be verified, and retries inaccessible objects on a later invocation. Residual files provide the durable cleanup work list. Back up the metadata database and private directory together; missing files caused by external deletion/disk loss require operator restoration.

## Migration and testing

Migration: `20260913011940_AddServiceRequestAttachmentsAndEvidenceRevision`. It creates the table and revision fields/checks. It was generated only; the primary application database was not migrated. The existing guarded test database migration chain verifies it on a disposable PostgreSQL database.

Test hosts inject fake attachment storage; filesystem unit tests use unique temporary directories and synthetic images. Coverage includes authorization/ownership, multipart validation, image normalization/privacy, path containment, compensation/reconciliation, lifecycle restrictions, PostgreSQL constraints/concurrent uploads/status races/cascade, revision readiness and audit input safety. Existing clarification, Smart Location and workflow regressions remain part of the full suite. No Python/Nominatim/cloud dependency is required.

Phase 2 still needs the Flutter camera/gallery selection flow, optional upload/list/preview/remove UI, lifecycle-aware controls and appropriate upload failure/retry handling. Vision transport/agent work and React presentation are outside this phase.

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

## Verified results

- `dotnet build backend/AssistLK.sln`: succeeded, 0 warnings, 0 errors.
- `dotnet test backend/AssistLK.sln`: 159 API + 311 integration = 470 passed; 0 failed, 0 skipped. Includes 75 new test cases.
- `git diff --check`: exit 0; new files also checked for trailing whitespace.
- No Python processes running during final verification. No mobile/, web/, or agent-services/ changes.
- Migration verified through the existing disposable PostgreSQL migration test; not applied to the primary database.
- No commit or Phase 2 implementation performed.
