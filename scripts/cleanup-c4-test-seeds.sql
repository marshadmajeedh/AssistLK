-- =====================================================================
-- AssistLK Development Database Cleanup
-- Target: Remove known contaminated Component 4 Live Tracking test seeds
--         from ServiceRequests and dependent tracking tables.
-- Safe, transactional, targeting exact known IDs only.
-- =====================================================================

BEGIN;

-- 1. Delete dependent ServiceStatusHistories records
DELETE FROM "ServiceStatusHistories"
WHERE "ServiceJobId" IN (
    'a655986d-9550-43c2-8aa9-6504b7873a63',
    '23e8b8f0-353b-4b40-a208-18d42246f578'
);

-- 2. Delete dependent ServiceJobs records
DELETE FROM "ServiceJobs"
WHERE "Id" IN (
    'a655986d-9550-43c2-8aa9-6504b7873a63',
    '23e8b8f0-353b-4b40-a208-18d42246f578'
)
AND "ServiceRequestId" IN (
    '33583297-7174-4ef8-8272-0abf7e138cd0',
    'd8d9b2f1-7987-4587-ad49-1410e10dff03'
);

-- 3. Delete the 2 invalid ServiceRequest test records
DELETE FROM "ServiceRequests"
WHERE "Id" IN (
    '33583297-7174-4ef8-8272-0abf7e138cd0',
    'd8d9b2f1-7987-4587-ad49-1410e10dff03'
)
AND "Status" = 'Assigned'
AND "Description" = 'Component 4 Live Tracking Test';

COMMIT;
