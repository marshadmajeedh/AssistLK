// Purpose: Performance test script for Customer Service Requests Listing endpoint (GET /api/service-requests/my).
// Why: Validates that the backend can serve multiple concurrent customers retrieving their request history without latency degradation.
// Scenario: 25 virtual users fetch the authenticated customer's service requests over a 30-second duration.
// Virtual Users: 25 VUs.
// Duration / Iterations: 30 seconds (duration: '30s').
// Checks: HTTP status code is 200 OK.
// Thresholds: Failure rate < 5% (http_req_failed rate < 0.05), 95th percentile duration < 2000ms (p(95) < 2000).
// Prerequisites / Required Environment Variables:
//   - BASE_URL: Backend API base URL (e.g. http://localhost:5000 or https://assistlk-backend-production.up.railway.app).
//   - TOKEN: Valid JWT Bearer token for an authenticated Customer.
// Real vs Mocked: Real HTTP GET requests sent against the live API querying the real PostgreSQL database.
// Run from repository root: k6 run -e BASE_URL="http://localhost:5000" -e TOKEN="<jwt_token>" performance-tests/service-request-list.js

import http from 'k6/http';
import { check } from 'k6';

export const options = {
    vus: 25,
    duration: '30s',

    thresholds: {
        http_req_failed: ['rate<0.05'],
        http_req_duration: ['p(95)<2000'],
    },
};

const BASE_URL = __ENV.BASE_URL;
const TOKEN = __ENV.TOKEN;

// Purpose: Executes a single iteration of retrieving the authenticated customer's service requests.
// Why: Measures list endpoint latency and stability under sustained concurrency.
// Scenario: HTTP GET to /api/service-requests/my with Customer Bearer token.
// Pass condition: API responds with HTTP 200 OK.
// Note: In k6, the default function is the per-VU iteration loop and cannot be selected as an independent unit test.
export default function () {

    console.log(`BASE_URL present: ${!!BASE_URL}`);
    console.log(`TOKEN present: ${!!TOKEN}`);
    console.log(`TOKEN length: ${TOKEN ? TOKEN.length : 0}`);

    const response = http.get(
        `${BASE_URL}/api/service-requests/my`,
        {
            headers: {
                Authorization: `Bearer ${TOKEN}`,
                'Content-Type': 'application/json',
            },
        }
    );

    console.log(`Status: ${response.status}`);

    check(response, {
        'status is 200': (r) => r.status === 200,
    });
}