# Aspire Hosting Integration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add .NET Aspire hosting support so MailPeek can be orchestrated in Aspire applications with SMTP endpoint discovery.

**Architecture:** New `MailPeek.Hosting.Aspire` package defines a custom Aspire resource exposing the SMTP endpoint. The existing `MailPeek` package gets a connection-string overload that reads Aspire-injected configuration. Both packages are published to NuGet.

**Tech Stack:** .NET Aspire Hosting SDK, ASP.NET Core, existing MailPeek library

---

### Task 1: Create MailPeek.Hosting.Aspire project

**Files:**
- Create: `src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj`

**Step 1: Create the project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>MailPeek.Hosting.Aspire</RootNamespace>
  </PropertyGroup>

  <PropertyGroup>
    <PackageId>MailPeek.Hosting.Aspire</PackageId>
    <Authors>Marcel Roozekrans</Authors>
    <Description>Aspire hosting integration for MailPeek. Adds MailPeek as a resource in your Aspire AppHost with SMTP endpoint discovery.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/MarcelRoozekrans/MailPeek</PackageProjectUrl>
    <RepositoryUrl>https://github.com/MarcelRoozekrans/MailPeek.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>smtp;email;testing;aspire;aspnetcore;mailpeek</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting" Version="9.*" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\MailPeek\icon.png" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Meziantou.Analyzer" Version="3.0.19">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Roslynator.Analyzers" Version="4.15.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="ErrorProne.NET.CoreAnalyzers" Version="0.1.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="ErrorProne.NET.Structs" Version="0.1.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="NetFabric.Hyperlinq.Analyzer" Version="2.3.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="ZeroAlloc.Analyzers" Version="1.0.3">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

**Step 2: Verify it builds**

Run: `dotnet build src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj`
Expected: Build succeeds (no source files yet, just the project skeleton).

**Step 3: Commit**

```bash
git add src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj
git commit -m "chore: add MailPeek.Hosting.Aspire project skeleton"
```

---

### Task 2: Implement MailPeekResource

**Files:**
- Create: `src/MailPeek.Hosting.Aspire/MailPeekResource.cs`

**Step 1: Create the resource class**

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace MailPeek.Hosting.Aspire;

public class MailPeekResource(string name, int smtpPort)
    : Resource(name), IResourceWithConnectionString
{
    public int SmtpPort { get; } = smtpPort;

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"smtp://localhost:{SmtpPort}");
}
```

**Step 2: Verify it builds**

Run: `dotnet build src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/MailPeek.Hosting.Aspire/MailPeekResource.cs
git commit -m "feat: add MailPeekResource for Aspire hosting"
```

---

### Task 3: Implement hosting extension method

**Files:**
- Create: `src/MailPeek.Hosting.Aspire/MailPeekHostingExtensions.cs`

**Step 1: Create the extension class**

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace MailPeek.Hosting.Aspire;

public static class MailPeekHostingExtensions
{
    public static IResourceBuilder<MailPeekResource> AddMailPeek(
        this IDistributedApplicationBuilder builder,
        string name = "mailpeek",
        int smtpPort = 2525)
    {
        var resource = new MailPeekResource(name, smtpPort);
        return builder.AddResource(resource);
    }
}
```

**Step 2: Verify it builds**

Run: `dotnet build src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/MailPeek.Hosting.Aspire/MailPeekHostingExtensions.cs
git commit -m "feat: add AddMailPeek extension for Aspire AppHost"
```

---

### Task 4: Add connection-string overload to MailPeek

**Files:**
- Modify: `src/MailPeek/Extensions/ServiceCollectionExtensions.cs`

**Step 1: Write the failing test**

Create test file `tests/MailPeek.Tests/Extensions/ServiceCollectionExtensionsTests.cs`:

```csharp
using MailPeek.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MailPeek.Configuration;

namespace MailPeek.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMailPeek_WithConnectionName_ConfiguresFromConnectionString()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:mailpeek"] = "smtp://myhost:3025"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("mailpeek");

        // Act
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;

        // Assert
        Assert.Equal("myhost", options.Hostname);
        Assert.Equal(3025, options.Port);
    }

    [Fact]
    public void AddMailPeek_WithConnectionName_DefaultsPort2525WhenNoPortInUri()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:mailpeek"] = "smtp://localhost"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("mailpeek");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;

        Assert.Equal("localhost", options.Hostname);
        Assert.Equal(2525, options.Port);
    }

    [Fact]
    public void AddMailPeek_WithMissingConnectionString_ThrowsArgumentException()
    {
        var config = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddMailPeek("nonexistent");

        var provider = services.BuildServiceProvider();
        Assert.Throws<ArgumentException>(() =>
            provider.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: FAIL — no overload `AddMailPeek(string)` exists.

**Step 3: Implement the connection-string overload**

Modify `src/MailPeek/Extensions/ServiceCollectionExtensions.cs`. Add a second overload after the existing method:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MailPeek.Configuration;
using MailPeek.Hubs;
using MailPeek.Smtp;
using MailPeek.Storage;

namespace MailPeek.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMailPeek(
        this IServiceCollection services,
        Action<MailPeekSmtpOptions>? configureOptions = null)
    {
        var options = new MailPeekSmtpOptions();
        configureOptions?.Invoke(options);

        services.Configure<MailPeekSmtpOptions>(opts =>
        {
            opts.Port = options.Port;
            opts.Hostname = options.Hostname;
            opts.MaxMessages = options.MaxMessages;
            opts.MaxMessageSize = options.MaxMessageSize;
        });

        services.AddSingleton<IMessageStore>(new InMemoryMessageStore(options.MaxMessages));
        services.AddSingleton<MailPeekHubNotifier>();
        services.AddHostedService<MailPeekSmtpHostedService>();
        services.AddSignalR();

        return services;
    }

    public static IServiceCollection AddMailPeek(
        this IServiceCollection services,
        string connectionName)
    {
        services.AddSingleton<IConfigureOptions<MailPeekSmtpOptions>>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString(connectionName);

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(
                    $"Connection string '{connectionName}' not found in configuration.");
            }

            var uri = new Uri(connectionString);
            var hostname = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 2525;

            return new ConfigureNamedOptions<MailPeekSmtpOptions>(null, opts =>
            {
                opts.Hostname = hostname;
                opts.Port = port;
            });
        });

        services.AddSingleton<IMessageStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MailPeekSmtpOptions>>().Value;
            return new InMemoryMessageStore(options.MaxMessages);
        });
        services.AddSingleton<MailPeekHubNotifier>();
        services.AddHostedService<MailPeekSmtpHostedService>();
        services.AddSignalR();

        return services;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MailPeek.Tests/ --filter "FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: All 3 tests PASS.

**Step 5: Run all tests to verify no regressions**

Run: `dotnet test`
Expected: All tests pass.

**Step 6: Commit**

```bash
git add src/MailPeek/Extensions/ServiceCollectionExtensions.cs tests/MailPeek.Tests/Extensions/ServiceCollectionExtensionsTests.cs
git commit -m "feat: add connection-string overload for Aspire integration"
```

---

### Task 5: Add project to solution

**Files:**
- Modify: `MailPeek.slnx`

**Step 1: Update the solution file**

Edit `MailPeek.slnx` to add the new project under `/src/`:

```xml
<Solution>
  <Folder Name="/samples/">
    <Project Path="samples/SampleApp/SampleApp.csproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/MailPeek/MailPeek.csproj" />
    <Project Path="src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/MailPeek.Tests/MailPeek.Tests.csproj" />
  </Folder>
</Solution>
```

**Step 2: Verify solution builds**

Run: `dotnet build`
Expected: All projects build successfully.

**Step 3: Commit**

```bash
git add MailPeek.slnx
git commit -m "chore: add MailPeek.Hosting.Aspire to solution"
```

---

### Task 6: Update CI/CD workflows to pack both packages

**Files:**
- Modify: `.github/workflows/ci.yml:46`
- Modify: `.github/workflows/release.yml:44-45`

**Step 1: Update CI workflow**

In `.github/workflows/ci.yml`, replace the single Pack step (line 46) with packing both projects. Change:

```yaml
      - name: Pack
        run: dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.gitversion.outputs.version }} -o ./artifacts
```

To:

```yaml
      - name: Pack
        run: |
          dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.gitversion.outputs.version }} -o ./artifacts
          dotnet pack src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj --no-build -c Release -p:PackageVersion=${{ steps.gitversion.outputs.version }} -o ./artifacts
```

**Step 2: Update Release workflow**

In `.github/workflows/release.yml`, replace the single Pack step (line 44-45) with packing both projects. Change:

```yaml
      - name: Pack
        run: dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.version.outputs.version }} -o ./artifacts
```

To:

```yaml
      - name: Pack
        run: |
          dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.version.outputs.version }} -o ./artifacts
          dotnet pack src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj --no-build -c Release -p:PackageVersion=${{ steps.version.outputs.version }} -o ./artifacts
```

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml .github/workflows/release.yml
git commit -m "ci: pack and publish MailPeek.Hosting.Aspire alongside MailPeek"
```

---

### Task 7: Update README with Aspire usage

**Files:**
- Modify: `README.md`

**Step 1: Add Aspire section to README**

Add after the "Testing" section (before "Dashboard Pages"):

```markdown
## Aspire Integration

MailPeek integrates with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/) for service discovery.

### AppHost

Install the hosting package in your AppHost project:

```bash
dotnet add package MailPeek.Hosting.Aspire
```

```csharp
var mailpeek = builder.AddMailPeek("mailpeek", smtpPort: 2525);

builder.AddProject<Projects.WebApp>("webapp")
    .WithReference(mailpeek);
```

### Consuming Service

In the web project that hosts the MailPeek dashboard:

```csharp
builder.Services.AddMailPeek("mailpeek"); // reads SMTP config from Aspire
app.UseMailPeek();
```

Other services can read the SMTP connection string directly:

```csharp
var smtp = builder.Configuration.GetConnectionString("mailpeek");
// → "smtp://localhost:2525"
```
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add Aspire integration section to README"
```

---

### Task 8: Verify end-to-end locally

**Step 1: Run full build**

Run: `dotnet build -c Release`
Expected: All projects build (MailPeek for net8.0+net9.0, MailPeek.Hosting.Aspire for net9.0).

**Step 2: Run tests**

Run: `dotnet test -c Release`
Expected: All tests pass (including new ServiceCollectionExtensionsTests).

**Step 3: Pack both packages**

Run:
```bash
dotnet pack src/MailPeek/MailPeek.csproj -c Release -o ./artifacts
dotnet pack src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj -c Release -o ./artifacts
```
Expected: Both `.nupkg` files created in `./artifacts/`.

**Step 4: Clean up**

Run: `rm -rf ./artifacts`
