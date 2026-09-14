# Component 2: Provider Matching

> **Scope:** This overview describes planned component responsibilities and completion criteria, not verified implementation status.

## Responsibility

Owns provider profiles, skills, categories, availability, service areas, verification, provider search, matching, and accept or reject actions.

## AI contribution

The Provider Matching Agent ranks eligible providers using validated request requirements, skills, availability, location, verification, and urgency. Recommendations do not bypass provider eligibility rules.

## Main integration points

- Backend providers and matching use cases
- React admin and provider workflows
- Flutter provider workflows
- Agent integration location is not established by this overview; follow the shared internal-service boundary.

## Completion criteria

Matching is explainable at a concise business level, respects authorization and availability, and is covered by deterministic tests.
