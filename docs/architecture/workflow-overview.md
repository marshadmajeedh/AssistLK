# AssistLK Workflow Overview

## Current C1 execution

C1 follows [the Python-only architecture](../../agent-services/README.md). Initial analysis can request Round 1 answers, then Round 2 answers if needed. Answered Round 2 is still re-analyzed; no Round 3 is created. If insufficient, the customer improves the main description. ASP.NET persists rounds and controls lifecycle; Python returns structured analysis. Transport failure restores the valid pre-analysis state through workflow recovery, with no native fallback.

Smart Location is Flutter GPS -> ASP.NET -> Nominatim infrastructure, separate from Python input location extraction. Later matching, quotation, booking, and tracking examples below are conceptual target workflows, not claims that all components are implemented.


## 1. Introduction

This document explains how the parts of AssistLK communicate and how a complete service request moves through the system.

```text
Customer Request
	-> Problem Understanding
	-> Provider Matching
	-> Quotation and Booking
	-> Service Tracking
	-> Service Completion
```

## 2. Complete Service Lifecycle

### Step 1: Customer creates a request

The customer submits a natural-language request such as "My vehicle stopped near Colombo". The API validates the input and creates a `ServiceRequest`.

### Step 2: Problem Understanding Agent

Component 1 reads the customer message, extracts information, and creates a structured request:

```json
{
	"service": "Vehicle Repair",
	"problem": "Possible battery issue",
	"location": "Colombo",
	"urgency": "High"
}
```

### Step 3: Store information in memory

The system stores structured context in shared workflow memory:

```text
Problem: Battery issue
Location: Colombo
Service: Vehicle Repair
```

Other agents access this information through `AgentMemoryService` rather than direct agent-to-agent calls.

### Step 4: Provider Matching Agent

Component 2 reads memory, searches providers, checks availability, ranks eligible providers, and returns recommendations such as ABC Auto Service, City Vehicle Care, and Fast Repair LK.

### Step 5: Customer selects a provider

The customer selects a provider such as ABC Auto Service. The selection is validated and stored in workflow memory for the quotation step.

### Step 6: Quotation process

Component 3 requests and collects quotations:

```text
Request Quotation
	-> Provider Receives Request
	-> Provider Sends Quotation
	-> Customer Reviews
	-> Customer Approval
```

Example quotation:

```text
Labour: 3000
Parts: 5000
Total: 8000
```

### Step 7: Booking creation

Booking creation requires customer approval and a safety check:

```text
Customer Approval
	-> Safety Check
	-> Create Booking
	-> Save Booking Data
```

The API owns authorization, approval verification, and the booking state transition.

### Step 8: Service tracking

Component 4 tracks the confirmed service:

```text
Booking Confirmed
	-> Provider Assigned
	-> Provider On The Way
	-> Provider Arrived
	-> Service Started
	-> Service Completed
```

### Step 9: Customer notifications

The system sends important updates, including:

```text
Your provider is arriving.
Your service has started.
Your service is completed.
```

### Step 10: Completion

```text
Service Completed
	-> Customer Confirmation
	-> Update History
	-> Close Workflow
```

Disputed or unsafe completion must be routed for review instead of being silently closed.

## 3. Agent Communication Flow

Agents do not directly communicate.

Incorrect:

```text
Problem Agent
	-> Provider Agent
```

Correct:

```text
Problem Agent
	-> Workflow Memory
	-> Provider Agent reads memory
```

Application workflow services control domain state and recovery; the orchestrator dispatches registered agents. C1 provider retries belong to Python.

## 4. Error Handling Workflow

When no provider is found:

```text
Provider Agent
	-> No Results
	-> Suggest Alternatives
	-> Ask Customer
```

Other failures must produce an explicit recoverable workflow status, record monitoring evidence, and avoid presenting incomplete data as a valid result.

## 5. Approval Workflow

Sensitive actions follow this process:

```text
Agent Decision
	-> Safety Policy Check
	-> Approval Required?
	-> Human Approval
	-> Execute Action
```

Booking creation, payment-related actions, cancellation of confirmed services, and other policy-defined sensitive actions must not bypass approval.

## 6. Monitoring Workflow

Every agent execution follows:

```text
Agent Starts
	-> Execute Task
	-> Record Metrics
	-> Save Result
```

Record the agent name, execution time, status, tool usage, errors, and correlation identifier where applicable through `AgentMonitoringService`.

## 7. Final End-to-End Example

```text
Customer: "My AC stopped working."
	-> Problem Understanding Agent detects AC Repair
	-> Provider Matching Agent finds three AC technicians
	-> Quotation Agent receives a quotation of Rs.5000
	-> Customer approves
	-> Booking is created
	-> Tracking Agent records technician arrival
	-> Service is completed and confirmed
```

## 8. Future Expansion

The workflow can support AI image diagnosis, voice requests, payment integration, GPS tracking, customer ratings, and predictive maintenance. These capabilities should be added through existing layers, tools, contracts, safety policies, and monitoring rather than by bypassing the architecture.
