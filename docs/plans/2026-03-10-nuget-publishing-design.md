# NuGet Publishing Design

## Overview

Set up automated NuGet publishing for the MailPeek package using GitVersion with Conventional Commits for semantic versioning, and GitHub Actions for CI/CD.

## Decisions

- **Versioning:** GitVersion with Conventional Commits (no hardcoded version in .csproj)
- **Pre-release:** Pushes to `master` publish pre-release packages (e.g., `1.2.3-alpha.4`)
- **Stable release:** GitHub Release creation publishes stable packages (e.g., `1.2.3`)
- **GitVersion mode:** ContinuousDeployment with `alpha` tag on master, `rc` on release branches
- **License:** MIT
- **Package icon:** 128x128 PNG rendered from 📧 emoji

## Components

### 1. Package Metadata (`src/MailPeek/MailPeek.csproj`)

Add NuGet properties — `PackageId`, `Authors`, `Description`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `PackageTags`, `PackageReadmeFile`, `PackageIcon`. Version is injected by GitVersion at build time.

### 2. GitVersion Configuration (`GitVersion.yml`)

- Mode: ContinuousDeployment
- Tag prefix: `v`
- Conventional Commits message patterns for major/minor/patch bumps
- `master` branch tagged `alpha`, `release/*` branches tagged `rc`

### 3. Dotnet Tool (`.config/dotnet-tools.json`)

Register GitVersion as a local dotnet tool for local version preview via `dotnet gitversion`.

### 4. CI Workflow (`.github/workflows/ci.yml`)

- Triggers: push to `master`, pull requests
- Steps: checkout (full history) → setup .NET 8 & 9 → restore tools → GitVersion → build → test → pack → push pre-release to NuGet (master only)
- NuGet API key from GitHub secret `NUGET_API_KEY`

### 5. Release Workflow (`.github/workflows/release.yml`)

- Trigger: GitHub Release published
- Steps: checkout (full history) → setup .NET 8 & 9 → restore tools → GitVersion → build → test → pack → push stable to NuGet

### 6. LICENSE File

MIT license at repo root.

### 7. Package Icon

128x128 PNG from 📧 emoji at `src/MailPeek/icon.png`, included in package.
