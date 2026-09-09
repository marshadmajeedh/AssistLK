# Creating a New Tool

Tools allow agents to interact with
external systems or application services.

## Steps

1. Create tool class.

	Example:

	`ProviderSearchTool`

2. Implement `IAgentTool`.

3. Define:

	- Name
	- Description
	- `ExecuteAsync()`

4. Register tool in `ToolRegistry`.

5. Use `ToolExecutor` when required.
