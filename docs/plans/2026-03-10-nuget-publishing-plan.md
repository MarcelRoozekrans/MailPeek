# NuGet Publishing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Automate NuGet package publishing with GitVersion + Conventional Commits + GitHub Actions.

**Architecture:** GitVersion in ContinuousDeployment mode reads commit history to calculate SemVer. CI workflow publishes pre-release packages on master push. Release workflow publishes stable packages when a GitHub Release is created.

**Tech Stack:** GitVersion, GitHub Actions, dotnet CLI, NuGet.org

---

### Task 1: Add LICENSE file

**Files:**
- Create: `LICENSE`

**Step 1: Create MIT license file**

```text
MIT License

Copyright (c) 2026 Marcel Roozekrans

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**Step 2: Commit**

```bash
git add LICENSE
git commit -m "chore: add MIT license file"
```

---

### Task 2: Generate package icon

**Files:**
- Create: `src/MailPeek/icon.png`

**Step 1: Generate a 128x128 PNG from the 📧 emoji**

Use a script or tool to render the emoji to a PNG. For example, using PowerShell with System.Drawing or an online tool. The icon should be a 128x128 PNG with the 📧 emoji centered on a transparent background.

**Step 2: Commit**

```bash
git add src/MailPeek/icon.png
git commit -m "chore: add package icon from envelope emoji"
```

---

### Task 3: Add NuGet package metadata to csproj

**Files:**
- Modify: `src/MailPeek/MailPeek.csproj`

**Step 1: Add package metadata PropertyGroup**

Add after the existing `<PropertyGroup>` (after line 8):

```xml
  <PropertyGroup>
    <PackageId>MailPeek</PackageId>
    <Authors>Marcel Roozekrans</Authors>
    <Description>In-memory fake SMTP server with a real-time web dashboard for ASP.NET Core. Capture emails during development and testing — no external mail server needed.</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/MarcelRoozekrans/MailPeek</PackageProjectUrl>
    <RepositoryUrl>https://github.com/MarcelRoozekrans/MailPeek.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>smtp;email;testing;development;fake-smtp;mailpeek;aspnetcore</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
  </PropertyGroup>
```

**Step 2: Add items to include README and icon in the package**

Add before the closing `</Project>`:

```xml
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="icon.png" Pack="true" PackagePath="\" />
  </ItemGroup>
```

**Step 3: Verify it builds**

Run: `dotnet build src/MailPeek/MailPeek.csproj`
Expected: Build succeeds with no errors.

**Step 4: Verify pack works**

Run: `dotnet pack src/MailPeek/MailPeek.csproj --no-build -o ./artifacts`
Expected: Creates a `.nupkg` file in `./artifacts/`.

**Step 5: Commit**

```bash
git add src/MailPeek/MailPeek.csproj
git commit -m "chore: add NuGet package metadata to MailPeek.csproj"
```

---

### Task 4: Install GitVersion as a dotnet tool

**Files:**
- Create: `.config/dotnet-tools.json`

**Step 1: Create the dotnet tools manifest**

Run: `dotnet new tool-manifest`
Expected: Creates `.config/dotnet-tools.json`.

**Step 2: Install GitVersion**

Run: `dotnet tool install GitVersion.Tool`
Expected: Adds GitVersion.Tool to `.config/dotnet-tools.json`.

**Step 3: Verify it works**

Run: `dotnet tool restore && dotnet gitversion /showvariable FullSemVer`
Expected: Outputs a version string like `0.1.0-alpha.1`.

**Step 4: Commit**

```bash
git add .config/dotnet-tools.json
git commit -m "chore: add GitVersion as dotnet local tool"
```

---

### Task 5: Add GitVersion configuration

**Files:**
- Create: `GitVersion.yml`

**Step 1: Create GitVersion.yml at repo root**

```yaml
mode: ContinuousDeployment
tag-prefix: v
major-version-bump-message: "^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\\(.*\\))?!:"
minor-version-bump-message: "^feat(\\(.*\\))?:"
patch-version-bump-message: "^fix(\\(.*\\))?:"
branches:
  master:
    regex: ^master$
    tag: alpha
  release:
    regex: ^release/.*$
    tag: rc
```

**Step 2: Verify GitVersion reads the config**

Run: `dotnet gitversion /showvariable FullSemVer`
Expected: Outputs a version like `0.1.0-alpha.X` (where X is the commit count).

**Step 3: Commit**

```bash
git add GitVersion.yml
git commit -m "chore: add GitVersion config for Conventional Commits"
```

---

### Task 6: Create CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

**Step 1: Create the CI workflow file**

```yaml
name: CI

on:
  push:
    branches: [master]
  pull_request:
    branches: [master]

jobs:
  build:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Run GitVersion
        id: gitversion
        run: |
          VERSION=$(dotnet gitversion /showvariable NuGetVersionV2)
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"
          echo "Package version: $VERSION"

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release -p:Version=${{ steps.gitversion.outputs.version }}

      - name: Test
        run: dotnet test --no-build -c Release --verbosity normal

      - name: Pack
        run: dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.gitversion.outputs.version }} -o ./artifacts

      - name: Push to NuGet (pre-release)
        if: github.event_name == 'push' && github.ref == 'refs/heads/master'
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

**Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add CI workflow with pre-release NuGet publishing"
```

---

### Task 7: Create Release workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Step 1: Create the release workflow file**

```yaml
name: Release

on:
  release:
    types: [published]

jobs:
  publish:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Extract version from tag
        id: version
        run: |
          TAG="${{ github.event.release.tag_name }}"
          VERSION="${TAG#v}"
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"
          echo "Release version: $VERSION"

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release -p:Version=${{ steps.version.outputs.version }}

      - name: Test
        run: dotnet test --no-build -c Release --verbosity normal

      - name: Pack
        run: dotnet pack src/MailPeek/MailPeek.csproj --no-build -c Release -p:PackageVersion=${{ steps.version.outputs.version }} -o ./artifacts

      - name: Push to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

**Step 2: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci: add release workflow for stable NuGet publishing"
```

---

### Task 8: Verify end-to-end locally

**Step 1: Run full build**

Run: `dotnet build -c Release`
Expected: Builds successfully for both net8.0 and net9.0.

**Step 2: Run tests**

Run: `dotnet test -c Release`
Expected: All tests pass.

**Step 3: Run pack with GitVersion**

Run: `dotnet pack src/MailPeek/MailPeek.csproj -c Release -p:PackageVersion=$(dotnet gitversion /showvariable NuGetVersionV2) -o ./artifacts`
Expected: Creates `MailPeek.<version>.nupkg` in `./artifacts/`.

**Step 4: Inspect the package**

Run: `dotnet nuget locals all --list` (just to confirm tooling works), then inspect the .nupkg (it's a zip — check it contains README.md, icon.png, and the lib/ folder with both net8.0 and net9.0 assemblies).

**Step 5: Clean up artifacts**

Run: `rm -rf ./artifacts`

**Step 6: Final commit (if any cleanup needed)**

```bash
git commit -m "chore: finalize NuGet publishing setup"
```

---

## Post-Implementation: GitHub Setup Required

After pushing to GitHub, the repo owner must:
1. Go to GitHub repo → Settings → Secrets and variables → Actions
2. Add secret `NUGET_API_KEY` with a NuGet.org API key scoped to push packages
3. Create a GitHub Release with tag `v0.1.0` to publish the first stable package
