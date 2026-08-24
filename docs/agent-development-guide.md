# Creating a New Agent

## Step 1

Create a new class.

Example:

`ProblemUnderstandingAgent`

## Step 2

Implement `IAgent`.

Example:

```csharp
public class ProblemUnderstandingAgent
	: IAgent
```

## Step 3

Define agent name.

Example:

```csharp
public string Name =>
	"ProblemUnderstandingAgent";
```

## Step 4

Implement `ExecuteAsync()`.

## Step 5

Register the agent in `AgentRegistry`.

## Step 6

Test through Agent Orchestrator.
