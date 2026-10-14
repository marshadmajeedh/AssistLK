// Purpose: Performance test script for Service Request Creation endpoint (POST /api/service-requests).
// Why: Validates that the backend handles concurrent customer request submissions under load within acceptable latency and error bounds.
// Scenario: 10 virtual users concurrently submit 25 total service request creation requests.
// Virtual Users: 10 VUs.
// Duration / Iterations: 25 total iterations shared across VUs.
// Checks: HTTP status code is 201 Created.
// Thresholds: Failure rate < 5% (http_req_failed rate < 0.05), 95th percentile duration < 2000ms (p(95) < 2000).
// Prerequisites / Required Environment Variables:
//   - BASE_URL: Backend API base URL (e.g. http://localhost:5000 or https://assistlk-backend-production.up.railway.app).
//   - TOKEN: Valid JWT Bearer token for an authenticated Customer.
// Real vs Mocked: Real HTTP POST requests sent against the live API and its underlying PostgreSQL database.
// Run from repository root: k6 run -e BASE_URL="http://localhost:5000" -e TOKEN="<jwt_token>" performance-tests/service-request-creation.js

import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 10,
  iterations: 25,
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000'],
  },
};

const BASE_URL = __ENV.BASE_URL;
const TOKEN = __ENV.TOKEN;

// Purpose: Executes a single service request creation iteration for a virtual user.
// Why: Simulates an authenticated customer submitting a new plumbing service request with GPS coordinates.
// Scenario: HTTP POST to /api/service-requests with valid payload and customer Bearer token.
// Pass condition: API responds with HTTP 201 Created.
// Note: In k6, the default function is the per-VU iteration loop and cannot be selected as an independent unit test.
export default function () {
  const payload = JSON.stringify({
    description: 'Performance test plumbing request',
    locationText: 'Colombo',
    latitude: 6.9271,
    longitude: 79.8612,
  });

  const res = http.post(
    `${BASE_URL}/api/service-requests`,
    payload,
    {
      headers: {
        Authorization: `Bearer ${TOKEN}`,
        'Content-Type': 'application/json',
      },
    }
  );

  check(res, {
    'created 201': (r) => r.status === 201,
  });
}