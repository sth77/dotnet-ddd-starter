# dotnet-ddd-starter — Design

Design basis for a .NET counterpart to [`sth77/spring-ddd-starter`](https://github.com/sth77/spring-ddd-starter).

The goal is **not** a port. It is the same *intent* — a seed project for tactical DDD with the conventions
baked in and enforced by the build — realised with the idioms and tools a .NET architect would reach for.
Every concept in the Java starter is triaged below into: **keep** (the idea survives, the mechanism changes),
**drop** (the concept exists only to work around a JVM/Spring limitation .NET does not have), or
**redesign** (the idea survives but the Java shape would be wrong in .NET).

Status of this document: design input, not yet validated by an implementation. Items marked ⚠ need to be
confirmed against a running spike before being written into the README as fact.

---

## 1. Baseline

| | Choice | Rationale |
|---|---|---|
| Runtime | **.NET 10** (LTS, Nov 2025 → Nov 2028) | Only sensible target. .NET 8/9 both go out of support 10 Nov 2026. |
| Language | C# 14 | Records, primary constructors, `required`, collection expressions. |
| ORM | **EF Core 10** | LTS, same support window. Complex types matured in EF 10 (see §6). |
| Web | **ASP.NET Core Minimal APIs** | Built-in validation (.NET 10) and endpoint metadata only work well here; see §7. |
| DB | PostgreSQL (prod + test via Testcontainers), SQLite for the fast inner loop | Avoid the EF in-memory provider — it is not a relational database and hides mapping bugs. |
| Messaging / outbox | **Wolverine 6.x** (`WolverineFx`, MIT) | The only OSS .NET stack that covers Spring Modulith's event-publication-registry role. 6.0 shipped May 2026 in the Critter Stack 2026 wave, targeting .NET 9/10. |
| Tests | xUnit v3, Testcontainers, `WebApplicationFactory`, Shouldly | See §15 on why *not* FluentAssertions. |
| Architecture tests | **ArchUnitNET** (Apache-2.0) | Direct lineage from ArchUnit; the rule vocabulary transfers. |

Assume a single deployable modular monolith, as in the Java starter.

---

## 2. Concept triage

| Java / Spring concept | Verdict | .NET realisation |
|---|---|---|
| Onion rings via jMolecules package annotations | **keep, different mechanism** | Project-per-ring + ArchUnitNET; assembly references make the direction structural, not advisory. |
| Feature modules (Spring Modulith top-level packages) | **keep, stronger mechanism** | Project-per-feature + `internal`. The C# compiler enforces what Modulith can only test. |
| jMolecules DDD types (`AggregateRoot<T,ID>`, `Identifier`, `Association`) | **keep, hand-rolled** | ~80 LOC in-repo `Ddd` library. nMolecules does not cover this — see §10. |
| jmolecules-byte-buddy (annotation-free JPA) | **drop** | EF Core's fluent API is annotation-free by construction. The whole problem disappears. |
| `ValueObjectAwareImplicitNamingStrategy` | **drop** | Vogen converters + EF complex types produce the same column shape by default. |
| `StringEnumTypeContributor` | **drop** | One line in `ConfigureConventions`. |
| Flyway + `ddl-auto: validate` | **redesign** | EF migrations + `has-pending-model-changes` as the drift gate. Optional DbUp if SQL-first is contractual. |
| Spring Data REST (auto-CRUD, search resources, URI binding) | **drop — no equivalent** | Explicit endpoints. This is the single biggest loss; §7.1. |
| HAL `_links`, `RepresentationModelProcessor` | **keep, hand-rolled** | ~150 LOC over `LinkGenerator` + `EndpointDataSource`. No credible .NET HAL library exists. |
| `SecuredAggregateCommands` (reflect `@Secured`) | **keep, better mechanism** | Read `IAuthorizeData` off endpoint metadata + `IAuthorizationService`; §7.4. |
| Spring Data projections (`@Projection`, SpEL) | **redesign** | Explicit response records + LINQ `Select`. No SpEL equivalent, and that is fine. |
| Modulith event publication registry | **keep** | Wolverine durable inbox/outbox on EF Core. |
| `@RetryableApplicationModuleListener` | **keep** | Wolverine `OnException<T>().RetryWithCooldown(...)` policy. |
| `AbstractAggregateRoot.registerEvent` | **keep** | Event list on the aggregate base; Wolverine scrapes the EF `ChangeTracker`. ⚠ verify exact API. |
| Modulith `Scenario` API test | **keep** | Wolverine *Tracked Sessions*. |
| JSpecify + NullAway | **drop** | `<Nullable>enable</Nullable>` + warnings-as-errors. Native, no plugin, no `package-info`. |
| Lombok | **drop** | Records, primary constructors, `required`. |
| Jakarta Bean Validation `@Valid` | **keep** | `Microsoft.Extensions.Validation` (.NET 10), source-generated, → RFC 9457 ProblemDetails. |
| `DomainExceptionHandler` | **keep** | `IExceptionHandler` + `AddProblemDetails()`. |
| ArchUnit rules | **keep** | ArchUnitNET + a few Roslyn analyzers (§9). |
| jmolecules-apt (compile-time DDD rules) | **keep, hand-rolled** | Own Roslyn analyzers + `BannedApiAnalyzers`. |
| Hygen templates | **redesign** | `dotnet new` template pack; §13 covers the injection gap honestly. |
| Spotless + Eclipse formatter | **keep** | `.editorconfig` + `dotnet format --verify-no-changes`. |
| springdoc-openapi | **keep** | `Microsoft.AspNetCore.OpenApi` + Scalar. |
| Spring profiles | **keep** | `appsettings.{Environment}.json` + `IOptions<T>` with `ValidateOnStart`. |
| Actuator | **keep** | Health checks + OpenTelemetry. |

---

## 3. Solution layout

The Java starter keeps feature packages top-level and prefixes `_application` / `_infrastructure` to escape
Modulith's conventions. In .NET the equivalent structural device is the **project**, and it is enforced by the
compiler rather than by a test. That is a real upgrade, and the layout should exploit it.

```
dotnet-ddd-starter.slnx
  src/
    App.Domain/                        # domain ring — no EF, no ASP.NET, no Wolverine
      Common/                            Ddd abstractions, DomainException, I18nText, Principal
      Sample/                            Sample, SampleCommand, SampleEvent, ISamples, City (VO)
      Person/                            Person, PersonCommand, PersonEvent, IPeople
      ReferenceData/                     City (entity), ICities
    App.Application/                   # application ring — orchestration, event handlers
      SampleOwnerNameSynchronizer.cs
    App.Infrastructure/                # infrastructure ring — EF, external APIs, security
      Persistence/                       AppDbContext, IEntityTypeConfiguration<T>, repositories
      Migrations/
      Security/
    App.Api/                           # infrastructure ring — HTTP surface
      Sample/                            SampleEndpoints, SampleSummary, SampleDetail, SampleLinks
      Common/                            HAL primitives, ProblemDetails wiring
    App.Host/                          # composition root, Program.cs
  tests/
    App.ArchitectureTests/
    App.Domain.Tests/                  # fast: no host, no DB
    App.IntegrationTests/              # Testcontainers + WebApplicationFactory
  templates/                           # dotnet new template pack
  Directory.Build.props
  Directory.Packages.props
  .editorconfig
```

**Ring enforcement is then a project-reference fact**: `App.Domain` references nothing but the BCL and the
tiny `Ddd` abstraction. If someone tries to `using Microsoft.EntityFrameworkCore` in the domain, it does not
compile. ArchUnitNET is then only needed for the finer-grained rules, not for the ring direction.

**Feature-module boundaries — two options:**

- **A. Folder-per-feature inside `App.Domain`** (mirrors the Java layout). Cheap, familiar, but module
  boundaries are then only test-enforced, exactly as in Modulith.
- **B. Project-per-feature** (`App.Sample.Domain`, `App.Person.Domain`, …). The compiler enforces module
  boundaries and `internal` gives real module-private types — something Java packages cannot express without
  JPMS.

**Recommendation: start with A, design so that B is a mechanical refactor.** Project-per-feature costs build
time and solution noise and only pays off past ~5 modules; but the naming and the dependency discipline
should be chosen now so that splitting later is a move, not a rewrite. Document the trigger for switching.

---

## 4. Domain building blocks

No framework types in the domain. A small in-repo abstraction, closer to jMolecules' *interfaces* than to
nMolecules' *attributes*, because C# generics let the interface carry the ID type — which analyzers, EF
configuration and repositories can all then use.

```csharp
namespace App.Domain.Common;

public interface IIdentifier;

public interface IEntity<TId> where TId : IIdentifier
{
    TId Id { get; }
}

public interface IAggregateRoot<TSelf, TId> : IEntity<TId>
    where TSelf : IAggregateRoot<TSelf, TId>
    where TId : IIdentifier;

public interface IDomainEvent;

public interface ICommand;

/// Reference to an aggregate in another module, by identity only.
public readonly record struct Association<TAggregate, TId>(TId Id)
    where TAggregate : IAggregateRoot<TAggregate, TId>
    where TId : IIdentifier;
```

`Association<,>` matters as much here as in Java: it is the type that makes "never hold an object reference
across a module boundary" visible and testable, and it is the thing an ArchUnitNET rule keys on.

### 4.1 Value objects and identifiers — use Vogen

[Vogen](https://github.com/SteveDunn/Vogen) (Apache-2.0) is a source generator + analyzer that turns a
primitive into a validated value object and generates the EF Core `ValueConverter`/`ValueComparer`, the
`System.Text.Json` converter, and more. It also emits **compile errors** for `new SampleId()` and
`default(SampleId)` — a stronger guarantee than anything the Java starter has.

```csharp
[ValueObject<Guid>]
public readonly partial struct SampleId : IIdentifier
{
    public static SampleId New() => From(Guid.CreateVersion7());
}
```

This one package replaces three things at once: the jMolecules `Identifier` interface, `jmolecules-jackson`
(bare-value JSON), and the single-value-wrapper half of `ValueObjectAwareImplicitNamingStrategy`. Guid v7 is
worth defaulting to — sequential, index-friendly, and native in .NET 9+.

Multi-field value objects are plain records and map as EF **complex types** (§6):

```csharp
public sealed record I18nText(string En, string De);
public sealed record City(int PostalCode, I18nText Name);
```

### 4.2 Aggregates, commands, events

C# has no sealed interfaces, but an abstract record with a private constructor gives the same closed
hierarchy — only nested types can derive:

```csharp
public abstract record SampleCommand : ICommand
{
    private SampleCommand() { }

    public sealed record Create(
        [property: Required] I18nText Name,
        [property: MaxLength(1000)] string Description,
        CityId? City,
        PersonId Owner) : SampleCommand;

    public sealed record Update(I18nText Name, string Description, CityId? City) : SampleCommand;
    public sealed record Publish : SampleCommand;
    /// Internal: no endpoint handles it, so it never becomes a HAL link.
    public sealed record UpdateOwnerName(string OwnerName) : SampleCommand;
}
```

The aggregate keeps the Java starter's discipline — one command per operation, `Can(...)` guard, events
registered rather than published:

```csharp
public sealed class Sample : AggregateRoot<Sample, SampleId>
{
    private Sample(/* ... */) { }

    public I18nText Name { get; private set; }
    public City? City { get; private set; }
    public SampleState State { get; private set; }
    public Association<Person, PersonId> Owner { get; }
    public string OwnerName { get; private set; }   // denormalised copy

    public static Sample Create(SampleCommand.Create data, ReferenceData.City? city, Person owner) { /* ... */ }

    public void Update(SampleCommand.Update data)
    {
        AssertCan<SampleCommand.Update>();
        // ...
        RegisterEvent(new SampleEvent.Updated(Id, Name, Description));
    }

    public bool Can<TCommand>() where TCommand : SampleCommand => Can(typeof(TCommand));
    public bool Can(Type command) => /* state machine */;
}
```

`AggregateRoot<TSelf,TId>` base class holds `Id`, the `[Timestamp]`-free concurrency token, and
`private readonly List<IDomainEvent> _events` with `IReadOnlyList<IDomainEvent> DomainEvents` — the analogue
of Spring's `AbstractAggregateRoot`. Optimistic concurrency is configured once in the DbContext
(`xmin` on PostgreSQL, `rowversion` on SQL Server), so no aggregate carries a version property, matching
the Java starter's `AbstractAggregate`.

### 4.3 Repositories

Keep the naming convention — plural of the aggregate, domain language, no `Repository` suffix. But the
interface now lives in the domain and the implementation in infrastructure, which is what the onion wants
anyway and what Spring Data's framework-coupled interfaces never quite allowed:

```csharp
// App.Domain/Sample/ISamples.cs
public interface ISamples
{
    Task<Sample?> FindAsync(SampleId id, CancellationToken ct = default);
    Task<Sample> GetRequiredAsync(SampleId id, CancellationToken ct = default);
    Task<IReadOnlyList<Sample>> FindByOwnerAsync(Association<Person, PersonId> owner, CancellationToken ct = default);
    void Add(Sample sample);
}
```

**What you lose:** Spring Data's derived query methods. `findByOwner` writes itself in Java; here it is three
lines of LINQ. Acceptable, and arguably clearer.

**What you must not do:** expose `IQueryable<Sample>` from the repository. That leaks EF into the domain and
hands callers the ability to bypass the aggregate. Read models get their own read-side path (§7.3).

### 4.4 Reference (master) data

The Java starter's model transfers unchanged, and it is one of the better ideas in it: reference data is an
entity with its own lifecycle that aggregates **copy fields from** rather than associate with, so an
aggregate records the city as it was at creation time.

The one part that does *not* transfer is Spring Data REST's URI binding (`"city": "/api/cities/{id}"`).
Commands should carry `CityId`, the endpoint resolves it via `ICities`, and the aggregate copies the fields.
This is a simplification, not a loss: the command shape stops depending on the API's URL space.

---

## 5. Persistence

### 5.1 The annotation-free domain model comes for free

This is the single largest simplification versus the Java starter. The whole byte-buddy apparatus, the
IntelliJ setup instructions, the `Not a managed type` failure mode, the naming-strategy class and the
ArchUnit rule banning `@Column`/`@Table`/`@AttributeOverride` in the domain — all of it exists because JPA
insists on annotating the model. EF Core's fluent API puts the mapping in
`IEntityTypeConfiguration<T>` classes in the infrastructure project by design:

```csharp
// App.Infrastructure/Persistence/SampleConfiguration.cs
internal sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> b)
    {
        b.HasKey(x => x.Id);
        b.ComplexProperty(x => x.Name);          // -> name_en, name_de
        b.ComplexProperty(x => x.City);          // -> city_postal_code, city_name_en, city_name_de
        b.Property(x => x.Owner).HasConversion(/* Association -> Guid */);
        b.Ignore(x => x.DomainEvents);
    }
}
```

Keep one rule from the Java starter, because it is still worth having: **`System.ComponentModel.DataAnnotations.Schema`
and EF attributes are banned in `App.Domain`.** Enforce with `Microsoft.CodeAnalysis.BannedApiAnalyzers`
(`BannedSymbols.txt`) rather than an architecture test — it fails at compile time, in the editor, with a
squiggle on the attribute.

### 5.2 Value objects → complex types

EF 10 is the release that makes this work properly:

- Complex types have **value semantics** — assigning one to two properties works, LINQ comparison compares
  contents. Owned entity types do neither. The docs now explicitly advise switching from owned types to
  complex types for table splitting and JSON.
- EF 10 adds **optional** complex types (needed for `City?` on `Sample`), with the caveat that an optional
  complex type must have at least one required property.
- Collections inside a complex type require `ToJson()` mapping; table splitting does not support them.
- Complex types can be structs; collections of structs are not supported.

Column naming defaults to `OwningProperty_Field` — i.e. exactly the "multi-field VO keeps the owning
attribute prefix" rule the Java starter implements by hand. Add
`UseSnakeCaseNamingConvention()` and the physical names match too.

Single-value wrappers collapse to the owning column automatically, because Vogen generates a
`ValueConverter` and a converted property is a single column named after the property. The
`<type>Value`-suffix convention and the naming-strategy class have no reason to exist here.

### 5.3 Enums

Hibernate has no global "enums as string" switch, which is why the Java starter needs a `TypeContributor`
registered through `META-INF/services`. EF Core's pre-convention model configuration does have one:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder b)
{
    b.Properties<Enum>().HaveConversion<string>();
}
```

The docs state the type in `Properties<T>()` may be a base type and that matching configurations are applied
from least specific, so `Enum` covers every enum property in the model. ⚠ Confirm on the spike; the fallback
is a `IModelFinalizingConvention` walking properties where `ClrType.IsEnum`.

### 5.4 Migrations and drift

The Java starter's discipline — **migration per change, never edit an applied migration, schema drift fails
the build** — transfers directly, but the mechanism differs and the choice deserves a deliberate decision.

**Recommended: EF migrations.** `dotnet ef migrations add` generates from the model, which removes the
hand-written-SQL/mapping divergence that `ddl-auto: validate` exists to catch. The drift gate becomes:

```bash
dotnet ef migrations has-pending-model-changes   # added EF Core 8; exits non-zero on drift
```

or, better for a starter, a unit test — `context.Database.HasPendingModelChanges()` — so the gate runs in
`dotnet test` without the `dotnet-ef` tool being installed. Since EF Core 9, `Migrate()` itself throws a
`PendingModelChangesWarning` when the model has uncommitted changes, so the failure also surfaces at startup,
which is the closest analogue to `ddl-auto: validate`.

**Alternative: DbUp or FluentMigrator** if the client mandates hand-written, reviewable SQL — common in Swiss
public-sector contracts. Then you keep the Java starter's exact model (`V0001__initial_schema.sql`, new file
per change) and lose the automatic drift detection unless you add a test that migrates a Testcontainer and
diffs against the EF model. Note this in the README as a documented fork point rather than a silent default.

Deployment: `dotnet ef migrations bundle` produces a self-contained executable — run it as a one-shot job,
not from every application replica's entrypoint.

---

## 6. HTTP API

### 6.1 There is no Spring Data REST, and pretending otherwise is a trap

A large share of the Java starter's value comes from Spring Data REST: collection and item resources for
every exported repository, search resources from finder methods, URI-to-entity conversion, the `profile`
resource, `projection` query parameter. **.NET has nothing comparable that is maintained and production-grade.**
The realistic options were OData (a different, heavier contract) or JSON:API via JsonApiDotNetCore (couples
the API shape to a spec the client may not want).

So the design decision is: **write the endpoints explicitly, and put the leverage into the scaffolding
instead.** Each aggregate gets a generated endpoint module. That is more code than Spring Data REST, and it
is the honest price of the platform. It also removes the awkwardness the Java starter has to engineer around
— disabling PUT/PATCH/DELETE on every exported repository so that state changes cannot bypass the domain.
In .NET those endpoints simply never exist.

### 6.2 Endpoint modules

```csharp
internal static class SampleEndpoints
{
    public static void MapSamples(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/samples").WithTags("Samples");

        group.MapGet("/", ListAsync).WithName(SampleRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(SampleRels.Self);

        group.MapPost("/", CreateAsync)
             .WithName(SampleRels.Create)
             .RequireAuthorization(Policies.User);

        group.MapPost("/{id}/publish", PublishAsync)
             .WithName(SampleRels.Publish)
             .RequireAuthorization(Policies.Admin);
    }
}
```

Minimal APIs over MVC controllers, for three concrete reasons: `Microsoft.Extensions.Validation` supports
minimal APIs and Blazor but **not** MVC; endpoint metadata is the mechanism §6.4 depends on; and named
endpoints give `LinkGenerator` a stable key, which is what makes HAL link generation tractable.

### 6.3 Projections → response records

Spring Data projections do two jobs: shape the response and influence the SQL. In .NET the second is LINQ's
job and the first is a record's:

```csharp
public sealed record SampleSummary(SampleId Id, string Name, SampleState State, string OwnerName);

var summaries = await db.Samples
    .Select(s => new SampleSummary(s.Id, s.Name.En, s.State, s.OwnerName))
    .ToListAsync(ct);
```

`?projection=summary` should not be reproduced. Two endpoints, or two routes in one group, are clearer than
a magic query parameter, and OpenAPI can describe them.

The SpEL trick (`@Value("#{@people.resolveRequired(target.owner)}")`) has no .NET equivalent and should not
be simulated. Where a detail response needs the owner, the read side joins explicitly. The starter should
keep the Java version's more interesting half — the **denormalised `OwnerName` kept in sync by an event
handler** — because that is the pattern worth teaching (§8).

Read queries may bypass the repository and use the `DbContext` directly from the API project. State changes
may not. That split is worth stating as a rule and enforcing with an ArchUnitNET test.

### 6.4 HAL links and authorisation-aware link visibility

Keep the concept: a command link appears only when the aggregate permits the operation in its current state
**and** the current user may invoke it. The Java implementation reflects over the controller's `@Secured`
annotations. ASP.NET Core offers something better, because authorisation policies are *endpoint metadata*
that the framework itself uses:

```csharp
internal sealed class SampleLinks(
    LinkGenerator linkGenerator,
    IAuthorizationService authorization,
    EndpointDataSource endpoints)
{
    public async Task<IReadOnlyList<HalLink>> ForAsync(Sample sample, HttpContext http)
    {
        var result = new List<HalLink> { Self(sample, http) };

        foreach (var (commandType, endpointName) in SampleRels.CommandEndpoints)
        {
            if (!sample.Can(commandType)) continue;
            if (!await IsAuthorizedAsync(endpointName, http)) continue;

            result.Add(new HalLink(
                Rel: SampleRels.RelFor(commandType),
                Href: linkGenerator.GetUriByName(http, endpointName, new { id = sample.Id.Value })!));
        }
        return result;
    }
}
```

`IsAuthorizedAsync` resolves the endpoint by name from `EndpointDataSource`, reads its `IAuthorizeData`
metadata, and calls `IAuthorizationService`. The policy attached to the route is the single source of truth,
exactly as `@Secured` is in the Java version — but read from the live routing table rather than by reflecting
over a class, so it cannot drift even if the endpoint is registered by a filter or a convention.

**Startup validation instead of an architecture test.** The Java starter needs an ArchUnit rule
(`stateChangingOperationsAreSecured`) because an unannotated operation is silently open. In .NET, walk
`EndpointDataSource` at startup and throw if any `POST`/`PUT`/`PATCH`/`DELETE` endpoint under `/api` lacks
authorisation metadata. It sees the real routing table, not the source, and it fails the integration test
suite on the first request. Keep an ArchUnitNET rule as well if you want the faster feedback.

⚠ `IAuthorizationService.AuthorizeAsync` per command per aggregate per list item is N×M policy evaluations on
a collection response. Cache the per-request decision per policy name; measure before shipping.

### 6.5 Validation and errors

```csharp
builder.Services.AddValidation();      // .NET 10, source-generated, no request-time reflection
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
```

`AddValidation()` validates DataAnnotations on minimal API parameters and bound types before the handler runs
and returns RFC 9457 ProblemDetails — the direct replacement for `@Valid @RequestBody`. Note the known
limitation that nullable value types as minimal API parameters are not validated (dotnet/aspnetcore#67033).

`DomainException` → `IExceptionHandler` → 409/422 ProblemDetails. Same shape as the Java starter, native
mechanism, and ProblemDetails is a published standard where the Java version's error format is ad hoc.

### 6.6 OpenAPI

`Microsoft.AspNetCore.OpenApi` (built in since .NET 9) plus **Scalar** for the UI; Swashbuckle is no longer in
the templates. ⚠ Pin `Microsoft.OpenApi` — the 3.0.0 release introduced a breaking change
(`OpenApiMediaType` → `IOpenApiMediaType`) that breaks the .NET 10 source generator; 2.3.9 is the known-good
version until .NET 11.

---

## 7. Modules and domain events

The Java starter's three rules survive intact and should be stated verbatim in the .NET `INSTRUCTIONS.md`:
reference other modules by identity only; denormalise what you need to read; synchronise through domain
events.

The infrastructure changes. **Wolverine** covers what Spring Modulith's event publication registry covers:

| Modulith | Wolverine |
|---|---|
| `@ApplicationModuleListener` (async, own transaction, after commit) | Message handler + `UseDurableLocalQueues()` |
| `event_publication` table, redelivery on restart | Transactional inbox/outbox on EF Core |
| `@Retryable` with backoff/jitter | `OnException<T>().RetryWithCooldown(...)` policies |
| Events registered on the aggregate, published on save | EF `ChangeTracker` domain-event scraping ⚠ verify API |
| `Scenario` API (publish X, await Y) | Tracked Sessions |

```csharp
public sealed class SampleOwnerNameSynchronizer(ISamples samples)
{
    public async Task Handle(PersonEvent.Updated e, CancellationToken ct)
    {
        foreach (var sample in await samples.FindByOwnerAsync(new Association<Person, PersonId>(e.PersonId), ct))
            sample.UpdateOwnerName(new SampleCommand.UpdateOwnerName(e.Name));
    }
}
```

Handlers must be idempotent — the inbox redelivers on restart, same as Modulith's registry. Say so in the
instructions file; it is the failure mode people hit first.

Wolverine's own modular-monolith guidance recommends a `DbContext` per module against separate schemas in one
physical database, and avoiding cross-module foreign keys so modules stay severable. That is compatible with
option B in §3 and is another reason to keep the layout split-ready. Note the current constraint:
Wolverine supports the transactional inbox/outbox with **a single database** for EF Core.

**What .NET does not get:** Spring Modulith's generated C4/PlantUML documentation and `@ApplicationModuleTest`
slicing. The documentation gap is real; the test-slicing gap is smaller, because a domain project with no
framework references already gives sub-second unit tests without any slicing machinery.

---

## 8. Architecture enforcement

Four layers, cheapest first:

1. **Project references.** Ring direction. Free, structural, instant.
2. **Roslyn analyzers.** Compile-time, in-editor:
   - `BannedApiAnalyzers` → no EF/schema attributes in `App.Domain`; no `DateTime.Now`/`DateTime.UtcNow`
     (use `TimeProvider`); no `Newtonsoft.Json`.
   - **One custom analyzer worth writing**: *every public, non-static, non-accessor method on an
     `IAggregateRoot<,>` takes exactly one parameter that implements `ICommand`.* This is the Java starter's
     `aggregateOperationsTakeCommands` rule, and as a Roslyn analyzer it fails in the IDE as you type rather
     than in `dotnet test`. Ship it with a code fix that generates the command record.
   - Vogen's own analyzers cover value-object misuse.
3. **ArchUnitNET tests** for what needs whole-assembly reachability: no module cycles; no `App.Domain` type
   reachable from another feature except through `Association<,>`; repositories return no `IQueryable`;
   endpoint projects do not reference each other.
4. **Startup validation** for what only exists at runtime: every state-changing endpoint has an authorisation
   policy (§6.4); every `IOptions<T>` validated with `ValidateOnStart`.

---

## 9. Is nMolecules a good choice? — No, not as the foundation

**Findings (verified September 2026):**

- `NMolecules.DDD` is at **0.2.2, published 1 December 2022** — a single listed version, ~63k total
  downloads, **no dependent packages on NuGet**, Apache-2.0, targeting .NET Standard 1.0. The GitHub repo's
  last commit is 1 December 2022; the README still reads "TODO: port documentation from jMolecules".
- It ships **attributes only** — `[Entity]`, `[ValueObject]`, `[AggregateRoot]`, `[Repository]`, `[Identity]`,
  `[Service]`, `[Factory]`, `[Module]`, `[BoundedContext]`. There is no `AggregateRoot<T,ID>`, no
  `Identifier`, and critically **no `Association<T,ID>`** — the type the whole loose-coupling story in the
  Java starter rests on.
- `NMolecules.Architecture` covers **Layered only**. There are no Onion or Hexagonal attributes, so the
  starter's central architectural concept — the simplified onion with three rings — has no vocabulary at all.
- `xmolecules/nmolecules-integrations` contains a genuinely useful Roslyn analyzer set
  (`XMoleculesAggregateRoot0001–0003`, `XMoleculesEntity0001–0004`, `XMoleculesValueObject0001–0005`,
  `XMoleculesRepository0001`) — roughly `JMoleculesDddRules` at compile time. Last commit **7 May 2023**.
- There is **no** nMolecules equivalent of `jmolecules-jpa`, `jmolecules-jackson`, `jmolecules-spring`,
  `jmolecules-bytebuddy`, or `jmolecules-archunit`. The integration story that makes jMolecules load-bearing
  in the Java starter does not exist.

**Assessment.** In the Java starter jMolecules earns its place three times over: it generates the JPA
annotations, it serialises identifiers, and it supplies the ArchUnit rule sets. nMolecules does none of those.
What remains is a documentation vocabulary — and in C#, interfaces express the same thing while also carrying
the ID type into the type system, which attributes cannot.

**Recommendation:**

1. Define the building-block **interfaces in-repo** (§4). Roughly 80 lines, zero dependency, no supply-chain
   review for a regulated client.
2. **Optionally** decorate types with nMolecules attributes for xMolecules-family consistency and
   cross-language recognisability — the cost is one dormant Apache-2.0 dependency with no transitive deps.
   Worth it only if the .NET starter is meant to sit visibly alongside the Java one. If you do, expect to
   pin it forever and to explain the 2022 date to anyone who checks.
3. **Do not** depend on `nMolecules.Analyzers` for the build gate. The rules are the right rules, but the
   package has not been touched since Roslyn 4.x and the analyzer surface has moved since. Write the two or
   three rules you actually need (§8) against the in-repo interfaces.
4. If ecosystem alignment matters to you: the missing Onion/Hexagonal attributes and an `Association<,>` type
   are a small, well-scoped upstream contribution. That is a better path to "nMolecules is a good choice"
   than adopting it as-is and working around the gaps.

---

## 10. Concepts worth adding that the Java starter does not have

These are where .NET has something to offer back, not just catch up on.

1. **Compiler-enforced module privacy.** `internal` + `InternalsVisibleTo` for tests gives module-private
   types that Java packages cannot express. A repository implementation can be `internal` to its module and
   genuinely unreachable, rather than reachable-but-tested-against.
2. **Central Package Management + supply-chain gates.** `Directory.Packages.props`, `<NuGetAudit>`,
   `<NuGetAuditMode>all</NuGetAuditMode>`, package source mapping, a lock file. For regulated and federal
   clients this is not optional, and it belongs in the starter rather than being rediscovered per project.
3. **`TimeProvider` everywhere.** Inject it into aggregates that need time; ban `DateTime.Now`/`UtcNow` via
   `BannedApiAnalyzers`. Deterministic domain tests with no Clock abstraction of your own.
4. **Source-generated JSON.** `JsonSerializerContext` for the API contract types: faster, trim/AOT-safe, and
   it makes the serialisable surface explicit and reviewable.
5. **RFC 9457 ProblemDetails as the error contract**, standard rather than bespoke.
6. **.NET Aspire** for the local inner loop — Postgres container, dashboard, OpenTelemetry wiring, and
   `AddEFMigrations` for migration coordination. This is the counterpart to Spring Boot's Docker Compose
   support, and a better one. Keep it optional: Aspire adds real conceptual surface, and not every client
   will want it.
7. **`WolverineFx.Http`** as an alternative to hand-written endpoint modules — handler-per-operation with the
   transactional middleware applied automatically. Worth prototyping as a second endpoint style before
   committing to the minimal-API-plus-scaffolding approach; it may cut the generated code substantially.
8. **Snapshot-test the HAL contract** (`Verify`). The link set per aggregate state × role is exactly the kind
   of thing that regresses silently, and approval tests catch it cheaply.
9. **`InternalsVisibleTo`-free test seams via `[ValidatableType]` and public read models** — keep the test
   suite honest about what is really public API.

---

## 11. Concepts to drop, and why

Stated explicitly so the .NET README does not inherit them by reflex:

- **byte-buddy/APT machinery and its IDE setup.** No equivalent problem exists.
- **The value-object naming strategy.** EF's complex-type naming plus snake_case conventions already produce
  the target schema.
- **The enum `TypeContributor`.** One line of configuration.
- **`@NullMarked` per package and its two ArchUnit rules.** `<Nullable>enable</Nullable>` in
  `Directory.Build.props` covers the whole solution.
- **The `_application` / `_infrastructure` underscore prefix.** It exists to dodge Spring Modulith's
  "every top-level package is a module" convention. Projects make it unnecessary.
- **`?projection=` and SpEL-powered projections.** Explicit endpoints and explicit records.
- **URI-valued command fields.** Ids in commands.

---

## 12. Scaffolding

The Java starter's Hygen generators are its most practical feature, and the requirement transfers exactly:
`feature`, `aggregate`, `controller`, `referencedata`.

**Primary: a `dotnet new` template pack** (`D4S.Ddd.Templates`), installed with `dotnet new install`:

```bash
dotnet new ddd-feature      -n Todo
dotnet new ddd-aggregate    -n Todo --feature todo
dotnet new ddd-endpoints    -n Todo --feature todo
dotnet new ddd-refdata      -n Country
```

Templates are the native tool, need no Node, and are distributable as a NuGet package — which matters for
rolling this out across projects rather than per-repo copies.

**The honest gap:** `dotnet new` templates create files well but do not *inject* into existing ones. Hygen's
`inject` is used for exactly that (wiring a new aggregate into the DbContext, registering endpoints, adding
the migration). Three options:

- **A.** Design the code so no injection is needed: `ApplyConfigurationsFromAssembly`, assembly-scanned
  endpoint registration via a marker interface, `dotnet ef migrations add` run as a template post-action.
  **Preferred** — it removes the problem rather than tooling around it, and it makes the codebase more
  conventional in the process.
- **B.** Keep Hygen. It works, but adds a Node dependency to a .NET repo — a hard sell in a locked-down
  enterprise environment.
- **C.** A small `dotnet tool` for the injecting generators. Most work, most control.

**Keep the `-Phygen-it` idea in whatever form.** A CI job that generates a throwaway feature from the
templates, wires reference data into it, runs the generated tests, and cleans up is what keeps templates and
sample code from drifting. It is one of the genuinely distinctive things about the Java starter and it should
survive the port. In .NET: a script that `dotnet new`s into a temp directory, builds, tests, and discards.

---

## 13. Build gates

Everything below must fail the build, matching the Java starter's posture:

```bash
dotnet format --verify-no-changes            # .editorconfig is the authority
dotnet build -warnaserror                    # nullable + analyzers + banned APIs
dotnet test                                  # unit + ArchUnitNET + HasPendingModelChanges
dotnet test --filter Category=Integration    # Testcontainers + WebApplicationFactory
dotnet list package --vulnerable --include-transitive
```

`Directory.Build.props` carries `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
`<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, `<AnalysisLevel>latest-all</AnalysisLevel>`.

Note one difference from the Java starter worth documenting: NullAway has to be wired into the compiler
plugin path with `--add-exports` hacks and is scoped to main sources because test idioms trip it. C# nullable
reference types have none of that — they apply to tests too, and the test code is better for it.

---

## 14. Dependency policy — this section is load-bearing in 2026

The .NET ecosystem has had a wave of licence changes that an enterprise starter must take a position on.
Verified as of September 2026:

| Package | Status | Starter position |
|---|---|---|
| **MediatR** | Dual RPL-1.5 / commercial (Lucky Penny Software) from 13.0.0; 12.5.0 last Apache-2.0 | **Do not use.** Wolverine, `martinothamar/Mediator` (MIT), or nothing — the design has no mediator role. |
| **AutoMapper** | Same model from 15.0.0; 14.0.0 last permissive | **Do not use.** `Select(...)` into response records; Mapperly (Apache-2.0) if a mapper is ever needed. |
| **MassTransit** | v9 commercial and source-available (Massient); v8 Apache-2.0 but loses support end of 2026 | **Do not use.** Wolverine. |
| **FluentAssertions** | 8.0+ Xceed Community Licence — *non-commercial use only*; 7.x Apache-2.0 indefinitely | **Do not use.** Shouldly (BSD-3) over pinning 7.x; AwesomeAssertions (Apache-2.0) for migrations. |
| **Wolverine** | MIT (verified in repo `LICENSE`, `main`); current line 6.x | **Recommended.** JasperFx sells support and Pro add-ons *alongside* the MIT core — open-core, not relicence. |
| **Vogen** | Apache-2.0 | Recommended. |
| **ArchUnitNET** | Apache-2.0 | Recommended. |
| **Polly** | Reported to adopt the Open Source Maintenance Fee from 16 Nov 2026 — ⚠ single secondary source, unverified | Not a dependency. Verify at App-vNext before it becomes one. |

**The RPL-1.5 trap deserves naming**, because the obvious mental model is wrong. Its obligations attach on
*Deploy in any form — internally or to an outside party*, and extend to every component you author. Internal
deployment of a line-of-business application triggers full source disclosure of that application. So the
"free open-source path" for MediatR and AutoMapper is not available to the organisation in any scenario, including the
internal pilot.

Two further things that catch consultancies specifically: the Lucky Penny Community licence covers client
work only if the *client* would also qualify under the USD 5M threshold, and MassTransit requires a licence
for any project under an agency's development or maintenance, with the client needing their own on handover.

Three consequences for the starter itself:

1. **Ship the policy as a document.** See `DEPENDENCIES.md` — licence class policy, per-package register with
   a named replacement, and the enforcement wiring. For federal and regulated clients this is a deliverable.
2. **Enforce it in the build**, because neither Lucky Penny nor MassTransit fails closed: a missing or expired
   key logs a warning and keeps running. `nuget-license` (Apache-2.0) with `--include-transitive` as a CI gate,
   plus an MSBuild denylist target for the four packages already decided about.
3. **Treat it as an AI-agent guardrail.** Agents default to MediatR/AutoMapper/MassTransit/FluentAssertions
   because most of the training corpus predates mid-2025. The list belongs in `AGENTS.md` *and* in a
   build-time check, or it gets reintroduced.

**Constraint for the first deployment:** the pilot is an internal application. Internal use does not
soften any of the above — it only makes being wrong cheaper to fix. It is therefore the right place to prove
a stack with zero commercially licensed and zero reciprocally licensed dependencies, before that property
becomes contractually load-bearing.

---

## 15. AI agent support

Carry over the Java starter's approach — a generic instructions file plus tool-specific ones — and add what
.NET specifically needs:

- **`AGENTS.md`** (the emerging cross-tool convention) with `CLAUDE.md` pointing at it.
- **Known-failure signatures**, the Java starter's best idea for agents. The .NET equivalents:
  `PendingModelChangesWarning` at startup → model drifted from migrations, add one;
  `VOG009`/`VOG010` → a value object constructed with `new`/`default`;
  complex-type mapping failure on an optional VO → EF 10 requires at least one required property;
  `IOpenApiMediaType.Example cannot be assigned` → `Microsoft.OpenApi` 3.0.0, pin 2.3.9.
- **Correct-the-cutoff notes**, as in the Java `CLAUDE.md`. Models trained before mid-2025 will not know that
  MediatR/AutoMapper/MassTransit/FluentAssertions went commercial, that Swashbuckle is out of the templates,
  that `AddValidation()` exists, or that EF 10 complex types supersede owned types. State it explicitly.
- **The template-regeneration CI job doubles as an agent guardrail**: it proves the agent's "just write it by
  hand" output has not drifted from the scaffolded shape.

---

## 16. Risks and open questions

1. **HAL is a minority taste in .NET.** Before building the link machinery, decide whether the clients of
   these APIs actually consume hypermedia. If they are TypeScript SPAs written by the same team, a vendor
   media type with a `commands: []` array is simpler and just as useful. The *capability-discovery* idea is
   what matters; HAL is one encoding of it. Recommend building the abstraction so the encoding is swappable.
2. **Scaffolding volume.** Without Spring Data REST, an aggregate needs endpoints, response records, link
   factory, EF configuration, migration. Prototype the generated output for one aggregate and count the lines
   before committing to the design. If it is more than roughly 150, look harder at `WolverineFx.Http`.
3. **Wolverine is a single-maintainer-led stack.** MIT and healthy, but concentrate the dependency: handlers
   should be plain classes with plain methods so that swapping the bus is a wiring change.
4. **Project-per-module vs folder-per-module** (§3) needs an explicit decision and a documented trigger.
5. **Authorisation-aware link generation cost** on collection endpoints (§6.4) — measure.
6. ⚠ Three claims to verify on the spike: `Properties<Enum>()` covering all enum types; Wolverine's
   `ChangeTracker` domain-event publication API and whether it plays well with an aggregate base class that
   holds events; optional complex types for `City?` given the "at least one required property" rule.

---

## 17. Roadmap

| Step | Outcome |
|---|---|
| 0 | Spike: one aggregate, EF 10 complex types, Vogen IDs, enum convention, drift test. Resolves the three ⚠s. |
| 1 | Solution skeleton, `Directory.Build.props`/`Directory.Packages.props`, ring projects, CI — **including the licence gate and denylist target**, which have to be in place before dependencies accumulate rather than retrofitted at step 9. |
| 2 | Domain building blocks, `Sample` + `Person` + `ReferenceData.City`, domain unit tests. |
| 3 | Persistence: DbContext, configurations, migrations, Testcontainers integration tests. |
| 4 | API: endpoint modules, response records, validation, ProblemDetails, OpenAPI + Scalar. |
| 5 | HAL links + authorisation-aware visibility + startup endpoint validation. |
| 6 | Wolverine: outbox, `SampleOwnerNameSynchronizer`, Tracked Session test. |
| 7 | Enforcement: ArchUnitNET suite, BannedApiAnalyzers, the command-parameter analyzer. |
| 8 | `dotnet new` template pack + the regeneration CI job. |
| 9 | `README.md`, `INSTRUCTIONS.md`, `AGENTS.md`, `DEPENDENCIES.md`. |

Steps 0–4 are the minimum viable starter. 5–8 are what make it *this* starter rather than another clean-architecture template.
