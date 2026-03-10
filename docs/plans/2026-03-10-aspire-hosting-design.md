# Aspire Hosting Integration Design

## Overview

Add .NET Aspire support for MailPeek so it can be easily included in Aspire-orchestrated applications. MailPeek stays embedded (not a container) — the Aspire integration makes the SMTP endpoint discoverable to other services.

## Decisions

- **Embedded model:** MailPeek runs inside a web project (like today), not as a separate container
- **Two-package approach:** `MailPeek.Hosting.Aspire` for AppHost, connection-string overload in existing `MailPeek` for consuming services
- **No Aspire dependency in base package:** Client integration reads from `IConfiguration` (standard .NET)

## Components

### 1. MailPeek.Hosting.Aspire Package

New project at `src/MailPeek.Hosting.Aspire/`. References `Aspire.Hosting`.

**MailPeekResource** — Custom resource implementing `IResourceWithConnectionString`. Exposes `smtp://localhost:{port}` as the connection string.

**MailPeekHostingExtensions** — `AddMailPeek(name, smtpPort)` on `IDistributedApplicationBuilder`. Returns `IResourceBuilder<MailPeekResource>` so it can be passed to `WithReference()`.

### 2. Client Integration (in MailPeek package)

New overload: `AddMailPeek(string connectionName)` on `IServiceCollection`. Reads `ConnectionStrings:{connectionName}` from `IConfiguration`, parses the `smtp://host:port` URI, and configures `MailPeekSmtpOptions` accordingly.

### 3. CI/CD Updates

Both CI and release workflows updated to pack and push `MailPeek.Hosting.Aspire` alongside `MailPeek`.

### 4. Solution Structure

Add `src/MailPeek.Hosting.Aspire/` to `MailPeek.slnx`.

## Usage

### AppHost
```csharp
var mailpeek = builder.AddMailPeek("mailpeek", smtpPort: 2525);
builder.AddProject<Projects.WebApp>("webapp").WithReference(mailpeek);
```

### Consuming Service
```csharp
builder.Services.AddMailPeek("mailpeek"); // reads connection string from Aspire
app.UseMailPeek();
```
