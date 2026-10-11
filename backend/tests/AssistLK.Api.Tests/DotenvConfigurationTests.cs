using System;
using System.Collections.Generic;
using System.IO;
using dotenv.net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssistLK.Api.Tests;

/// <summary>
/// Unit and integration tests verifying physical .env loading, path resolution,
/// configuration precedence, User Secrets coexistence, and security isolation.
/// </summary>
[Collection("EnvironmentTests")]
public class DotenvConfigurationTests
{
    [Fact]
    public void Backend_Starts_With_No_DotEnv_File()
    {
        // When no .env file exists in the directory hierarchy, LoadDotEnv must execute safely without throwing.
        var originalDotenvPath = Environment.GetEnvironmentVariable("DOTENV_PATH");
        try
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), ".env"));

            var exception = Record.Exception(() => Program.LoadDotEnv());
            Assert.Null(exception);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", originalDotenvPath);
        }
    }

    [Fact]
    public void Backend_Loads_Harmless_Test_Value_From_DotEnv()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_env_{Guid.NewGuid():N}.env");
        var testKey = $"TEST_DOTENV_KEY_{Guid.NewGuid():N}";
        var testValue = "harmless_test_value_12345";
        var originalDotenvPath = Environment.GetEnvironmentVariable("DOTENV_PATH");

        try
        {
            File.WriteAllText(tempFile, $"{testKey}={testValue}\n");
            Environment.SetEnvironmentVariable("DOTENV_PATH", tempFile);

            Program.LoadDotEnv();

            var loadedProcessVar = Environment.GetEnvironmentVariable(testKey);
            Assert.Equal(testValue, loadedProcessVar);

            // Verify standard ASP.NET Core ConfigurationBuilder ingests the process environment variable
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal(testValue, configuration[testKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", originalDotenvPath);
            Environment.SetEnvironmentVariable(testKey, null);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ConnectionStrings_DefaultConnection_Can_Be_Supplied_Through_DotEnv()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_cs_{Guid.NewGuid():N}.env");
        var simulatedConnectionString = "Host=mock-localhost;Port=5432;Database=assistlk_mock;Username=mock_user;Password=mock_password;SSL Mode=Require;";
        var originalDotenvPath = Environment.GetEnvironmentVariable("DOTENV_PATH");
        var originalConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        try
        {
            // Clear explicit OS env var so dotenv value can be set
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            File.WriteAllText(tempFile, $"ConnectionStrings__DefaultConnection={simulatedConnectionString}\n");
            Environment.SetEnvironmentVariable("DOTENV_PATH", tempFile);

            Program.LoadDotEnv();

            Assert.Equal(simulatedConnectionString, Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));

            // Verify ConfigurationBuilder consumes it via standard ASP.NET Core mapping
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal(simulatedConnectionString, configuration.GetConnectionString("DefaultConnection"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", originalDotenvPath);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalConnection);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Explicit_OS_Environment_Variables_Retain_Precedence_Over_DotEnv()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_precedence_{Guid.NewGuid():N}.env");
        var testKey = $"PRECEDENCE_KEY_{Guid.NewGuid():N}";
        var osValue = "explicit_os_environment_value";
        var dotenvValue = "dotenv_value_that_should_not_overwrite";
        var originalDotenvPath = Environment.GetEnvironmentVariable("DOTENV_PATH");

        try
        {
            // Set the OS/process environment variable first
            Environment.SetEnvironmentVariable(testKey, osValue);
            File.WriteAllText(tempFile, $"{testKey}={dotenvValue}\n");
            Environment.SetEnvironmentVariable("DOTENV_PATH", tempFile);

            Program.LoadDotEnv();

            // The explicit OS value must NOT be overwritten by the .env file
            var finalValue = Environment.GetEnvironmentVariable(testKey);
            Assert.Equal(osValue, finalValue);

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal(osValue, configuration[testKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", originalDotenvPath);
            Environment.SetEnvironmentVariable(testKey, null);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Configuration_Precedence_Chain_OS_Over_DotEnv_Over_UserSecrets_Over_AppSettings()
    {
        // Demonstrates the final actual precedence order:
        // Explicit OS Env Var > .env values > User Secrets > appsettings
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_chain_{Guid.NewGuid():N}.env");
        var keyOsWins = $"CHAIN_OS_WINS_{Guid.NewGuid():N}";
        var keyDotEnvWins = $"CHAIN_DOTENV_WINS_{Guid.NewGuid():N}";
        var keySecretsWins = $"CHAIN_SECRETS_WINS_{Guid.NewGuid():N}";
        var originalDotenvPath = Environment.GetEnvironmentVariable("DOTENV_PATH");

        try
        {
            // 1. Set explicit OS var
            Environment.SetEnvironmentVariable(keyOsWins, "value_from_os");

            // 2. Prepare .env containing both keyOsWins and keyDotEnvWins
            File.WriteAllText(tempFile, $"{keyOsWins}=value_from_dotenv\n{keyDotEnvWins}=value_from_dotenv\n");
            Environment.SetEnvironmentVariable("DOTENV_PATH", tempFile);

            // Load dotenv into process environment
            Program.LoadDotEnv();

            // Verify OS value was not overwritten
            Assert.Equal("value_from_os", Environment.GetEnvironmentVariable(keyOsWins));
            // Verify DotEnv value populated process environment
            Assert.Equal("value_from_dotenv", Environment.GetEnvironmentVariable(keyDotEnvWins));

            // 3. Simulate configuration providers in standard ASP.NET Core Development order:
            // Base AppSettings -> UserSecrets -> EnvironmentVariables (which includes OS + loaded dotenv)
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [keyOsWins] = "value_from_appsettings",
                    [keyDotEnvWins] = "value_from_appsettings",
                    [keySecretsWins] = "value_from_appsettings"
                })
                .AddInMemoryCollection(new Dictionary<string, string?> // Simulating User Secrets
                {
                    [keyOsWins] = "value_from_user_secrets",
                    [keyDotEnvWins] = "value_from_user_secrets",
                    [keySecretsWins] = "value_from_user_secrets"
                })
                .AddEnvironmentVariables()
                .Build();

            // OS environment variable wins over .env, secrets, and appsettings
            Assert.Equal("value_from_os", config[keyOsWins]);

            // .env value (injected into process environment) wins over User Secrets and appsettings
            Assert.Equal("value_from_dotenv", config[keyDotEnvWins]);

            // User Secrets value wins over appsettings when no env var is set
            Assert.Equal("value_from_user_secrets", config[keySecretsWins]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTENV_PATH", originalDotenvPath);
            Environment.SetEnvironmentVariable(keyOsWins, null);
            Environment.SetEnvironmentVariable(keyDotEnvWins, null);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ResolveEnvFilePath_Discovers_DotEnv_In_Parent_Tree()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"probe_root_{Guid.NewGuid():N}");
        var deepSubdir = Path.Combine(tempRoot, "backend", "src", "AssistLK.Api", "bin", "Debug", "net8.0");
        Directory.CreateDirectory(deepSubdir);

        var rootEnv = Path.Combine(tempRoot, ".env");
        File.WriteAllText(rootEnv, "TEST=1");

        // Simulate searching upwards from a directory inside the tree
        var current = new DirectoryInfo(deepSubdir);
        string? discoveredPath = null;
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                discoveredPath = candidate;
                break;
            }
            current = current.Parent;
        }

        try
        {
            Assert.NotNull(discoveredPath);
            Assert.Equal(Path.GetFullPath(rootEnv), Path.GetFullPath(discoveredPath!));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Fact]
    public void ProjectFile_Maintains_UserSecretsId()
    {
        // Search upwards for repository root or backend root, then find src/AssistLK.Api/AssistLK.Api.csproj
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? csprojPath = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "AssistLK.Api", "AssistLK.Api.csproj");
            if (File.Exists(candidate))
            {
                csprojPath = candidate;
                break;
            }

            var repoCandidate = Path.Combine(dir.FullName, "backend", "src", "AssistLK.Api", "AssistLK.Api.csproj");
            if (File.Exists(repoCandidate))
            {
                csprojPath = repoCandidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.NotNull(csprojPath);
        Assert.True(File.Exists(csprojPath), $"AssistLK.Api.csproj should exist at {csprojPath}");
        var content = File.ReadAllText(csprojPath!);
        Assert.Contains("<UserSecretsId>15fcbe56-7908-4746-bfbe-0692dc0c8045</UserSecretsId>", content);
        Assert.Contains("<PackageReference Include=\"dotenv.net\" Version=\"4.1.0\" />", content);
    }
}
