# Architectural decision records

Decisions taken while implementing [`dotnet-ddd-starter-design.md`](dotnet-ddd-starter-design.md). Each record
states the context, the alternatives that were on the table, the decision and why, and what follows from it.
Where a record deviates from the design document, it says so explicitly.

Status legend: **Accepted** — in force.

| # | Decision |
|---|---|
| [ADR-001](#adr-001-target-net-10-c-14-ef-core-10) | Target .NET 10, C# 14, EF Core 10 |
| [ADR-002](#adr-002-project-per-ring-folder-per-feature) | Project per ring, folder per feature (with a documented split trigger) |
| [ADR-003](#adr-003-hand-rolled-ddd-building-blocks-not-nmolecules) | Hand-rolled DDD building blocks, not nMolecules |
| [ADR-004](#adr-004-identifiers-and-single-value-value-objects-with-plain-net) | Identifiers and single-value value objects with plain .NET |
| [ADR-005](#adr-005-postgresql-only-no-sqlite-no-in-memory-provider) | PostgreSQL only; no SQLite, no in-memory provider |
| [ADR-006](#adr-006-persistence-mapping-conventions) | Persistence mapping conventions (enums, snake_case, complex types, xmin) |
| [ADR-007](#adr-007-sql-first-migrations-with-schema-validation-against-the-ef-model) | SQL-first migrations with schema validation against the EF model |
| [ADR-008](#adr-008-hand-rolled-transactional-outbox-behind-an-explicit-unit-of-work) | Hand-rolled transactional outbox behind an explicit unit of work |
| [ADR-009](#adr-009-minimal-apis-with-explicit-assembly-scanned-endpoint-modules) | Minimal APIs with explicit, assembly-scanned endpoint modules |
| [ADR-010](#adr-010-hal-links-derived-from-endpoint-metadata) | HAL links derived from endpoint metadata |
| [ADR-011](#adr-011-roslyn-analyzer-ddd0001-the-command-is-the-first-parameter) | Roslyn analyzer DDD0001: the command is the first parameter |
| [ADR-012](#adr-012-authentication-jwt-bearer-in-production-header-scheme-for-development-and-tests) | Authentication: JWT bearer in production, header scheme for development and tests |
| [ADR-013](#adr-013-validation-with-microsoftextensionsvalidation-on-domain-commands) | Validation with Microsoft.Extensions.Validation on domain commands |
| [ADR-014](#adr-014-test-stack-and-where-integration-tests-run) | Test stack (xUnit assertions only), and where integration tests run |
| [ADR-015](#adr-015-naming-feature-namespace--aggregate-name-aliases-elsewhere) | Naming: feature namespace = aggregate name, aliases elsewhere |
| [ADR-016](#adr-016-dependency-policy-enforced-by-the-build) | Dependency policy enforced by the build |
| [ADR-017](#adr-017-scaffolding-with-dotnet-new-templates-and-no-injection) | Scaffolding with `dotnet new` templates and no injection |
| [ADR-018](#adr-018-the-starter-is-a-kernel-not-a-menu) | The starter is a kernel, not a menu |
| [ADR-019](#adr-019-the-application-ring-may-log) | The application ring may log |
| [ADR-020](#adr-020-the-gates-do-not-know-the-names-of-the-sample-modules) | The gates do not know the names of the sample modules |

---

## ADR-001: Target .NET 10, C# 14, EF Core 10

**Status:** Accepted (confirms design §1).

**Context.** .NET 8 and .NET 9 both leave support on 10 November 2026. The starter's first application ships after
that date. EF Core 10 is the first release where complex types cover what the design needs (optional complex
types, value semantics).

**Alternatives considered.**
- *.NET 8 LTS* — widest current install base, but out of support before the first deployment and without
  optional complex types (owned types would have been needed, with reference semantics).
- *.NET 9* — STS, same end-of-support date as .NET 8, no advantage.

**Decision.** `net10.0`, `LangVersion 14`, EF Core 10.0.x, with `global.json` pinned to the 10.0.200 feature band
(`rollForward: latestFeature`). The machine that built this has SDK 10.0.201 (Windows) and 10.0.401 (WSL); both
satisfy the pin.

**Consequences.** Some packages are still catching up with .NET 10 (`Microsoft.Extensions.Validation` is now part
of the shared framework and must *not* be referenced as a package — NU1510 fails the build).

---

## ADR-002: Project per ring, folder per feature

**Status:** Accepted (design §3, option A).

**Context.** The rings of the onion (domain, application, infrastructure, HTTP surface, host) can be enforced by
the compiler through project references. Feature modules (Sample, Person, ReferenceData) can either be folders
inside `App.Domain` (test-enforced boundaries, as in Spring Modulith) or projects of their own (compiler-enforced,
`internal` gives real module privacy).

**Alternatives considered.**
- *A. Folder per feature* — one domain project, cheap, familiar, boundaries checked by ArchUnitNET.
- *B. Project per feature* — `App.Sample.Domain`, `App.Person.Domain`, …; compiler-enforced, but more build time
  and solution noise, and it only pays off past roughly five modules.
- *Hybrid* — project per feature only for the domain ring. Rejected: the API and infrastructure rings would still
  be shared, so the "severable module" property would be an illusion.

**Decision.** Option A, with the naming and dependency discipline of option B so that the split is a move, not a
rewrite: every feature lives in exactly one folder per ring (`src/App.Domain/Sample`, `src/App.Infrastructure/
Persistence/Sample`, `src/App.Api/Sample`), cross-module references go through `Association<,>` only, and
`ModuleRules` in `App.ArchitectureTests` forbids the dependency directions that a project split would make
impossible anyway.

**Split trigger (documented as required by the design).** Move to option B when any of these holds: more than
five feature modules; two teams own different modules; a module needs its own release cadence or its own
database schema (Wolverine's modular-monolith guidance); or a `ModuleRules` test has been suppressed rather than
fixed twice.

**Consequences.** Module boundaries are as strong as the architecture tests. Repository and endpoint
implementations are `internal` already, so a split changes namespaces and project files, not code.

---

## ADR-003: Hand-rolled DDD building blocks, not nMolecules

**Status:** Accepted (design §4, §9).

**Context.** The Java starter leans on jMolecules for `AggregateRoot<T,ID>`, `Identifier`, `Association<T,ID>`,
JPA integration, Jackson integration and ArchUnit rule sets. nMolecules ships attributes only (last release
December 2022), has no `Association<,>`, no onion vocabulary, and no persistence or serialisation integration.

**Alternatives considered.**
- *nMolecules.DDD attributes* as the foundation — no type-level information (an attribute cannot carry the ID
  type), dormant, no integrations.
- *nMolecules attributes as decoration on top of our interfaces* — for cross-language recognisability. Rejected
  for now: one more dependency to explain to a regulated client for no build-time benefit. Can be added later
  without touching behaviour.
- *A third-party DDD base library* — none with meaningful adoption and a permissive licence.

**Decision.** `App.Domain/Common/Ddd.cs` and `AggregateRoot.cs`: `IIdentifier`, `IEntity<TId>`,
`IAggregateRoot<TSelf, TId>`, `IDomainEvent`, `ICommand`, `Association<TAggregate, TId>`, `IHasDomainEvents`
(explicitly implemented so it does not clutter the aggregate's surface) and an `AggregateRoot<TSelf, TId>` base
class with identity equality and the registered-events list. Roughly 100 lines, zero dependencies.

**Consequences.** The Roslyn analyzers (ADR-011, DDD0002 in ADR-004), the EF conventions (ADR-006) and the
architecture tests all key on these interfaces by metadata name. Renaming them is a coordinated change.

---

## ADR-004: Identifiers and single-value value objects with plain .NET

**Status:** Accepted. **Deviates from design §4.1**, which proposed the Vogen source generator.

**Context.** Strongly-typed identifiers need equality, JSON as a bare value, an EF `ValueConverter`, route
binding from a string, and a guard against `new SampleId()` / `default`. The design proposed Vogen for all of that.
Value objects are, however, exactly the kind of thing C# 12+ models well on its own (`readonly record struct`,
static abstract interface members, `IParsable<T>`), and a generator is one more thing a reviewer has to understand.

**Alternatives considered.**
- *Vogen* (Apache-2.0) — smallest per-type code, compile-time guards (VOG009/VOG010), EF/JSON converters
  generated. Costs: a generator and its analyzers in the build, the `Vogen.SharedTypes` runtime assembly in the
  domain, `[EfCoreConverter<T>]` registrations in infrastructure, and concepts (`StaticAbstractsGeneration`,
  `IVogen<,>`) that are Vogen's rather than .NET's.
- *StronglyTypedId* — same trade-off with fewer features.
- *Plain `readonly record struct` per identifier plus two small shared contracts* — chosen.

**Decision.**
- `IValueObject<TSelf, TPrimitive>` (`Value` + static abstract `From`) and
  `IIdentifier<TSelf, TPrimitive> : IValueObject<…>, IParsable<TSelf>` in `App.Domain.Common`.
- Each identifier is a `readonly record struct` with a **private constructor**, `New()` (UUID v7), `From(Guid)`
  (rejects `Guid.Empty` with `ValueObjectValidationException`), two one-line `Parse`/`TryParse` forwarders to
  `Identifier.Parse<,>`/`TryParse<,>`, and `[JsonConverter(typeof(SingleValueJsonConverter<TSelf, Guid>))]`. About
  twenty lines; the template pack generates them.
- One generic JSON converter (`SingleValueJsonConverter<,>`, domain) and one generic EF converter
  (`SingleValueConverter<,>`, infrastructure) serve every implementation. `RegisterDomainValueObjects()` scans the
  domain assembly at model-building time, so a new identifier needs **no** persistence registration. Associations
  get the same treatment (`AssociationConverter<,,>`).
- Multi-value value objects (`EmailAddress`, `I18nText`, `City`) are sealed records with a private constructor
  and a `From`/`TryFrom` factory where validation is needed.
- The compile-time guard a generator would provide comes from the in-repo analyzer **DDD0002**: `new TId()` and
  `default(TId)` are errors (`Nullable<TId>` defaults are allowed). Verified on a probe.

**Consequences.** Twenty lines of ceremony per identifier instead of three; in exchange nothing is generated and
the whole mechanism is readable in two files. `EmailAddress` is a class, so `default` is `null` and the nullable
analysis covers it. Should identifiers over `int`/`string` be needed, the contracts already support any
`IParsable` primitive.

---

## ADR-005: PostgreSQL only; no SQLite, no in-memory provider

**Status:** Accepted. **Deviates from design §1**, which proposed SQLite for the fast inner loop.

**Context.** The design already bans the EF in-memory provider (not relational, hides mapping bugs). SQLite was
meant as a fast local database. Two things make that unattractive here: optimistic concurrency uses PostgreSQL's
`xmin` system column, and the outbox dispatcher relies on `FOR UPDATE SKIP LOCKED` and `jsonb` (ADR-008), neither
of which SQLite has. A SQLite path would therefore be a second, weaker configuration that the outbox tests could
not exercise.

**Alternatives considered.**
- *SQLite for `dotnet run`, PostgreSQL for tests* — two schemas, two concurrency strategies, no outbox locally.
- *In-memory provider* — rejected by the design.
- *PostgreSQL everywhere* — one schema, one migration path, Testcontainers for tests, `compose.yaml` for local runs.

**Decision.** PostgreSQL everywhere. `compose.yaml` starts a local instance matching `appsettings.Development.json`.
Fast tests are the domain tests (no database) and the model-level tests (`App.Infrastructure.Tests`, which build
the EF model with the real provider but never open a connection).

**Consequences.** A container runtime is required for the integration suite. On this machine that is Docker
Engine inside WSL 2, see ADR-014.

---

## ADR-006: Persistence mapping conventions

**Status:** Accepted (design §5.2, §5.3, §4.2). Resolves the three ⚠ items the design asked to verify on a spike.

**Context.** The Java starter needs a naming strategy class, a `TypeContributor` for enums, and byte-buddy to keep
the model annotation-free. EF Core's fluent API and pre-convention configuration make all three one-liners, but the
design flagged three claims to confirm.

**Decisions and what the spike showed** (pinned by `MappingConventionTests`):

| Convention | Mechanism | Verified |
|---|---|---|
| Enums stored as their name | `configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50)` | Yes — `Properties<Enum>()` covers every enum property; the column is `character varying(50)`. No fallback convention needed. |
| Single-value VOs collapse to one column | `SingleValueConverter<,>` registered for every `IValueObject<,>` by `RegisterDomainValueObjects()` | Yes — `id uuid`, `email character varying(320)`. |
| Multi-field VOs are complex types, prefixed with the owning property | `ComplexProperty(x => x.Name)` + `UseSnakeCaseNamingConvention()` | Yes — `name_en`, `name_de`; nested: `city_name_en`. |
| Optional VO (`City?`) | `ComplexProperty(x => x.City, c => c.IsRequired(false))` | Yes, **with a caveat**: EF cannot constructor-bind a *nested* complex property, so a record VO that contains another VO needs a private parameterless constructor (`private City() : this(0, null!)`). `I18nText` (scalars only) needs nothing. |
| Optimistic concurrency without a version property | `Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()` applied to every `IAggregateRoot<,>` in `OnModelCreating` | Yes. Reference data (`City`) is not an aggregate and gets no token. |
| Cross-module reference stored as the target id | `AssociationConverter<TAggregate, TId, TPrimitive>` registered for every aggregate by `RegisterDomainValueObjects()` | Yes — `owner uuid`, indexed. |
| Get-only properties | must be configured explicitly (`Property(x => x.CreatedAt)`); EF does not discover them by convention | Yes. |

**Alternatives considered.** Owned entity types instead of complex types (reference semantics, superseded in EF
10); explicit `HasColumnName` everywhere instead of `EFCore.NamingConventions` (more code, same result);
a `rowversion`-style column instead of `xmin` (an extra column on every table).

**Consequences.** `System.ComponentModel.DataAnnotations.Schema` and EF attributes are banned in the domain by
`BannedSymbols.Domain.txt`; the mapping lives in `IEntityTypeConfiguration<T>` classes applied by
`ApplyConfigurationsFromAssembly`, so adding an aggregate never touches `AppDbContext`. The physical schema is
written by hand (ADR-007) and validated against this model by `SchemaValidationTests`.

---

## ADR-007: SQL-first migrations with schema validation against the EF model

**Status:** Accepted. **Deviates from design §5.4**, which recommended EF Core migrations with
`HasPendingModelChanges()` as the drift gate and named SQL-first as the fork point.

**Context.** The Java starter uses Flyway scripts plus `ddl-auto: validate`: the team writes the SQL, the mapping
is checked against it. EF code-first migrations generate the SQL from the model, which is convenient until it is
not: the team does not fully control the schema, and the generator's behaviour in non-trivial cases (renames, data
migrations, index changes, type widening) is opaque and has to be second-guessed in review. For the clients this
starter targets, reviewable DDL is frequently a contractual expectation.

**Alternatives considered.**
- *EF Core migrations* — automatic generation and drift detection; schema control and reviewability are weak
  exactly where migrations get interesting.
- *DbUp / FluentMigrator* (MIT) — mature runners for hand-written SQL. Would be the choice if the runner needed
  more than sequential scripts with checksums and a lock; here that is 100 lines.
- *Hand-written SQL scripts, an in-repo runner, and a validation test that diffs the real database against the EF
  model* — chosen.

**Decision.**
- Scripts live in `src/App.Infrastructure/Migrations/V0001__initial_schema.sql`, `V0002__seed_reference_data.sql`,
  … as embedded resources. `SqlMigrator` applies unapplied scripts in name order, each in its own transaction,
  records version and SHA-256 checksum in `schema_version`, refuses to run if an applied script was edited, and
  serialises concurrent starters with a PostgreSQL advisory lock. Reference data is seeded by script too.
- Deployments run the host once with `--migrate` (applies and exits); `Database:MigrateOnStartup=true` is for the
  inner loop and the test host only.
- **The `ddl-auto: validate` analogue:** `SchemaValidationTests` (integration suite) reads `information_schema` /
  `pg_indexes` and asserts that every table, column (store type and nullability), primary key and index that the EF
  relational model expects exists in the migrated database. Extra database objects are allowed; a missing or
  differently typed one fails with a precise message. This is the safety net that makes SQL-first cheap: the model
  is still the single description of the mapping, and the scripts are checked against it on every run.
- EF Core remains the ORM and the source of the mapping (`IEntityTypeConfiguration<T>`); only schema *generation*
  is out of scope. No `Microsoft.EntityFrameworkCore.Design`, no design-time factory, no `dotnet ef`.

**Consequences.** Adding an aggregate means writing a `CREATE TABLE` by hand (the template generates a starting
point); the schema is exactly what was reviewed. Drift detection needs a database and therefore lives in the
integration suite rather than the fast tests. `SqlMigratorTests` checks script naming and ordering without a
database.

---

## ADR-008: Hand-rolled transactional outbox behind an explicit unit of work

**Status:** Accepted. **Deviates from design §7**, which proposed Wolverine.

**Context.** Cross-module synchronisation needs the Spring Modulith event-publication-registry behaviour: events
stored atomically with the aggregate, delivered asynchronously, retried with cooldown, redelivered after a crash,
consumed idempotently. That is a *pattern*, and in a modular monolith whose handlers run in-process it is a small
one. The library that implements it is interchangeable; the discipline it enforces is not.

**Alternatives considered.**
- *Wolverine* (MIT) — full-featured and the design's recommendation. It brings external transports, sagas,
  scheduling and a code-generation model the project would not use, and three frictions for this codebase:
  Wolverine 6 needs `WolverineFx.RuntimeCompilation` (or pre-generated code) to run, its generated handler code
  cannot construct `internal` dependencies without `ServiceLocationPolicy.AlwaysAllowed`, and it manages its own
  database schema outside the project's migrations. Plus a single-vendor concentration risk (design §16.3).
- *Rebus / other buses* — same category, same overhead for in-process delivery.
- *Hand-rolled outbox + inbox on the application database, dispatched by a `BackgroundService`* — chosen.

**Decision** (`src/App.Infrastructure/Messaging`, ~250 lines):
- `IUnitOfWork.CommitAsync()` (domain) is implemented by `EfUnitOfWork`: it turns the events registered on tracked
  aggregates into `outbox_messages` rows (type name + JSON payload) and saves them in the **same `SaveChanges`
  transaction** as the aggregates, then signals the dispatcher.
- `OutboxDispatcher` claims a batch with `UPDATE … WHERE id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING` (safe
  with several replicas), resolves the event type through `DomainEventTypes` (never `Type.GetType` on data), and
  invokes every `IEventHandler<TEvent>` **in its own scope and database transaction**. The `inbox_messages` row
  (message, handler) is written in that transaction, so a redelivered message is skipped per handler that already
  succeeded — idempotent consumption without asking handlers to be clever. Failures schedule a retry with cooldown
  (`OutboxOptions.Backoff`); after `MaxAttempts` the message is dead-lettered (`failed_at`) and logged as an error.
- Handlers are plain classes in `App.Application` implementing `IEventHandler<TEvent>`, discovered by assembly
  scan in `AddMessaging()`. They call `IUnitOfWork.CommitAsync()` like endpoints do; cascading events are outboxed
  in the same transaction.
- Tests wait with `AppFactory.WaitForOutboxAsync()` (the Modulith `Scenario` role): drained, or fail with the
  dead-letter error.

**What this does not do, on purpose:** external transports, sagas, scheduled messages, competing consumers across
processes. If messages must leave the process, put a bus *behind* `IEventHandler<T>` or replace `OutboxDispatcher`;
the domain, application and API rings do not change (the architecture tests forbid them from seeing
`App.Infrastructure.Messaging`).

**Consequences.** Zero messaging dependencies; the migration owns the outbox tables; everything is debuggable with
a breakpoint. The team owns retry and dead-letter handling — both are small and covered by options, but they are
ours.

---

## ADR-009: Minimal APIs with explicit, assembly-scanned endpoint modules

**Status:** Accepted (design §6.1, §6.2).

**Context.** There is no maintained, production-grade Spring Data REST equivalent for .NET. Endpoints must be
written. The design asked to consider `WolverineFx.Http` as a second endpoint style.

**Alternatives considered.**
- *MVC controllers* — `Microsoft.Extensions.Validation` does not support MVC; endpoint metadata is less direct.
- *OData / JSON:API libraries* — a different, heavier contract imposed on clients.
- *WolverineFx.Http* — handler-per-operation with transactional middleware; would concentrate more of the stack on
  one vendor and hide the unit of work that ADR-008 makes explicit. Deferred; the endpoint modules are small enough
  (the Sample module is ~150 lines including representations) that the design's threshold is not exceeded.
- *Minimal APIs with one `IEndpointModule` per feature, discovered by assembly scan* — chosen.

**Decision.** `App.Api` holds one `internal sealed class XEndpoints : IEndpointModule` per feature.
`ApiExtensions.MapApi()` instantiates every module in the assembly, so adding a feature requires no registration.
Reads may use `AppDbContext` directly (`AsNoTracking`); state changes load the aggregate through its repository,
call a command method and commit through `IUnitOfWork`. `RingRules` forbids any `SaveChanges*` call from the API
assembly. Response shapes are explicit records (`SampleSummary`, `SampleDetail`), not a `?projection=` parameter.
Domain exceptions become RFC 9457 ProblemDetails through `DomainExceptionHandler` (404 not found, 409 state or
concurrency conflict, 422 rule violation).

**Consequences.** More code per aggregate than Spring Data REST; the scaffolding (ADR-017) generates it. There are
no generic PUT/PATCH/DELETE endpoints to disable because they never exist.

---

## ADR-010: HAL links derived from endpoint metadata

**Status:** Accepted (design §6.4, §16.1).

**Context.** A command link must appear only when the aggregate's state permits the command **and** the caller may
invoke the endpoint. The Java starter reflects over `@Secured`. ASP.NET Core stores authorisation as endpoint
metadata that the framework itself enforces.

**Alternatives considered.**
- *Reflect over handler methods for `[Authorize]`* — misses policies added by `RequireAuthorization()` on the route.
- *A vendor media type with a `commands: []` array instead of HAL* — simpler for a same-team SPA. The
  capability-discovery idea is what matters; HAL is one encoding. The abstraction is confined to `Hal.cs` and
  `HalLinks.cs` so the encoding can be swapped.
- *Read `IAuthorizeData` from `EndpointDataSource` and evaluate it with `IAuthorizationService`* — chosen.

**Decision.** `HalLinks.ForAsync(http, selfEndpoint, routeValues, commandEndpoints, aggregate.Can)`: `self` plus
one link per `CommandEndpoint` whose command type passes `Can(...)` and whose endpoint the current user is
authorised for. `EndpointAuthorization` resolves the endpoint by name, combines its policies via
`AuthorizationPolicy.CombineAsync`, and caches the decision per request and endpoint name, so a collection costs
one evaluation per distinct policy. Hrefs are paths (`LinkGenerator.GetPathByName`), not absolute URIs, so they
survive reverse proxies without forwarded-header configuration. Commands without an endpoint
(`SampleCommand.UpdateOwnerName`) are simply absent from the list and never become links.

**Startup validation.** `StateChangingEndpointsRequireAuthorization` (an `IHostedService`) walks the routing table
and throws if any POST/PUT/PATCH/DELETE endpoint under `/api` lacks authorisation metadata. It fails the host, and
therefore the integration suite, before the first request.

**Consequences.** The link contract per state × role is snapshot-tested (`ContractTests.Hal_link_contract_snapshot`)
so it cannot regress silently.

---

## ADR-011: Roslyn analyzer DDD0001: the command is the first parameter

**Status:** Accepted. **Deviates from design §8**, which said "exactly one parameter".

**Context.** The Java starter's `aggregateOperationsTakeCommands` rule forces every state-changing operation to be
expressed as a command record, which is what makes the command hierarchy a complete description of the aggregate's
API and what HAL link generation keys on. As a Roslyn analyzer it fails in the IDE as you type.

**Alternatives considered.**
- *Exactly one parameter, implementing `ICommand`* — as designed. Breaks the design's own reference-data pattern:
  `Sample.Update(SampleCommand.Update data, ReferenceData.City? city)` needs the resolved city, because the command
  carries a `CityId` and the aggregate copies fields (design §4.4).
- *Exactly one parameter, with the command carrying resolved entities* — leaks aggregates/entities into command
  records that are also the HTTP request body.
- *ArchUnitNET test instead of an analyzer* — later feedback (test time instead of edit time).
- *First parameter implements `ICommand`; further parameters allowed for resolved dependencies* — chosen.

**Decision.** `App.Analyzers/AggregateOperationsTakeCommandsAnalyzer` reports **DDD0001 (error)** for every public,
non-static, non-override method returning `void`, `Task` or `ValueTask` on a class implementing `IAggregateRoot<,>`
whose first parameter does not implement `ICommand`. Queries (non-void, e.g. `Can(...)`) and static factories are
exempt. The analyzer is a project reference with `OutputItemType="Analyzer"` on `App.Domain`, ships with release
tracking files, and was verified to fire on a probe type.

**Consequences.** No code fix is shipped yet (the design suggested one that generates the command record); it is a
follow-up. Everything the analyzer needs is the metadata name of two interfaces, so it survives moving the domain
into per-feature projects (ADR-002).

---

## ADR-012: Authentication: JWT bearer in production, header scheme for development and tests

**Status:** Accepted.

**Context.** The starter must run locally and in tests without an identity provider, but must not ship a
backdoor. Policies (`User`, `Admin`) are role-based and drive both authorisation and link visibility.

**Alternatives considered.**
- *JWT only, with a local Keycloak/Duende in `compose.yaml`* — heavy for a starter; tests would depend on a token
  service.
- *A test-only authentication handler injected by `WebApplicationFactory`* — covers tests but not `dotnet run`.
- *A configurable scheme: `Jwt` (production) or `DevelopmentHeaders` (`X-User`, `X-Roles`), the latter refused by
  options validation outside the Development and Testing environments* — chosen.

**Decision.** `AuthenticationOptions` bound from `Authentication`, validated with `ValidateOnStart()`: Jwt mode
requires an authority; DevelopmentHeaders mode is rejected unless the environment is Development or Testing.
`appsettings.json` defaults to Jwt; `appsettings.Development.json` and the integration test factory select
DevelopmentHeaders.

**Consequences.** Production configuration cannot accidentally enable the header scheme — the host refuses to
start. Role claims are `roles` in JWT mode and `X-Roles` in header mode; both map to `ClaimTypes.Role`.

---

## ADR-013: Validation with Microsoft.Extensions.Validation on domain commands

**Status:** Accepted (design §6.5).

**Context.** The Java starter validates `@Valid @RequestBody` commands with Bean Validation. In .NET 10 the
equivalent is `AddValidation()` (source-generated, minimal APIs only), which returns RFC 9457 validation problems
before the handler runs.

**Alternatives considered.**
- *FluentValidation* — a second validation DSL, more packages, no source generation.
- *Validation inside aggregate methods only* — semantic rules belong there, but shape validation (required,
  max length) would reach the aggregate as exceptions and produce 4xx responses by exception mapping.
- *DataAnnotations on the command records in the domain, `AddValidation()` in the API project* — chosen.

**Decision.** `System.ComponentModel.DataAnnotations` attributes (`[Required]`, `[MaxLength]`) are allowed on
command records and `I18nText`; only the `.Schema` namespace is banned in the domain. `AddValidation()` is called
from `ApiExtensions.AddApi()` — in the same compilation as the `Map*` calls, because the generator discovers
validatable types from the endpoints it can see. Nested records (`I18nText` inside `SampleCommand.Create`) are
validated recursively; the integration test asserts a 400 with `errors` keys for both a top-level and a nested
property.

**Consequences.** Semantic rules (state machine, invariants) still live in the aggregate and surface as 409/422
through `DomainExceptionHandler`. Value objects validate in their `From` factory and surface as 422.

---

## ADR-014: Test stack, and where integration tests run

**Status:** Accepted (design §1, §15).

**Context.** FluentAssertions 8 is non-commercial-only; the design proposed Shouldly instead. xUnit's own
`Assert` covers what these tests need, and a starter should not teach a second assertion vocabulary. Integration
tests need a real PostgreSQL. The development machine has no Docker on Windows, but a Docker Engine (29.x) inside WSL 2 Ubuntu,
and `sudo` in WSL requires a password, so the daemon cannot be exposed on TCP by an automated step; exposing a
local service was also declined by the session's permission policy.

**Alternatives considered.**
- *Docker Desktop* — licence review required above the free-use threshold; not installable silently.
- *Expose the WSL daemon on `tcp://127.0.0.1:2375`* (systemd drop-in or a user-space relay) and point
  Testcontainers at it from Windows — the most ergonomic long-term setup; needs one `sudo` step by the developer.
- *Run the integration suite inside WSL with a user-space .NET SDK (`dotnet-install.sh`)* — no privileges, Unix
  socket auto-detected by Testcontainers, identical to Linux CI — chosen for now.
- *Shouldly / FluentAssertions 7 / plain `Assert`* for assertions — plain `Assert` chosen (see decision).
- *Verify* for the golden-master test — a 30-line helper chosen; one snapshot file does not justify a library.

**Decision.**
- xUnit v3 (VSTest runner mode) with **plain `Assert`** — no assertion DSL. Testcontainers.PostgreSql
  (`postgres:17-alpine`), `WebApplicationFactory` over the real `Program.cs`,
  `Microsoft.Extensions.TimeProvider.Testing` for deterministic time. One container and one host per collection.
- The one golden-master test (HAL link contract per state × role, design §10.8) uses a 30-line `Snapshot.Match`
  helper: compare against `Snapshots/HalLinkContract.json`, write a `.received.json` on mismatch,
  `UPDATE_SNAPSHOTS=1` to rewrite.
- Four test projects, cheapest first: `App.Domain.Tests` (no host, no DB), `App.Infrastructure.Tests` (EF model
  and migration-script checks, no DB), `App.ArchitectureTests` (ArchUnitNET + reflection), `App.IntegrationTests`
  (Docker; includes `SchemaValidationTests`, the outbox delivery test and the HAL snapshot).
  `EndpointSecurityCheckTests` in the integration project needs no Docker either.
- On this machine the integration suite runs from WSL:
  `wsl -e bash -lc 'cd /mnt/c/…/dotnet-ddd-starter && ~/.dotnet/dotnet test tests/App.IntegrationTests -p:ArtifactsPath=$HOME/artifacts/ddd'`
  (`ArtifactsPath` keeps Linux build output away from the Windows `bin/obj`). A standalone Docker CLI
  (`Docker.DockerCLI` via winget) is installed on Windows; it becomes useful once the daemon is reachable over TCP.

**Consequences.** `xUnit1051` (forward `TestContext.Current.CancellationToken`) is disabled for test projects: it
adds noise to every `HttpClient` call and no safety in this suite. Changes to the link contract must be accepted
deliberately by replacing the snapshot file.

---

## ADR-015: Naming: feature namespace = aggregate name, aliases elsewhere

**Status:** Accepted.

**Context.** Following the design's layout, the `Sample` aggregate lives in namespace `App.Domain.Sample` — a type
and a namespace with the same simple name (CA1724). Any other namespace that also has a segment called `Sample`
(for example `App.Infrastructure.Persistence.Sample`) makes the bare identifier `Sample` resolve to the namespace,
and `App.Domain.Sample.City` vs `App.Domain.ReferenceData.City` is ambiguous wherever both are imported.

**Alternatives considered.**
- *Rename aggregates or namespaces* (`SampleAggregate`, `App.Domain.Samples`) — fights the ubiquitous language and
  the Java layout the starter mirrors.
- *Mirror feature namespaces in every ring* — guarantees the clash in infrastructure and API.
- *Keep the design's domain layout; keep infrastructure, API-common and test namespaces flat or feature-neutral;
  use aliases where two `City`s meet* — chosen.

**Decision.** Domain: `App.Domain.<Feature>` holds the aggregate of the same name. Infrastructure persistence and
test projects use flat namespaces (`App.Infrastructure.Persistence`, `App.Domain.Tests`) with feature *folders*
(IDE0130 is disabled). API modules use `App.Api.<Feature>` and refer to the aggregate as `Domain.Sample.Sample`.
Where both `City` types are needed, alias the reference-data one (`using RefCity = App.Domain.ReferenceData.City;`).
CA1724 is disabled solution-wide.

**A feature with more than one aggregate.** `App.Domain.<Feature>` is named after the *feature*, which for a
single-aggregate module happens to be the aggregate's own name. Where a module owns several — PlaneZ's `Fleet`
holds `Airplane` and `AirplaneType` — the feature keeps its own name and no namespace segment is added per
aggregate. The rule is "one namespace per feature", not "one per aggregate".

**Consequences.** Templates (ADR-017) follow the same rules so generated code compiles against the same analyzers.

---

## ADR-016: Dependency policy enforced by the build

**Status:** Accepted (design §14, `DEPENDENCIES.md`).

**Context.** MediatR, AutoMapper, MassTransit and FluentAssertions relicensed in 2025; none fails closed. Coding
agents reintroduce them by reflex. Regulated clients review the dependency register.

**Alternatives considered.** Policy as a document only (rediscovered per project); a CI-only licence scan
(late feedback); both plus a compile-time denylist — chosen.

**Decision.**
- Central Package Management (`Directory.Packages.props`), `NuGetAudit` at level `low` including transitive
  packages, lock files (`RestorePackagesWithLockFile`, `--locked-mode` when `CI=true`), package source mapping to
  nuget.org in `nuget.config`.
- `Directory.Build.targets` target `BanRestrictivelyLicensedPackages` (error D4S-0001) fails the build for
  MediatR, AutoMapper, MassTransit, FluentAssertions and their DI helper packages. Verified: the item condition with
  a property function evaluates correctly on this MSBuild.
- `nuget-license --include-transitive --allowed-license-types build/allowed-licenses.json` as the CI gate
  (`build/ci.sh` / `build/ci.ps1`); the tool is installed as a global tool here.
- `Microsoft.CodeAnalysis.BannedApiAnalyzers` with a common list (`DateTime.Now`, `Guid.NewGuid`,
  `Newtonsoft.Json`, …) and a stricter domain list (EF/ASP.NET namespaces, schema attributes, `Task.Run`).

**Correction to the design.** The `Microsoft.OpenApi` 2.3.9 pin is unnecessary: `Microsoft.AspNetCore.OpenApi`
10.0.12 constrains `Microsoft.OpenApi` to `[2.12.0, 3.0.0)`, so the breaking 3.x line cannot be pulled in
transitively. Not pinning avoids a version conflict with that range.

**Consequences.** Adding a package is a three-step ritual (register in `DEPENDENCIES.md`, version in
`Directory.Packages.props`, reference) and the lock files change with it.

---

## ADR-017: Scaffolding with `dotnet new` templates and no injection

**Status:** Accepted (design §12).

**Context.** The Java starter's Hygen generators create files *and* inject into existing ones (DbContext, endpoint
registration). `dotnet new` templates create files but cannot inject.

**Alternatives considered.**
- *Keep Hygen* — a Node dependency in a .NET repository; hard to sell in locked-down environments.
- *A custom `dotnet tool` that injects* — most work, most to maintain.
- *Design the code so no injection is needed, and accept the residue as a printed post-action* — chosen.

**Decision.** The codebase removes injection points: `ApplyConfigurationsFromAssembly` for EF configurations,
assembly scanning for `IEndpointModule`, assembly scanning for `IEventHandler<T>` and for value-object converters, and
embedded `Migrations/*.sql` scripts picked up by a glob.
The template pack (`templates/`, package id `D4S.Ddd.Templates`) provides `ddd-aggregate` and `ddd-refdata`; the
design's `ddd-feature` and `ddd-endpoints` are folded into `ddd-aggregate` because a feature without an aggregate
has nothing to generate and endpoints without an aggregate have nothing to link. Two manual steps remain and are printed by the
template: the repository `AddScoped` line, and choosing the version number of the generated SQL migration script
(ADR-007). Identifier converters are discovered (ADR-004), so they need no step. `build/regen-check.ps1|.sh` generates a throwaway feature into a temporary copy, applies
those steps, builds with warnings as errors, runs the fast test projects and discards the copy — the `-Phygen-it`
job of the Java starter, and the guardrail that keeps templates and hand-written code from drifting.

**Details fixed while building the templates.**
- The post-action that prints the manual steps is `InstructionDisplayPostActionProcessor`
  (`AC1156F7-BB77-4DB8-B28F-24EEBCCA1E5C`); the "run script" processor hangs `dotnet new` when used by mistake.
  Manual-instruction text is not symbol-substituted, so it is written for the default name.
- Pluralisation is a template symbol (`y` → `ies`, else `s`) with a `--plural` override, applied to type names,
  file names (`ICities.cs`), route segments, HAL rels and table names — `name + "s"` would have produced
  `/api/countrys`.
- Generated repositories use `db.Set<T>()` instead of a `DbSet<T>` property on `AppDbContext`, which would have
  been a fourth injection point. `ApplyConfigurationsFromAssembly` and the `xmin` convention pick the entity up.
- Both templates generate a `CREATE TABLE` script with a `--migration-version` parameter (default `V0003`); since
  the version cannot be derived, the regeneration check passes `V0004` for the second template and the printed
  instructions tell the developer to pick the next free number.

**Consequences.** The two manual steps are the honest price of the platform; a future `dotnet tool` could
automate them if they prove error-prone. The regeneration check is the last gate in `build/ci.sh` / `build/ci.ps1`.

---

## ADR-018: The starter is a kernel, not a menu

**Status:** Accepted.

**Context.** A starter can be a *menu* — ship every reasonable option and let each project prune — or a *kernel* —
ship the smallest set that enforces the architecture and document how to extend it. The distinction decides what
every downstream project inherits, and what coding agents reproduce: agents copy whatever is present.

**Alternatives considered.**
- *Menu* — attractive for demos; in practice nothing gets deleted, every project inherits the whole dependency
  surface, and each library's concepts leak into the code that agents and juniors learn from.
- *Kernel* — prefer the platform's own means wherever they suffice, hand-roll the small patterns, and name the
  upgrade path for each thing deliberately left out — chosen.

**Decision.** Where a common library was on the table, the choice went as follows:

| Concern | Kernel choice | Library kept as the upgrade path | Record |
|---|---|---|---|
| Events between modules | Hand-rolled outbox/inbox + `BackgroundService` | Wolverine (MIT) when messages must leave the process | ADR-008 |
| Schema evolution | SQL scripts + 100-line runner + schema validation test | DbUp (MIT) if the runner needs more | ADR-007 |
| Identifiers / value objects | `readonly record struct` + two contracts + analyzer DDD0002 | Vogen (Apache-2.0) if per-type boilerplate becomes a burden | ADR-004 |
| Assertions | xUnit `Assert` | — (FluentAssertions ≥ 8 is prohibited; Shouldly would be permissible but unnecessary) | ADR-014 |
| Golden-master tests | 30-line `Snapshot.Match` | Verify (MIT) if approval tests multiply | ADR-014 |
| In-process dispatch / mapping | none: endpoints call repositories and aggregate methods; `Select` into records | `martinothamar/Mediator`, Mapperly — only with an ADR | ADR-009 |
| API documentation | `Microsoft.AspNetCore.OpenApi` + Scalar UI; the document is the contract | Refit (MIT) for a typed client, only if a .NET frontend shares the repository | ADR-009 |

**Kept despite being "a library":** EF Core + Npgsql (the ORM), `EFCore.NamingConventions` (one convention,
replaceable by `HasColumnName` calls), ArchUnitNET (the enforcement layer), Testcontainers (a real database in
tests is non-negotiable), `Microsoft.CodeAnalysis.BannedApiAnalyzers` (build-time only), Scalar (UI only),
`Microsoft.Extensions.TimeProvider.Testing` and `Microsoft.AspNetCore.Authentication.JwtBearer` (Microsoft).

**Consequences.** Third-party runtime dependencies beyond Microsoft packages: the Npgsql EF provider and the
naming convention. Anything added later must argue against the kernel principle in an ADR, not just appear in
`Directory.Packages.props`. Adding a feature costs about twenty hand-written lines (identifier, `CREATE TABLE`)
that a generator would have produced; the templates generate both.

---

## ADR-019: The application ring may log

**Status:** Accepted (amends ADR-002).

**Context.** The rings are enforced by project references and by `RingRules`, which banned every
`Microsoft.Extensions.*` namespace from the domain **and** the application ring. Building the PlaneZ sample on
this starter showed what that costs. `BookingEventHandler` re-assigns or cancels a pilot's booking when an
airplane is grounded, and a fleet-wide telemetry round decides which airframes to read; both run unattended,
after a transaction they did not open, and both are the only place where the *outcome* of that decision exists.
Neither could say a word about it.

**Alternatives considered.**
- *Leave the rule* — pushes the logging into the outbox dispatcher, which knows the message but not the
  decision, or into the repository, which knows neither.
- *An own logging abstraction in the domain ring* — a second `ILogger` that every adapter has to bridge, with no
  benefit: `ILogger` already is the abstraction.
- *Allow `Microsoft.Extensions.Logging.Abstractions` in the application ring only* — chosen.

**Decision.** `App.Application` references `Microsoft.Extensions.Logging.Abstractions` and may inject
`ILogger<T>` (through `[LoggerMessage]` source-generated methods, as the rest of the repository does). The domain
ring stays plain C#: not even a logger — a model that wants to report something registers an event. `RingRules`
is split into `Domain_ring_is_free_of_frameworks` and
`Application_ring_is_free_of_frameworks_except_the_logging_abstractions`, so the narrower permission is visible
in the rule's name rather than buried in a regex.

**Consequences.** The application ring gains one Microsoft abstractions package; it pulls in no runtime
implementation, and `NullLogger<T>.Instance` keeps handler unit tests free of any hosting setup. Hosting,
persistence and web types stay banned there. Anything beyond logging needs its own ADR.

---

## ADR-020: The gates do not know the names of the sample modules

**Status:** Accepted (amends ADR-017).

**Context.** The first thing a project does with this starter is delete the shipped sample modules and put its
own there. The regeneration check and the CI scripts did not survive that: they anchored the generated
repository registration on `using App.Domain.Sample;` and on `services.AddScoped<ICities, Cities>();`, and they
named the solution file literally. Porting the PlaneZ domain onto the starter broke both — and a broken *check*
fails differently from broken code: it stops proving anything.

**Alternatives considered.**
- *Keep one sample module forever* — a permanent tax on every downstream repository, only so that a check keeps
  passing.
- *Let the scripts search for whatever registration happens to be there* — fragile in a new way; it guesses.
- *Declare an explicit extension point, and find the solution rather than name it* — chosen.

**Decision.** `InfrastructureServiceCollectionExtensions.AddPersistence` carries a
`// <ddd-scaffold:repositories>` marker; the scaffolding instructions and both regeneration checks anchor on it,
and on the last `using App.Domain.<something>;` line rather than on a particular module. `build/ci.sh`,
`build/ci.ps1` and both regeneration checks resolve the solution with a `*.slnx` glob and fail loudly when there
is none. Nothing under `build/` names a feature module.

**Consequences.** Renaming the solution, or deleting every module the starter shipped with, keeps the gates
working — which is the only state in which they are worth having. The marker comment is load-bearing: removing
it fails the regeneration check with "anchor not found", which is the intended diagnostic.
