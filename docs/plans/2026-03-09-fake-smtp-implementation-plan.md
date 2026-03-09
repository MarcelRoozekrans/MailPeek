# Fake SMTP Server with Dashboard — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a NuGet package that provides an in-memory fake SMTP server with a real-time Hangfire-style web dashboard for ASP.NET Core apps.

**Architecture:** SmtpServer lib handles SMTP protocol, MimeKit parses messages, in-memory ConcurrentDictionary stores them, ASP.NET Core middleware serves embedded static dashboard + REST API + SignalR hub for real-time updates.

**Tech Stack:** .NET 8/9 multi-target, SmtpServer (NuGet), MimeKit, ASP.NET Core SignalR, embedded static HTML/JS/CSS

---

### Task 1: Solution & Project Scaffolding

**Files:**
- Create: `SmtpServer.Dashboard.sln`
- Create: `src/SmtpServer.Dashboard/SmtpServer.Dashboard.csproj`
- Create: `tests/SmtpServer.Dashboard.Tests/SmtpServer.Dashboard.Tests.csproj`
- Create: `samples/SampleApp/SampleApp.csproj`
- Create: `.gitignore`

**Step 1: Create solution and projects**

```bash
dotnet new sln -n SmtpServer.Dashboard
mkdir -p src/SmtpServer.Dashboard
mkdir -p tests/SmtpServer.Dashboard.Tests
mkdir -p samples/SampleApp
dotnet new classlib -n SmtpServer.Dashboard -o src/SmtpServer.Dashboard -f net8.0
dotnet new xunit -n SmtpServer.Dashboard.Tests -o tests/SmtpServer.Dashboard.Tests -f net8.0
dotnet new web -n SampleApp -o samples/SampleApp -f net8.0
dotnet sln add src/SmtpServer.Dashboard/SmtpServer.Dashboard.csproj
dotnet sln add tests/SmtpServer.Dashboard.Tests/SmtpServer.Dashboard.Tests.csproj
dotnet sln add samples/SampleApp/SampleApp.csproj
```

**Step 2: Configure multi-targeting and dependencies for main project**

Edit `src/SmtpServer.Dashboard/SmtpServer.Dashboard.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>SmtpServer.Dashboard</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="SmtpServer" Version="10.*" />
    <PackageReference Include="MimeKit" Version="4.*" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Assets\**\*" />
  </ItemGroup>

</Project>
```

**Step 3: Configure test project**

Edit `tests/SmtpServer.Dashboard.Tests/SmtpServer.Dashboard.Tests.csproj` to reference the main project:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\SmtpServer.Dashboard\SmtpServer.Dashboard.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.*" />
</ItemGroup>
```

**Step 4: Configure sample app**

Edit `samples/SampleApp/SampleApp.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\SmtpServer.Dashboard\SmtpServer.Dashboard.csproj" />
</ItemGroup>
```

**Step 5: Add .gitignore**

```
bin/
obj/
.vs/
*.user
*.suo
```

**Step 6: Build to verify**

```bash
dotnet build
```
Expected: Build succeeded with 0 errors.

**Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution with library, test, and sample projects"
```

---

### Task 2: Domain Models & Options

**Files:**
- Create: `src/SmtpServer.Dashboard/Models/StoredMessage.cs`
- Create: `src/SmtpServer.Dashboard/Models/StoredAttachment.cs`
- Create: `src/SmtpServer.Dashboard/Models/MessageSummary.cs`
- Create: `src/SmtpServer.Dashboard/Configuration/FakeSmtpOptions.cs`
- Create: `src/SmtpServer.Dashboard/Configuration/FakeDashboardOptions.cs`
- Create: `src/SmtpServer.Dashboard/Authorization/ISmtpDashboardAuthorizationFilter.cs`
- Create: `src/SmtpServer.Dashboard/Authorization/DashboardContext.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Models/StoredMessageTests.cs`

**Step 1: Write tests for StoredMessage**

```csharp
// tests/SmtpServer.Dashboard.Tests/Models/StoredMessageTests.cs
using SmtpServer.Dashboard.Models;

namespace SmtpServer.Dashboard.Tests.Models;

public class StoredMessageTests
{
    [Fact]
    public void Constructor_SetsIdAndReceivedDate()
    {
        var msg = new StoredMessage();

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.True(msg.ReceivedAt <= DateTimeOffset.UtcNow);
        Assert.True(msg.ReceivedAt > DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void HasAttachments_ReturnsTrueWhenAttachmentsExist()
    {
        var msg = new StoredMessage();
        msg.Attachments.Add(new StoredAttachment
        {
            FileName = "test.txt",
            ContentType = "text/plain",
            Content = new byte[] { 1, 2, 3 }
        });

        Assert.True(msg.HasAttachments);
    }

    [Fact]
    public void HasAttachments_ReturnsFalseWhenEmpty()
    {
        var msg = new StoredMessage();
        Assert.False(msg.HasAttachments);
    }

    [Fact]
    public void ToSummary_MapsCorrectly()
    {
        var msg = new StoredMessage
        {
            From = "sender@test.com",
            To = ["recipient@test.com"],
            Subject = "Hello",
        };
        msg.Attachments.Add(new StoredAttachment
        {
            FileName = "file.pdf",
            ContentType = "application/pdf",
            Content = []
        });

        var summary = msg.ToSummary();

        Assert.Equal(msg.Id, summary.Id);
        Assert.Equal("sender@test.com", summary.From);
        Assert.Equal("recipient@test.com", summary.To.First());
        Assert.Equal("Hello", summary.Subject);
        Assert.True(summary.HasAttachments);
        Assert.Equal(msg.ReceivedAt, summary.ReceivedAt);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~StoredMessageTests" -v minimal
```
Expected: FAIL — types don't exist yet.

**Step 3: Create the models**

```csharp
// src/SmtpServer.Dashboard/Models/StoredAttachment.cs
namespace SmtpServer.Dashboard.Models;

public class StoredAttachment
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Content { get; set; }
}
```

```csharp
// src/SmtpServer.Dashboard/Models/MessageSummary.cs
namespace SmtpServer.Dashboard.Models;

public class MessageSummary
{
    public required Guid Id { get; set; }
    public required string From { get; set; }
    public required List<string> To { get; set; }
    public required string Subject { get; set; }
    public required bool HasAttachments { get; set; }
    public required DateTimeOffset ReceivedAt { get; set; }
}
```

```csharp
// src/SmtpServer.Dashboard/Models/StoredMessage.cs
namespace SmtpServer.Dashboard.Models;

public class StoredMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public List<string> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public List<StoredAttachment> Attachments { get; set; } = [];
    public Dictionary<string, string> Headers { get; set; } = [];
    public byte[]? RawMessage { get; set; }
    public bool ParseError { get; set; }
    public string? ParseErrorMessage { get; set; }

    public bool HasAttachments => Attachments.Count > 0;

    public MessageSummary ToSummary() => new()
    {
        Id = Id,
        From = From,
        To = To,
        Subject = Subject,
        HasAttachments = HasAttachments,
        ReceivedAt = ReceivedAt
    };
}
```

**Step 4: Create configuration and authorization types**

```csharp
// src/SmtpServer.Dashboard/Configuration/FakeSmtpOptions.cs
namespace SmtpServer.Dashboard.Configuration;

public class FakeSmtpOptions
{
    public int Port { get; set; } = 2525;
    public string Hostname { get; set; } = "localhost";
    public int MaxMessages { get; set; } = 1000;
    public long MaxMessageSize { get; set; } = 10_000_000;
}
```

```csharp
// src/SmtpServer.Dashboard/Configuration/FakeDashboardOptions.cs
using SmtpServer.Dashboard.Authorization;

namespace SmtpServer.Dashboard.Configuration;

public class FakeDashboardOptions
{
    public string PathPrefix { get; set; } = "/smtp";
    public ISmtpDashboardAuthorizationFilter[] Authorization { get; set; } = [];
    public string Title { get; set; } = "Fake SMTP Dashboard";
}
```

```csharp
// src/SmtpServer.Dashboard/Authorization/DashboardContext.cs
using Microsoft.AspNetCore.Http;

namespace SmtpServer.Dashboard.Authorization;

public class DashboardContext(HttpContext httpContext)
{
    public HttpContext HttpContext { get; } = httpContext;
}
```

```csharp
// src/SmtpServer.Dashboard/Authorization/ISmtpDashboardAuthorizationFilter.cs
namespace SmtpServer.Dashboard.Authorization;

public interface ISmtpDashboardAuthorizationFilter
{
    bool Authorize(DashboardContext context);
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~StoredMessageTests" -v minimal
```
Expected: All 4 tests PASS.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add domain models, configuration options, and authorization interfaces"
```

---

### Task 3: IMessageStore & InMemoryMessageStore

**Files:**
- Create: `src/SmtpServer.Dashboard/Storage/IMessageStore.cs`
- Create: `src/SmtpServer.Dashboard/Storage/InMemoryMessageStore.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Storage/InMemoryMessageStoreTests.cs`

**Step 1: Write tests**

```csharp
// tests/SmtpServer.Dashboard.Tests/Storage/InMemoryMessageStoreTests.cs
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Storage;

public class InMemoryMessageStoreTests
{
    private readonly InMemoryMessageStore _store = new(maxMessages: 5);

    [Fact]
    public void Add_StoresMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var all = _store.GetAll();
        Assert.Single(all);
        Assert.Equal("Hello", all.First().Subject);
    }

    [Fact]
    public void GetAll_ReturnsNewestFirst()
    {
        var msg1 = CreateMessage("a@test.com", "First");
        var msg2 = CreateMessage("b@test.com", "Second");
        _store.Add(msg1);
        _store.Add(msg2);

        var all = _store.GetAll().ToList();
        Assert.Equal("Second", all[0].Subject);
        Assert.Equal("First", all[1].Subject);
    }

    [Fact]
    public void GetById_ReturnsCorrectMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var found = _store.GetById(msg.Id);
        Assert.NotNull(found);
        Assert.Equal(msg.Id, found.Id);
    }

    [Fact]
    public void GetById_ReturnsNullForMissing()
    {
        Assert.Null(_store.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void Delete_RemovesMessage()
    {
        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        var deleted = _store.Delete(msg.Id);
        Assert.True(deleted);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Delete_ReturnsFalseForMissing()
    {
        Assert.False(_store.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        _store.Add(CreateMessage("a@test.com", "First"));
        _store.Add(CreateMessage("b@test.com", "Second"));

        _store.Clear();
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public void Add_EvictsOldestWhenMaxReached()
    {
        for (var i = 0; i < 6; i++)
        {
            _store.Add(CreateMessage($"user{i}@test.com", $"Message {i}"));
        }

        var all = _store.GetAll().ToList();
        Assert.Equal(5, all.Count);
        Assert.DoesNotContain(all, m => m.Subject == "Message 0");
        Assert.Contains(all, m => m.Subject == "Message 5");
    }

    [Fact]
    public void OnMessageReceived_FiresWhenMessageAdded()
    {
        StoredMessage? received = null;
        _store.OnMessageReceived += msg => received = msg;

        var msg = CreateMessage("test@example.com", "Hello");
        _store.Add(msg);

        Assert.NotNull(received);
        Assert.Equal(msg.Id, received.Id);
    }

    [Fact]
    public void GetPage_ReturnsPaginatedResults()
    {
        for (var i = 0; i < 10; i++)
        {
            _store = new InMemoryMessageStore(maxMessages: 20);
        }
        var store = new InMemoryMessageStore(maxMessages: 20);
        for (var i = 0; i < 10; i++)
        {
            store.Add(CreateMessage($"user{i}@test.com", $"Message {i}"));
        }

        var page = store.GetPage(pageNumber: 1, pageSize: 3);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(10, page.TotalCount);
    }

    private static StoredMessage CreateMessage(string from, string subject) => new()
    {
        From = from,
        To = ["recipient@test.com"],
        Subject = subject
    };
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~InMemoryMessageStoreTests" -v minimal
```
Expected: FAIL — types don't exist yet.

**Step 3: Implement IMessageStore and InMemoryMessageStore**

```csharp
// src/SmtpServer.Dashboard/Storage/IMessageStore.cs
using SmtpServer.Dashboard.Models;

namespace SmtpServer.Dashboard.Storage;

public class PagedResult<T>
{
    public required List<T> Items { get; set; }
    public required int TotalCount { get; set; }
}

public interface IMessageStore
{
    void Add(StoredMessage message);
    IReadOnlyList<StoredMessage> GetAll();
    StoredMessage? GetById(Guid id);
    bool Delete(Guid id);
    void Clear();
    PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null);
    event Action<StoredMessage>? OnMessageReceived;
}
```

```csharp
// src/SmtpServer.Dashboard/Storage/InMemoryMessageStore.cs
using System.Collections.Concurrent;
using SmtpServer.Dashboard.Models;

namespace SmtpServer.Dashboard.Storage;

public class InMemoryMessageStore(int maxMessages = 1000) : IMessageStore
{
    private readonly ConcurrentDictionary<Guid, StoredMessage> _messages = new();
    private readonly LinkedList<Guid> _order = new();
    private readonly Lock _orderLock = new();

    public event Action<StoredMessage>? OnMessageReceived;

    public void Add(StoredMessage message)
    {
        _messages[message.Id] = message;

        lock (_orderLock)
        {
            _order.AddFirst(message.Id);

            while (_order.Count > maxMessages)
            {
                var oldest = _order.Last!.Value;
                _order.RemoveLast();
                _messages.TryRemove(oldest, out _);
            }
        }

        OnMessageReceived?.Invoke(message);
    }

    public IReadOnlyList<StoredMessage> GetAll()
    {
        lock (_orderLock)
        {
            return _order
                .Select(id => _messages.GetValueOrDefault(id))
                .Where(m => m is not null)
                .ToList()!;
        }
    }

    public StoredMessage? GetById(Guid id) =>
        _messages.GetValueOrDefault(id);

    public bool Delete(Guid id)
    {
        if (!_messages.TryRemove(id, out _))
            return false;

        lock (_orderLock)
        {
            _order.Remove(id);
        }

        return true;
    }

    public void Clear()
    {
        lock (_orderLock)
        {
            _order.Clear();
        }

        _messages.Clear();
    }

    public PagedResult<StoredMessage> GetPage(int pageNumber, int pageSize, string? searchTerm = null)
    {
        var all = GetAll();

        IEnumerable<StoredMessage> filtered = all;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            filtered = all.Where(m =>
                m.Subject.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.From.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.To.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var list = filtered.ToList();

        return new PagedResult<StoredMessage>
        {
            Items = list.Skip(pageNumber * pageSize).Take(pageSize).ToList(),
            TotalCount = list.Count
        };
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~InMemoryMessageStoreTests" -v minimal
```
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add IMessageStore interface and InMemoryMessageStore with FIFO eviction"
```

---

### Task 4: SMTP Message Handler & Hosted Service

**Files:**
- Create: `src/SmtpServer.Dashboard/Smtp/FakeSmtpMessageStore.cs`
- Create: `src/SmtpServer.Dashboard/Smtp/FakeSmtpHostedService.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Smtp/FakeSmtpMessageStoreTests.cs`

**Step 1: Write tests for message handler (MimeKit parsing)**

```csharp
// tests/SmtpServer.Dashboard.Tests/Smtp/FakeSmtpMessageStoreTests.cs
using System.Buffers;
using MimeKit;
using SmtpServer.Dashboard.Smtp;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Smtp;

public class FakeSmtpMessageStoreTests
{
    [Fact]
    public async Task ParseAndStore_ParsesSimpleMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new FakeSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Cc.Add(new MailboxAddress("CC User", "cc@test.com"));
        mime.Subject = "Test Subject";
        mime.Body = new TextPart("plain") { Text = "Hello World" };

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);
        var raw = stream.ToArray();

        handler.ParseAndStore(raw);

        var messages = store.GetAll();
        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal("sender@test.com", msg.From);
        Assert.Contains("recipient@test.com", msg.To);
        Assert.Contains("cc@test.com", msg.Cc);
        Assert.Equal("Test Subject", msg.Subject);
        Assert.Equal("Hello World", msg.TextBody);
    }

    [Fact]
    public async Task ParseAndStore_ParsesHtmlMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new FakeSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Subject = "HTML Test";

        var builder = new BodyBuilder
        {
            TextBody = "Plain text",
            HtmlBody = "<h1>Hello</h1>"
        };
        mime.Body = builder.ToMessageBody();

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);

        handler.ParseAndStore(stream.ToArray());

        var msg = store.GetAll().Single();
        Assert.Equal("Plain text", msg.TextBody);
        Assert.Equal("<h1>Hello</h1>", msg.HtmlBody);
    }

    [Fact]
    public async Task ParseAndStore_ParsesAttachments()
    {
        var store = new InMemoryMessageStore();
        var handler = new FakeSmtpMessageStore(store);

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress("Sender", "sender@test.com"));
        mime.To.Add(new MailboxAddress("Recipient", "recipient@test.com"));
        mime.Subject = "With Attachment";

        var builder = new BodyBuilder { TextBody = "See attached" };
        builder.Attachments.Add("test.txt", System.Text.Encoding.UTF8.GetBytes("file content"),
            new ContentType("text", "plain"));
        mime.Body = builder.ToMessageBody();

        using var stream = new MemoryStream();
        await mime.WriteToAsync(stream);

        handler.ParseAndStore(stream.ToArray());

        var msg = store.GetAll().Single();
        Assert.True(msg.HasAttachments);
        Assert.Single(msg.Attachments);
        Assert.Equal("test.txt", msg.Attachments[0].FileName);
    }

    [Fact]
    public void ParseAndStore_HandlesMalformedMessage()
    {
        var store = new InMemoryMessageStore();
        var handler = new FakeSmtpMessageStore(store);

        handler.ParseAndStore([0xFF, 0xFE, 0x00]);

        var msg = store.GetAll().Single();
        Assert.True(msg.ParseError);
        Assert.NotNull(msg.ParseErrorMessage);
        Assert.NotNull(msg.RawMessage);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~FakeSmtpMessageStoreTests" -v minimal
```
Expected: FAIL.

**Step 3: Implement FakeSmtpMessageStore (the handler that bridges SmtpServer lib → our IMessageStore)**

```csharp
// src/SmtpServer.Dashboard/Smtp/FakeSmtpMessageStore.cs
using System.Buffers;
using MimeKit;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace SmtpServer.Dashboard.Smtp;

public class FakeSmtpMessageStore(IMessageStore messageStore) : MessageStore
{
    public override async Task<SmtpResponse> SaveAsync(
        ISessionContext context,
        IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
    {
        var raw = buffer.ToArray();
        ParseAndStore(raw);
        return SmtpResponse.Ok;
    }

    public void ParseAndStore(byte[] raw)
    {
        var storedMessage = new StoredMessage { RawMessage = raw };

        try
        {
            using var stream = new MemoryStream(raw);
            var mime = MimeMessage.Load(stream);

            storedMessage.From = mime.From.Mailboxes.FirstOrDefault()?.Address ?? string.Empty;
            storedMessage.To = mime.To.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Cc = mime.Cc.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Bcc = mime.Bcc.Mailboxes.Select(m => m.Address).ToList();
            storedMessage.Subject = mime.Subject ?? string.Empty;
            storedMessage.TextBody = mime.TextBody;
            storedMessage.HtmlBody = mime.HtmlBody;

            foreach (var header in mime.Headers)
            {
                storedMessage.Headers[header.Field] = header.Value;
            }

            foreach (var attachment in mime.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    part.Content.DecodeTo(ms);

                    storedMessage.Attachments.Add(new StoredAttachment
                    {
                        FileName = part.FileName ?? "unknown",
                        ContentType = part.ContentType.MimeType,
                        Content = ms.ToArray()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            storedMessage.ParseError = true;
            storedMessage.ParseErrorMessage = ex.Message;
        }

        messageStore.Add(storedMessage);
    }
}
```

**Step 4: Implement FakeSmtpHostedService**

```csharp
// src/SmtpServer.Dashboard/Smtp/FakeSmtpHostedService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Storage;
using SmtpServer.ComponentModel;
using SmtpServerLib = SmtpServer;

namespace SmtpServer.Dashboard.Smtp;

public class FakeSmtpHostedService(
    IOptions<FakeSmtpOptions> options,
    IMessageStore messageStore,
    ILogger<FakeSmtpHostedService> logger) : IHostedService, IDisposable
{
    private SmtpServerLib.SmtpServer? _smtpServer;
    private CancellationTokenSource? _cts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        logger.LogInformation("Starting fake SMTP server on {Hostname}:{Port}", opts.Hostname, opts.Port);

        var serverOptions = new SmtpServerLib.SmtpServerOptionsBuilder()
            .ServerName(opts.Hostname)
            .Port(opts.Port)
            .MaxMessageSize(opts.MaxMessageSize)
            .Build();

        var serviceProvider = new SmtpServerLib.ComponentModel.ServiceProvider();
        var smtpMessageStore = new FakeSmtpMessageStore(messageStore);
        serviceProvider.Add(smtpMessageStore as SmtpServer.Storage.IMessageStore);

        _smtpServer = new SmtpServerLib.SmtpServer(serverOptions, serviceProvider);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _ = Task.Run(async () =>
        {
            try
            {
                await _smtpServer.StartAsync(_cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "SMTP server error");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping fake SMTP server");
        _cts?.Cancel();
        _smtpServer?.Shutdown();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }
}
```

> **Note:** The SmtpServer NuGet package has its own `IMessageStore` and `ServiceProvider`. The exact API may differ slightly between versions. Verify against the installed version and adjust if needed.

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~FakeSmtpMessageStoreTests" -v minimal
```
Expected: All 4 tests PASS.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add SMTP message handler with MimeKit parsing and hosted service"
```

---

### Task 5: SignalR Hub

**Files:**
- Create: `src/SmtpServer.Dashboard/Hubs/SmtpDashboardHub.cs`
- Create: `src/SmtpServer.Dashboard/Hubs/SmtpDashboardHubNotifier.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Hubs/SmtpDashboardHubNotifierTests.cs`

**Step 1: Write test for the notifier**

```csharp
// tests/SmtpServer.Dashboard.Tests/Hubs/SmtpDashboardHubNotifierTests.cs
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Hubs;

public class SmtpDashboardHubNotifierTests
{
    [Fact]
    public void WhenMessageAdded_NotifiesClients()
    {
        var store = new InMemoryMessageStore();
        var hubContext = Substitute.For<IHubContext<SmtpDashboardHub>>();
        var clients = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.All.Returns(clientProxy);

        var notifier = new SmtpDashboardHubNotifier(store, hubContext);
        notifier.Start();

        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        store.Add(msg);

        clientProxy.Received(1).SendCoreAsync(
            "NewMessage",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }
}
```

**Step 2: Add NSubstitute to test project**

```bash
dotnet add tests/SmtpServer.Dashboard.Tests package NSubstitute
```

**Step 3: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~SmtpDashboardHubNotifierTests" -v minimal
```
Expected: FAIL.

**Step 4: Implement hub and notifier**

```csharp
// src/SmtpServer.Dashboard/Hubs/SmtpDashboardHub.cs
using Microsoft.AspNetCore.SignalR;

namespace SmtpServer.Dashboard.Hubs;

public class SmtpDashboardHub : Hub
{
    // Client methods: NewMessage, MessageDeleted, MessagesCleared
    // No server-callable methods needed — dashboard is read-only via SignalR
}
```

```csharp
// src/SmtpServer.Dashboard/Hubs/SmtpDashboardHubNotifier.cs
using Microsoft.AspNetCore.SignalR;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Hubs;

public class SmtpDashboardHubNotifier(
    IMessageStore store,
    IHubContext<SmtpDashboardHub> hubContext)
{
    public void Start()
    {
        store.OnMessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(StoredMessage message)
    {
        _ = hubContext.Clients.All.SendAsync("NewMessage", message.ToSummary());
    }

    public Task NotifyMessageDeleted(Guid id) =>
        hubContext.Clients.All.SendAsync("MessageDeleted", id);

    public Task NotifyMessagesCleared() =>
        hubContext.Clients.All.SendAsync("MessagesCleared");
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~SmtpDashboardHubNotifierTests" -v minimal
```
Expected: PASS.

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add SignalR hub and notifier for real-time dashboard updates"
```

---

### Task 6: REST API Middleware

**Files:**
- Create: `src/SmtpServer.Dashboard/Middleware/DashboardApiMiddleware.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Middleware/DashboardApiMiddlewareTests.cs`

**Step 1: Write integration tests for API endpoints**

```csharp
// tests/SmtpServer.Dashboard.Tests/Middleware/DashboardApiMiddlewareTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmtpServer.Dashboard.Models;
using SmtpServer.Dashboard.Storage;
using SmtpServer.Dashboard.Middleware;

namespace SmtpServer.Dashboard.Tests.Middleware;

public class DashboardApiMiddlewareTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;
    private InMemoryMessageStore? _store;

    public async Task InitializeAsync()
    {
        _store = new InMemoryMessageStore();
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IMessageStore>(_store);
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseDashboardApi("/smtp");
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    [Fact]
    public async Task GetMessages_ReturnsEmptyList()
    {
        var response = await _client!.GetAsync("/smtp/api/messages");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetMessages_ReturnsStoredMessages()
    {
        _store!.Add(new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Hello"
        });

        var response = await _client!.GetAsync("/smtp/api/messages");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetMessageById_ReturnsMessage()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store!.Add(msg);

        var response = await _client!.GetAsync($"/smtp/api/messages/{msg.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMessageById_Returns404ForMissing()
    {
        var response = await _client!.GetAsync($"/smtp/api/messages/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMessage_RemovesMessage()
    {
        var msg = new StoredMessage
        {
            From = "test@test.com",
            To = ["r@test.com"],
            Subject = "Test"
        };
        _store!.Add(msg);

        var response = await _client!.DeleteAsync($"/smtp/api/messages/{msg.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_store.GetAll());
    }

    [Fact]
    public async Task DeleteAllMessages_ClearsStore()
    {
        _store!.Add(new StoredMessage { From = "a@t.com", To = ["b@t.com"], Subject = "1" });
        _store!.Add(new StoredMessage { From = "c@t.com", To = ["d@t.com"], Subject = "2" });

        var response = await _client!.DeleteAsync("/smtp/api/messages");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_store.GetAll());
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~DashboardApiMiddlewareTests" -v minimal
```
Expected: FAIL.

**Step 3: Implement the API middleware**

```csharp
// src/SmtpServer.Dashboard/Middleware/DashboardApiMiddleware.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Middleware;

public static class DashboardApiMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IApplicationBuilder UseDashboardApi(this IApplicationBuilder app, string pathPrefix)
    {
        app.UseEndpoints(endpoints =>
        {
            var api = $"{pathPrefix}/api";

            endpoints.MapGet($"{api}/messages", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var page = int.TryParse(context.Request.Query["page"], out var p) ? p : 0;
                var size = int.TryParse(context.Request.Query["size"], out var s) ? s : 50;
                var search = context.Request.Query["search"].FirstOrDefault();

                var result = store.GetPage(page, size, search);
                var summaries = new
                {
                    items = result.Items.Select(m => m.ToSummary()),
                    result.TotalCount
                };

                await context.Response.WriteAsJsonAsync(summaries, JsonOptions);
            });

            endpoints.MapGet($"{api}/messages/{{id:guid}}", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var id = Guid.Parse((string)context.GetRouteValue("id")!);
                var msg = store.GetById(id);

                if (msg is null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await context.Response.WriteAsJsonAsync(msg, JsonOptions);
            });

            endpoints.MapGet($"{api}/messages/{{id:guid}}/html", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var id = Guid.Parse((string)context.GetRouteValue("id")!);
                var msg = store.GetById(id);

                if (msg is null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(msg.HtmlBody ?? "<em>No HTML body</em>");
            });

            endpoints.MapGet($"{api}/messages/{{id:guid}}/attachments/{{index:int}}", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var id = Guid.Parse((string)context.GetRouteValue("id")!);
                var index = int.Parse((string)context.GetRouteValue("index")!);
                var msg = store.GetById(id);

                if (msg is null || index < 0 || index >= msg.Attachments.Count)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                var attachment = msg.Attachments[index];
                context.Response.ContentType = attachment.ContentType;
                context.Response.Headers.ContentDisposition = $"attachment; filename=\"{attachment.FileName}\"";
                await context.Response.Body.WriteAsync(attachment.Content);
            });

            endpoints.MapDelete($"{api}/messages/{{id:guid}}", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var notifier = context.RequestServices.GetService<SmtpDashboardHubNotifier>();
                var id = Guid.Parse((string)context.GetRouteValue("id")!);

                if (!store.Delete(id))
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                if (notifier is not null)
                    await notifier.NotifyMessageDeleted(id);

                context.Response.StatusCode = 200;
            });

            endpoints.MapDelete($"{api}/messages", async context =>
            {
                var store = context.RequestServices.GetRequiredService<IMessageStore>();
                var notifier = context.RequestServices.GetService<SmtpDashboardHubNotifier>();
                store.Clear();

                if (notifier is not null)
                    await notifier.NotifyMessagesCleared();

                context.Response.StatusCode = 200;
            });
        });

        return app;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~DashboardApiMiddlewareTests" -v minimal
```
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add REST API middleware for dashboard message operations"
```

---

### Task 7: Dashboard Static Assets (Embedded HTML/JS/CSS)

**Files:**
- Create: `src/SmtpServer.Dashboard/Assets/index.html`
- Create: `src/SmtpServer.Dashboard/Assets/css/dashboard.css`
- Create: `src/SmtpServer.Dashboard/Assets/js/dashboard.js`
- Create: `src/SmtpServer.Dashboard/Middleware/DashboardStaticFilesMiddleware.cs`

**Step 1: Create dashboard HTML**

```html
<!-- src/SmtpServer.Dashboard/Assets/index.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{TITLE}}</title>
    <link rel="stylesheet" href="{{PATH_PREFIX}}/assets/css/dashboard.css" />
</head>
<body>
    <header>
        <h1>{{TITLE}}</h1>
        <div class="header-actions">
            <input type="text" id="search" placeholder="Search messages..." />
            <button id="clearAll" class="btn btn-danger">Clear All</button>
        </div>
    </header>
    <main>
        <div id="inbox">
            <table id="messageTable">
                <thead>
                    <tr>
                        <th>From</th>
                        <th>To</th>
                        <th>Subject</th>
                        <th>Date</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody id="messageBody"></tbody>
            </table>
            <div id="pagination"></div>
            <div id="emptyState" class="empty-state">No messages received yet.</div>
        </div>
        <div id="messageDetail" class="hidden">
            <button id="backToInbox" class="btn">&larr; Back</button>
            <div id="detailHeader"></div>
            <div class="tabs">
                <button class="tab active" data-tab="html">HTML</button>
                <button class="tab" data-tab="text">Text</button>
                <button class="tab" data-tab="headers">Headers</button>
                <button class="tab" data-tab="attachments">Attachments</button>
            </div>
            <div id="tabContent">
                <div id="tab-html" class="tab-panel active">
                    <iframe id="htmlPreview" sandbox="allow-same-origin"></iframe>
                </div>
                <div id="tab-text" class="tab-panel"><pre id="textPreview"></pre></div>
                <div id="tab-headers" class="tab-panel"><table id="headersTable"></table></div>
                <div id="tab-attachments" class="tab-panel"><ul id="attachmentsList"></ul></div>
            </div>
        </div>
    </main>
    <script src="{{PATH_PREFIX}}/assets/js/signalr.min.js"></script>
    <script src="{{PATH_PREFIX}}/assets/js/dashboard.js"></script>
    <script>Dashboard.init("{{PATH_PREFIX}}");</script>
</body>
</html>
```

**Step 2: Create CSS**

Create `src/SmtpServer.Dashboard/Assets/css/dashboard.css` — a clean, minimal design with dark/light theme support using `prefers-color-scheme`. Style the header, table, detail view, tabs, pagination, buttons, and empty state. Keep it under 300 lines.

**Step 3: Create dashboard JS**

Create `src/SmtpServer.Dashboard/Assets/js/dashboard.js` — the `Dashboard` module that:
- Connects to SignalR hub at `{prefix}/hub`
- Handles `NewMessage`, `MessageDeleted`, `MessagesCleared` events
- Fetches messages from `{prefix}/api/messages` with pagination and search
- Shows message detail view with tabs (HTML iframe, text, headers, attachments)
- Handles delete single / clear all with confirmation
- Auto-refreshes inbox when new messages arrive via SignalR

**Step 4: Download SignalR JS client**

Download `@microsoft/signalr` browser JS bundle and save to `src/SmtpServer.Dashboard/Assets/js/signalr.min.js` as an embedded resource.

```bash
curl -o src/SmtpServer.Dashboard/Assets/js/signalr.min.js \
  https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js
```

**Step 5: Implement static files middleware**

```csharp
// src/SmtpServer.Dashboard/Middleware/DashboardStaticFilesMiddleware.cs
using System.Reflection;
using Microsoft.AspNetCore.Http;
using SmtpServer.Dashboard.Configuration;

namespace SmtpServer.Dashboard.Middleware;

public class DashboardStaticFilesMiddleware(
    RequestDelegate next,
    FakeDashboardOptions options)
{
    private static readonly Assembly Assembly = typeof(DashboardStaticFilesMiddleware).Assembly;
    private static readonly string Prefix = "SmtpServer.Dashboard.Assets.";

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!path.StartsWith(options.PathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var relativePath = path[options.PathPrefix.Length..].TrimStart('/');

        // Serve index.html for the root and message detail routes
        if (string.IsNullOrEmpty(relativePath) || relativePath.StartsWith("message/"))
        {
            await ServeIndex(context);
            return;
        }

        // Serve static assets
        if (relativePath.StartsWith("assets/"))
        {
            var resourcePath = relativePath["assets/".Length..].Replace('/', '.');
            await ServeEmbeddedResource(context, resourcePath);
            return;
        }

        await next(context);
    }

    private async Task ServeIndex(HttpContext context)
    {
        using var stream = Assembly.GetManifestResourceStream($"{Prefix}index.html");
        if (stream is null) { context.Response.StatusCode = 404; return; }

        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        html = html.Replace("{{TITLE}}", options.Title)
                   .Replace("{{PATH_PREFIX}}", options.PathPrefix.TrimEnd('/'));

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html);
    }

    private async Task ServeEmbeddedResource(HttpContext context, string resourcePath)
    {
        var fullPath = $"{Prefix}{resourcePath}";
        using var stream = Assembly.GetManifestResourceStream(fullPath);
        if (stream is null) { context.Response.StatusCode = 404; return; }

        var ext = Path.GetExtension(resourcePath);
        context.Response.ContentType = ContentTypes.GetValueOrDefault(ext, "application/octet-stream");
        await stream.CopyToAsync(context.Response.Body);
    }
}
```

**Step 6: Build to verify assets are embedded**

```bash
dotnet build src/SmtpServer.Dashboard
```
Expected: Build succeeded.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add embedded dashboard UI with static files middleware"
```

---

### Task 8: Service Registration Extensions

**Files:**
- Create: `src/SmtpServer.Dashboard/Extensions/ServiceCollectionExtensions.cs`
- Create: `src/SmtpServer.Dashboard/Extensions/ApplicationBuilderExtensions.cs`
- Test: `tests/SmtpServer.Dashboard.Tests/Extensions/ServiceRegistrationTests.cs`

**Step 1: Write tests**

```csharp
// tests/SmtpServer.Dashboard.Tests/Extensions/ServiceRegistrationTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Extensions;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Extensions;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddFakeSmtp_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();

        services.AddFakeSmtp(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();

        var store = provider.GetService<IMessageStore>();
        Assert.NotNull(store);

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s.GetType().Name == "FakeSmtpHostedService");

        var notifier = provider.GetService<SmtpDashboardHubNotifier>();
        Assert.NotNull(notifier);
    }

    [Fact]
    public void AddFakeSmtp_ConfiguresOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();

        services.AddFakeSmtp(opts =>
        {
            opts.Port = 3000;
            opts.MaxMessages = 500;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FakeSmtpOptions>>();

        Assert.Equal(3000, options.Value.Port);
        Assert.Equal(500, options.Value.MaxMessages);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~ServiceRegistrationTests" -v minimal
```
Expected: FAIL.

**Step 3: Implement service registration extensions**

```csharp
// src/SmtpServer.Dashboard/Extensions/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Smtp;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFakeSmtp(
        this IServiceCollection services,
        Action<FakeSmtpOptions>? configureOptions = null)
    {
        var options = new FakeSmtpOptions();
        configureOptions?.Invoke(options);

        services.Configure<FakeSmtpOptions>(opts =>
        {
            opts.Port = options.Port;
            opts.Hostname = options.Hostname;
            opts.MaxMessages = options.MaxMessages;
            opts.MaxMessageSize = options.MaxMessageSize;
        });

        services.AddSingleton<IMessageStore>(new InMemoryMessageStore(options.MaxMessages));
        services.AddSingleton<SmtpDashboardHubNotifier>();
        services.AddHostedService<FakeSmtpHostedService>();
        services.AddSignalR();

        return services;
    }
}
```

```csharp
// src/SmtpServer.Dashboard/Extensions/ApplicationBuilderExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SmtpServer.Dashboard.Authorization;
using SmtpServer.Dashboard.Configuration;
using SmtpServer.Dashboard.Hubs;
using SmtpServer.Dashboard.Middleware;

namespace SmtpServer.Dashboard.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseFakeSmtpDashboard(
        this IApplicationBuilder app,
        Action<FakeDashboardOptions>? configureOptions = null)
    {
        var options = new FakeDashboardOptions();
        configureOptions?.Invoke(options);

        // Ensure path prefix doesn't have trailing slash
        options.PathPrefix = options.PathPrefix.TrimEnd('/');

        // Authorization middleware
        if (options.Authorization.Length > 0)
        {
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? "";
                if (path.StartsWith(options.PathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var dashboardContext = new DashboardContext(context);
                    if (!options.Authorization.All(f => f.Authorize(dashboardContext)))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }
                }

                await next();
            });
        }

        // Static files middleware (dashboard UI)
        app.UseMiddleware<DashboardStaticFilesMiddleware>(options);

        // API endpoints
        app.UseRouting();
        app.UseDashboardApi(options.PathPrefix);

        // SignalR hub
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<SmtpDashboardHub>($"{options.PathPrefix}/hub");
        });

        // Start the SignalR notifier
        var notifier = app.ApplicationServices.GetRequiredService<SmtpDashboardHubNotifier>();
        notifier.Start();

        return app;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~ServiceRegistrationTests" -v minimal
```
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add Hangfire-style service registration extensions"
```

---

### Task 9: Sample Application

**Files:**
- Modify: `samples/SampleApp/Program.cs`

**Step 1: Write sample Program.cs**

```csharp
// samples/SampleApp/Program.cs
using SmtpServer.Dashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFakeSmtp(options =>
{
    options.Port = 2525;
    options.MaxMessages = 500;
});

var app = builder.Build();

app.UseFakeSmtpDashboard(options =>
{
    options.PathPrefix = "/smtp";
    options.Title = "Dev Mail Dashboard";
});

app.MapGet("/", () => "Fake SMTP running. Dashboard at /smtp");

app.Run();
```

**Step 2: Build and run to verify**

```bash
dotnet build samples/SampleApp
dotnet run --project samples/SampleApp &
```

Expected: App starts, SMTP server listens on 2525, dashboard at `http://localhost:5000/smtp`.

**Step 3: Test by sending a test email**

```bash
# Quick send via PowerShell or swaks if available
dotnet run --project samples/SampleApp &
# Visit http://localhost:5000/smtp in browser
```

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add sample application demonstrating usage"
```

---

### Task 10: Integration Test — End-to-End

**Files:**
- Create: `tests/SmtpServer.Dashboard.Tests/Integration/EndToEndTests.cs`

**Step 1: Write E2E test**

```csharp
// tests/SmtpServer.Dashboard.Tests/Integration/EndToEndTests.cs
using System.Net;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmtpServer.Dashboard.Extensions;
using SmtpServer.Dashboard.Storage;

namespace SmtpServer.Dashboard.Tests.Integration;

public class EndToEndTests : IAsyncLifetime
{
    private IHost? _host;
    private HttpClient? _client;
    private const int SmtpPort = 12525; // Use high port for tests

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddFakeSmtp(opts => opts.Port = SmtpPort);
                });
                webBuilder.Configure(app =>
                {
                    app.UseFakeSmtpDashboard();
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();

        // Give SMTP server time to start
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.StopAsync();
        _host?.Dispose();
    }

    [Fact]
    public async Task SendEmail_AppearsInApi()
    {
        // Send via MailKit SMTP client
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Test", "test@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
        message.Subject = "E2E Test";
        message.Body = new TextPart("plain") { Text = "Hello from E2E test" };

        using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
        await smtpClient.ConnectAsync("localhost", SmtpPort, false);
        await smtpClient.SendAsync(message);
        await smtpClient.DisconnectAsync(true);

        // Verify via API
        var store = _host!.Services.GetRequiredService<IMessageStore>();
        var messages = store.GetAll();

        Assert.Single(messages);
        Assert.Equal("test@example.com", messages[0].From);
        Assert.Equal("E2E Test", messages[0].Subject);
    }
}
```

**Step 2: Add MailKit to test project**

```bash
dotnet add tests/SmtpServer.Dashboard.Tests package MailKit
```

**Step 3: Run tests**

```bash
dotnet test tests/SmtpServer.Dashboard.Tests --filter "FullyQualifiedName~EndToEndTests" -v minimal
```
Expected: PASS.

**Step 4: Run full test suite**

```bash
dotnet test -v minimal
```
Expected: All tests PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "test: add end-to-end integration test with MailKit SMTP client"
```

---

### Task 11: Polish & Final Build Verification

**Step 1: Run full build**

```bash
dotnet build -c Release
```
Expected: Build succeeded for both net8.0 and net9.0.

**Step 2: Run all tests**

```bash
dotnet test -c Release -v minimal
```
Expected: All tests PASS.

**Step 3: Verify NuGet package creation**

```bash
dotnet pack src/SmtpServer.Dashboard -c Release
```
Expected: `.nupkg` file created.

**Step 4: Final commit**

```bash
git add -A
git commit -m "chore: final polish and release build verification"
```
