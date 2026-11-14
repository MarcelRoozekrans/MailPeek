# AGENTS.md: MailPeek

MailPeek is an in-memory fake SMTP server with a real-time web dashboard for ASP.NET Core apps,
used during local development and in tests to capture outgoing mail. Two NuGet packages ship:
`MailPeek` (SMTP server, REST API, SignalR hub and the embedded dashboard) and
`MailPeek.Hosting.Aspire` (an Aspire AppHost resource that exposes an `smtp://` connection string).

## Layout

- Shipped: `src/MailPeek` (net8.0, net9.0, net11.0) and `src/MailPeek.Hosting.Aspire`
  (net9.0, net11.0). The Aspire package has no project reference to `MailPeek`; the two only meet
  through the connection string named after the resource.
- Not shipped: `samples/SampleApp` (net11.0, only a manual demo) and `tests/MailPeek.Tests`
  (net9.0, net11.0). Coverage excludes both, see `coverage.runsettings` and `codecov.yml`.
- `src/MailPeek/Assets/` is the whole dashboard: plain HTML, CSS and JS, embedded into the DLL
  with `<EmbeddedResource Include="Assets\**\*" />`. There is no npm or frontend build step.
  `Assets/js/signalr.min.js` is a vendored, minified third-party file: do not edit or reformat it.
- `README.md` is packed into both packages as their NuGet readme, so edits show on nuget.org.
  It points at `docs/regression-screenshots/`, which therefore must stay in place.
- `docs/plans/` and `docs/regression-report-*.md` are historical design and test notes, not
  current specs.

## Build and test

Prerequisites: the .NET 11 SDK, which builds every target including `net11.0`, plus the 8.0 and
9.0 runtimes; CI installs `8.0.x`, `9.0.x` and `11.0.x`. There is no `global.json`. `dotnet tool restore` installs GitVersion, which CI uses
for the build and package version.

CI, `.github/workflows/ci.yml`:

```bash
dotnet tool restore
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --verbosity normal --collect "XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults
dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -o ./artifacts
dotnet pack src/MailPeek.Hosting.Aspire/MailPeek.Hosting.Aspire.csproj --no-build -c Release -o ./artifacts
```

CI also passes `-p:Version=` and `-p:PackageVersion=` from GitVersion. Keep `--collect` and
`--settings` together: the comment in `ci.yml` explains why coverage reads 0 at random without
`--collect`.

Fast inner loop, one target framework only:

```bash
dotnet test tests/MailPeek.Tests/MailPeek.Tests.csproj -f net11.0
```

## Conventions and gotchas

- Package references are pinned per `.csproj`; there is no `Directory.Packages.props`. The six
  analyzer packages are listed in all three non-sample projects: bump them in every project at
  once. `Directory.Build.props` only turns on NuGetAudit.
- Analyzer suppressions are narrow and per project: `EPS06` in `MailPeek.csproj`, `MA0004` in the
  test project. Do not add a blanket `NoWarn`; fix the finding instead.
- `MailPeek` targets net8.0, so library code must compile against the net8.0 API surface. Guard
  newer APIs with `#if` or avoid them.
- `AddMailPeek(Action<MailPeekSmtpOptions>)` copies each option into `services.Configure` by hand.
  A new property on `MailPeekSmtpOptions` must be added to that copy, or it is silently ignored.
- The analysis services (`AutoTagger`, `LinkChecker`, `HtmlCompatibilityChecker`, `SpamScorer`,
  `WebhookNotifier`, `MailPeekHubNotifier`) are singletons that subscribe to
  `IMessageStore.OnMessageReceived` in `Start()`. `UseMailPeek` calls each `Start()`. A new service
  needs a registration in both `AddMailPeek` overloads and a `Start()` call in `UseMailPeek`.
- Embedded asset lookup maps the URL `{prefix}/assets/js/x.js` to the resource name
  `MailPeek.Assets.js.x.js` by replacing `/` with `.`. Only `.html`, `.css` and `.js` get a real
  content type; any other new asset type needs an entry in `DashboardStaticFilesMiddleware`.
- `index.html` holds `{{TITLE}}` and `{{PATH_PREFIX}}` placeholders that the middleware fills in.
  All asset and API URLs in the dashboard must be built from the path prefix, never hard-coded to
  `/mailpeek`.
- The REST API lives in `Middleware/DashboardApiExtensions.cs`. A new or changed endpoint also
  changes the API table in `README.md` and the calls in `Assets/js/dashboard.js`.
- Integration tests start a real SMTP listener. Each test host picks a free port with
  `GetFreePort()`, because the net9.0 and net11.0 test hosts run in parallel; never use a fixed
  port in a test.
- The release version comes from release-please (`release-type: simple`) and `CHANGELOG.md` is
  written by it; there is no `<Version>` in the project files. GitVersion only sets the version of
  CI builds.

<!-- org-core:start sha256=3022fd292033117a25777334465cf8843056c471038e693d18c2272e0bc8044a (generated from docker-nuc scripts/agents-md/org-core-personal.md; do not edit here) -->
## Org rules

These rules hold in every public MarcelRoozekrans repo.

### Commits and releases

- Conventional Commits. release-please reads them on the default branch: `feat:` and `fix:` cut a
  release, `docs:`, `chore:`, `ci:`, `test:` and `refactor:` do not. A breaking change needs `!` or
  a `BREAKING CHANGE:` footer.
- A commit body never contains nested parentheses. release-please cannot parse them and silently
  drops the whole commit from the changelog, while the workflow still reports success.
- After merging a `feat:` or `fix:`, check that the commit appears in the release-please PR.

### Branches and pull requests

- Every change goes through a branch and a pull request. Never push to the default branch directly.
- A PR body says what the diff does, and nothing the diff does not do.
- Fix review findings in the PR under review, including small ones. File an issue only for a finding
  that belongs to other code, needs a design decision, is its own piece of work, or waits on
  something external.

### CI

- The .NET repos call the shared workflows of `MarcelRoozekrans/ci-baseline`, pinned to `@v1`, never
  `@main`.
- Its coverage gate fails the build when line coverage drops below the floor of 80%, and Codecov
  fails a PR that lowers project coverage by more than 1 percentage point. New code comes with
  tests.
- A repo with a lower floor documents the reason and links the issue that raises it.
<!-- org-core:end -->
