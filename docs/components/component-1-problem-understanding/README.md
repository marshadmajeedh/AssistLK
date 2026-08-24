# Component 1: Problem Understanding

## Responsibility

Owns service request creation, request details, attachments, classification, clarification, history, cancellation, and admin review.

## AI contribution

The Problem Understanding Agent classifies the request, extracts structured details, identifies missing information, and suggests clarifying questions. Its output must be validated before use.

## Main integration points

- Backend service requests and domain rules
- React and Flutter request workflows
- Agent service `agents/problem_understanding/`
- Request, classification, and workflow schemas

## Completion criteria

A request is traceable from client submission through API validation, persisted history, AI evidence, and tests.
