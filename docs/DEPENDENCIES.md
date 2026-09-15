# Dependencies and licence policy

Every third-party package in this repository is listed here with its licence, why it is present, and what
replaces it if the licence changes. This is a deliverable, not housekeeping: for federal and regulated
clients it is part of what gets reviewed, and the .NET ecosystem has relicensed enough load-bearing
packages since 2025 that "we'll deal with it later" has a price.

Two rules make the rest work:

1. **A package enters this file before it enters `Directory.Packages.props`.** No exceptions, including
   transitive dependencies pulled in deliberately.
2. **The policy is enforced by the build, not by memory.** See [Enforcement](#enforcement).

---

## 1. Licence classes

| Class | Licences | Policy |
|---|---|---|
| **A — Permissive** | MIT, Apache-2.0, BSD-2/3-Clause, MS-PL, ISC, 0BSD | Free to use. Default. |
| **B — Weak copyleft** | MPL-2.0, LGPL-2.1/3.0, EPL-2.0 | Review before use. Acceptable when linked unmodified and not statically linked into a shipped binary. Record the reasoning here. |
| **C — Reciprocal / strong copyleft** | RPL-1.5, AGPL-3.0, GPL-2.0/3.0, SSPL | **Prohibited** in anything the organisation deploys or delivers. See §2. |
| **D — Commercial** | Any paid licence | Procurement sign-off **before** the first `PackageReference`, not after the pilot. |
| **E — Source-available** | BSL, Massient MassTransit v9, Elastic v2 | Treated as class D. Source visibility is not a licence grant. |

### Why C is prohibited rather than reviewed

RPL-1.5 is the class that traps teams, because the obvious mental model is wrong. Under RPL-1.5 the
obligation attaches when you **Deploy in any form — internally or to an outside party**, and it extends to
*all components you author*, whether compiled into one binary or split across client and server. Internal
deployment of a line-of-business application triggers full source disclosure of that application.

This is strictly broader than AGPL and much broader than GPL. It is the licence under which the free
editions of MediatR 13+ and AutoMapper 15+ are published, and it is why "we'll take the open-source path"
is not an available answer for those two.

---

## 2. Denylist

These are blocked at build time (§5.2). They are blocked because of their licence, not their quality —
all four are good libraries.

| Package | Blocked from | Why | Last permissive version |
|---|---|---|---|
| `MediatR` | 13.0.0 | Dual RPL-1.5 / commercial (Lucky Penny Software). RPL path prohibited under §1; Community licence unavailable (§3). | 12.5.0, Apache-2.0, indefinitely |
| `AutoMapper` | 15.0.0 | Same model, same reasoning. | 14.0.0 |
| `MassTransit` | v9 | Commercial under Massient; source-available, not open source. v8 (Apache-2.0) loses support at end of 2026. | v8 — **expiring, see §6** |
| `FluentAssertions` | 8.0.0 | Xceed Community Licence covers *Non-Commercial Use* only; any other use requires a paid licence. | 7.x, Apache-2.0, still receiving bugfixes |

### Two misreadings to head off

**"We're under the revenue threshold."** The Lucky Penny Community licence requires under USD 5,000,000
gross annual revenue. A consultancy of any size is far above it. For client work the test is applied to the client as well — the
Community licence covers client work only if the client itself would qualify, which a federal authority or
a large enterprise does not.

**"It's internal, so it's non-commercial."** It is not. The Xceed Community Licence is scoped to
Non-Commercial Use. An internal line-of-business application at a commercial software company is commercial
use, and needs paid licences at the same per-developer rate as a client project.

### Neither product fails closed

Lucky Penny requires a licence key for auditing, but usage is not restricted by a missing or invalid key.
MassTransit v9 validates its key locally, logs a warning when the subscription has expired, and does not
disable running services.

A team can therefore be out of compliance for a year with nothing breaking and nothing in CI going red.
That is the whole argument for §5.

---

## 3. Dependency register

Class A unless noted. Verified September 2026; re-verify at each review (§6).

### Runtime

| Package | Purpose | Licence | Risk | Replacement if it changes |
|---|---|---|---|---|
| `Microsoft.*` / `System.*` (BCL, ASP.NET Core, EF Core) | Platform | MIT | None | — |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI document generation | MIT | None | NSwag (MIT) |
| `Scalar.AspNetCore` | OpenAPI UI | MIT | Low | Swashbuckle UI, or none — the document is the contract |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Database provider | PostgreSQL (BSD-like) | None | — |
| `EFCore.NamingConventions` | snake_case physical names | Apache-2.0 | Low | Explicit `HasColumnName`, or accept EF defaults |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Bearer-token authentication (production mode, ADR-012) | MIT | None | — |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | Compile-time API bans | MIT | None | — |

**On messaging.** The outbox/inbox is hand-rolled (ADR-008, ~250 lines in `App.Infrastructure.Messaging`), so
there is no messaging dependency at all. Should messages ever need to leave the process, **Wolverine** (MIT;
JasperFx monetises support and "Critter Stack Pro" add-ons *alongside* the MIT core, i.e. open-core, not a
relicence) is the documented candidate; the integration points (`IEventHandler<T>`, `IUnitOfWork`) are
bus-agnostic for that reason.

### Build and test

| Package | Purpose | Licence | Notes |
|---|---|---|---|
| `xunit.v3` / `xunit.runner.visualstudio` / `Microsoft.NET.Test.Sdk` | Test framework and VSTest adapter | Apache-2.0 / MIT | |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory` | MIT | |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider` for deterministic domain tests | MIT | |
| `Microsoft.CodeAnalysis.CSharp` / `Microsoft.CodeAnalysis.Analyzers` | Build the in-repo Roslyn analyzer (`App.Analyzers`), compile-time only | MIT | |
| `Testcontainers.PostgreSql` | Integration test database | MIT | |
| `ArchUnitNET` | Architecture tests | Apache-2.0 | |
| `nuget-license` (dotnet tool) | Licence gate in CI | Apache-2.0 | §5.1 |

### Explicitly not present, by design

| Gap | Why nothing fills it |
|---|---|
| Mediator / in-process dispatch | The design has no mediator role. An endpoint calls a repository and an aggregate method. If one is ever needed: Wolverine (MIT) or `martinothamar/Mediator` (MIT, source-generated, AOT-clean, largely MediatR-compatible). |
| Object mapper | Response records are built with `Select(...)`, which must be an EF-translatable expression anyway. If one is ever needed: Mapperly (Apache-2.0, compile-time, reviewable output) ahead of Mapster (MIT). |
| Assertion DSL | Plain xUnit `Assert` is enough (ADR-014). FluentAssertions 8+ is prohibited (§2); Shouldly (BSD-3) would be permissible but is unnecessary. |
| Snapshot/approval library | One golden-master test (HAL links) uses a 30-line file-compare helper. Verify (MIT) if approval tests ever multiply. |
| Value-object generator | `readonly record struct` + two small contracts + analyzer DDD0002 (ADR-004). Vogen (Apache-2.0) if per-type boilerplate ever becomes a burden. |
| Messaging framework | Hand-rolled outbox (ADR-008). Wolverine (MIT) when messages must leave the process. |
| Migration runner | 100-line `SqlMigrator` (ADR-007). DbUp (MIT) if the runner needs more than sequential scripts with checksums. |

---

## 4. Adding a dependency

1. Determine the licence class (§1). Class C stops here.
2. Add a row to §3 with purpose, licence, risk and a **named** replacement. "We'd find something" is not a
   replacement.
3. Add the version to `Directory.Packages.props` (central package management; no versions in `.csproj`).
4. Run `dotnet restore` and confirm the CI licence gate passes, including transitively.

---

## 5. Enforcement

### 5.1 Licence gate in CI

`nuget-license` reads `project.assets.json`, so a restore must run first. `-t` is what catches the
transitive case, which is where this actually bites.

`build/allowed-licenses.json`:

```json
[
  "MIT",
  "Apache-2.0",
  "BSD-2-Clause",
  "BSD-3-Clause",
  "MS-PL",
  "ISC",
  "PostgreSQL"
]
```

```yaml
- name: Restore
  run: dotnet restore -c Release

- name: Licence gate
  run: |
    dotnet tool install --global nuget-license
    nuget-license \
      --input dotnet-ddd-starter.slnx \
      --include-transitive \
      --allowed-license-types build/allowed-licenses.json \
      --output Markdown \
      --file-output artifacts/licences.md
```

Publish `artifacts/licences.md` as a build artifact. It is most of a licence SBOM section and costs nothing
to produce once the gate exists.

Packages whose NuGet metadata carries a licence *URL* rather than an SPDX expression need a mapping entry
(`--licenseurl-to-license-mappings build/license-url-mappings.json`). Keep that file small and reviewed — it is
the obvious place for the gate to be quietly defeated. Current entries, both transitive dependencies of the
.NET SDK/BCL packages and both reviewed:

| URL | Mapped to | Package(s) | Why acceptable |
|---|---|---|---|
| `https://github.com/dotnet/standard/blob/master/LICENSE.TXT` | `MIT` | `NETStandard.Library` | The linked file is the MIT licence. |
| `http://go.microsoft.com/fwlink/?LinkId=329770` | `MS-NET-Library` | legacy `Microsoft.*`/`System.*` packages | Microsoft .NET Library EULA: royalty-free redistribution of the binaries with your application; no source obligations. Listed as its own class-A entry in `allowed-licenses.json` so it stays visible. |

### 5.2 Denylist at compile time

The licence gate catches licence *metadata*. The denylist catches the four packages we have already decided
about, before anyone waits for CI. In `Directory.Build.targets`:

```xml
<Project>
  <PropertyGroup>
    <!-- Leading and trailing ';' so that Contains() matches whole ids only. -->
    <BannedPackageIds>;MediatR;MediatR.Extensions.Microsoft.DependencyInjection;AutoMapper;AutoMapper.Extensions.Microsoft.DependencyInjection;MassTransit;FluentAssertions;</BannedPackageIds>
  </PropertyGroup>

  <Target Name="BanRestrictivelyLicensedPackages" BeforeTargets="CollectPackageReferences">
    <ItemGroup>
      <_BannedInUse Include="@(PackageReference)"
                    Condition="$(BannedPackageIds.Contains(';%(PackageReference.Identity);'))" />
    </ItemGroup>
    <Error Condition="'@(_BannedInUse)' != ''"
           Code="D4S-0001"
           Text="Restrictively licensed package(s) referenced: @(_BannedInUse, ', '). See DEPENDENCIES.md §2." />
  </Target>
</Project>
```

Verified 2026-09-12: a `<PackageReference Include="MediatR" />` added to a test project fails `dotnet build` with
`error D4S-0001: Restrictively licensed package(s) referenced: MediatR` before restore evaluates the package graph.
The live target is in `Directory.Build.targets` and also hooks `_GenerateRestoreProjectSpec` and `CoreCompile`.

### 5.3 Vulnerability and integrity gates

In `Directory.Build.props`:

```xml
<PropertyGroup>
  <NuGetAudit>true</NuGetAudit>
  <NuGetAuditMode>all</NuGetAuditMode>
  <NuGetAuditLevel>low</NuGetAuditLevel>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

CI restores with `--locked-mode`. Package source mapping pins each package id to an expected feed.

### 5.4 Agent guardrail

Coding agents reach for MediatR, AutoMapper, MassTransit and FluentAssertions by reflex, because that is
what their training data contains — most of it written before mid-2025. §2 must therefore be restated in
`AGENTS.md`, and §5.2 exists so that a reflex reintroduction fails at compile time rather than in review.

---

## 6. Review and open items

Review at every .NET major upgrade and at least every six months. Re-verify licences from the upstream
`LICENSE` file, not from NuGet metadata or a blog post.

| Item | Due | Action |
|---|---|---|
| **MassTransit v8 support ends end of 2026** | now | Not in our stack. Relevant only for projects migrating onto this starter — confirm before quoting a migration. |
| Polly and the Open Source Maintenance Fee | next review | Reported to adopt OSMF from 16 Nov 2026, **single secondary source, unverified**. Polly is not currently a dependency. Verify at App-vNext before it becomes one. |
| `Microsoft.OpenApi` pin | on .NET 11 | 3.0.0 broke the .NET 10 source generator (`OpenApiMediaType` → `IOpenApiMediaType`). **No manual pin needed**: `Microsoft.AspNetCore.OpenApi` 10.0.12 constrains it to `[2.12.0, 3.0.0)` (ADR-016). Re-check the constraint on each `Microsoft.AspNetCore.OpenApi` update. |
| FluentAssertions 7.x in inherited projects | as encountered | Free indefinitely, but migrate to plain xUnit assertions (this starter) or AwesomeAssertions (Apache-2.0, API-compatible with 7.x) rather than accumulating pins. |

---

## 7. First deployment note

The first application built on this starter is an internal one. That changes the exposure but not the policy:
internal deployment triggers RPL-1.5 in full, the revenue threshold is evaluated against the organisation regardless of
who the user is, and internal use is commercial use for the purposes of the Xceed licence.

What internal use does change is the cost of being wrong. It is therefore the right place to prove that the
stack carries **zero commercially licensed and zero reciprocally licensed dependencies**, before that
property becomes contractually load-bearing in a client project. That constraint is the point of this file,
and it should not be relaxed for the pilot on the grounds that the pilot is easy.
