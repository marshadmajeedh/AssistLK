// Purpose: Performance test script for Customer Login endpoint (POST /api/auth/login).
// Why: Validates that the auth service withstands concurrent user authentication bursts within response time limits.
// Scenario: 10 virtual users concurrently authenticate with customer credentials over a 30-second duration.
// Virtual Users: 10 VUs.
// Duration / Iterations: 30 seconds (duration: '30s').
// Checks: HTTP status code is 200 OK.
// Thresholds: Failure rate < 5% (http_req_failed rate < 0.05), 95th percentile duration < 2000ms (p(95) < 2000).
// Prerequisites / Required Environment Variables:
//   - BASE_URL: Backend API base URL (e.g. http://localhost:5000 or https://assistlk-backend-production.up.railway.app).
//   - EMAIL: Registered user email address.
//   - PASSWORD: Valid password for the test user.
// Real vs Mocked: Real HTTP POST requests hitting API password verification and JWT issuance.
// Run from repository root: k6 run -e BASE_URL="http://localhost:5000" -e EMAIL="customer@example.com" -e PASSWORD="password" performance-tests/login-test.js

import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.05'],
    http_req_duration: ['p(95)<2000'],
  },
};

const BASE_URL = __ENV.BASE_URL;
const EMAIL = __ENV.EMAIL;
const PASSWORD = __ENV.PASSWORD;

// Purpose: Executes a single user authentication attempt.
// Why: Measures end-to-end authentication latency including password verification and token generation.
// Scenario: HTTP POST to /api/auth/login with JSON credentials { email, password }.
// Pass condition: API responds with HTTP 200 OK.
// Note: In k6, the default function is the per-VU iteration loop and cannot be selected as an independent unit test.
export default function () {
  const payload = JSON.stringify({
    email: EMAIL,
    password: PASSWORD,
  });

  const res = http.post(
    `${BASE_URL}/api/auth/login`,
    payload,
    {
      headers: { 'Content-Type': 'application/json' },
    }
  );

  check(res, {
    'login status 200': (r) => r.status === 200,
  });

  sleep(1);
}