using Xunit;

namespace AssistLK.Api.Tests;

/// <summary>
/// xUnit collection definition for tests that interact with or mutate process-wide environment variables
/// (e.g. DOTENV_PATH, ConnectionStrings__DefaultConnection, Jwt__Key).
/// Ensures tests within this collection run sequentially to avoid parallel race conditions.
/// </summary>
[CollectionDefinition("EnvironmentTests", DisableParallelization = true)]
public class EnvironmentTestsCollection
{
}
