# Instructions

How code is written in this repository. The build enforces most of it; this file explains the intent so that the
rules are followed where the build cannot see.

## 1. Modules

Three rules, stated verbatim from the Java starter because they transfer unchanged:

1. **Reference other modules by identity only.** An aggregate holds `Association<Person, PersonId>`, never a
   `Person`. Factories and command methods may *receive* a resolved aggregate or reference-data entity as an extra
   parameter; they may not store it.
2. **Denormalise what you need to read.** `Sample.OwnerName` is a copy. If a read model needs data from another
   module, copy it into the aggregate and keep it fresh with an event handler, or join explicitly on the read side.
3. **Synchronise through domain events.** Modules talk through events on the outbox, handled asynchronously in
   their own unit of work (`IEventHandler<TEvent>`, e.g. `SampleOwnerNameSynchronizer`). Handlers must be
   idempotent: the outbox redelivers, and the inbox only de-duplicates once a handler's transaction committed.

Modules live in `src/App.Domain/<Feature>` and have a folder in each outer ring. `ModuleRules` forbids cycles.
Reference data (`ReferenceData`) is copied *from*; it never depends on a feature module.

## 2. Aggregates

- Derive from `AggregateRoot<TSelf, TId>`. The id is a `readonly record struct` implementing
  `IIdentifier<TSelf, Guid>` with a private constructor, `New()` (UUID v7), `From(Guid)`, `Parse`/`TryParse`
  forwarders and `[JsonConverter(typeof(SingleValueJsonConverter<TSelf, Guid>))]` — copy `SampleId.cs`, or let
  the template generate it. `new TId()` and `default(TId)` are compile errors (`DDD0002`).
- **One command record per operation.** Commands are nested in a closed hierarchy
  (`public abstract record SampleCommand : ICommand { private SampleCommand() {} public sealed record Create(...) : SampleCommand; … }`).
  The command is the **first parameter** of the operation (analyzer `DDD0001`). Further parameters carry resolved
  dependencies only (reference data, another aggregate, `TimeProvider`).
- Operations return `void`. Guard them with `AssertCan<TCommand>()`, which throws `OperationNotAllowedException`
  (→ 409). Expose `bool Can<TCommand>()` and `bool Can(Type)`: they drive the HAL links.
- **Register events, never publish them**: `RegisterEvent(new SampleEvent.Published(Id))`. Events are nested
  sealed records in a closed hierarchy implementing `IDomainEvent`. The unit of work writes them to the outbox.
- No public setters. No EF attributes. No `DateTime.Now` — take a `TimeProvider`.
- EF needs a private materialisation constructor when the type has complex-type properties; mark it with the
  `#pragma warning disable CS8618` block used in `Sample.cs`. Get-only properties must be configured explicitly.
- Commands without an endpoint (e.g. `UpdateOwnerName`) are internal to the module and never become links.

## 3. Value objects

- Single value: a `sealed record` (or `readonly record struct` for identifiers) implementing
  `IValueObject<TSelf, TPrimitive>`: private constructor, `From` normalises and validates (throw
  `ValueObjectValidationException`, → 422), optional `TryFrom`, `[JsonConverter(typeof(SingleValueJsonConverter<…>))]`.
  Serialises as a bare value; EF converter registered automatically by `RegisterDomainValueObjects()`.
- Multiple fields: a `sealed record` mapped as a complex type (`ComplexProperty`). Columns are prefixed with the
  owning property (`city_postal_code`). Records nested in records need a private parameterless constructor.

## 4. Repositories

- Interface in the domain, plural of the aggregate, no suffix: `ISamples`, `IPeople`, `ICities`.
- `FindAsync` returns `null`; `GetRequiredAsync` throws `AggregateNotFoundException` (→ 404). Add finders as you
  need them (`FindByOwnerAsync`). **Never return `IQueryable`.**
- Implementation is `internal sealed` in `App.Infrastructure/Persistence/<Feature>`, registered in
  `InfrastructureServiceCollectionExtensions.AddPersistence`.
- `IUnitOfWork.CommitAsync()` once per request/handler. It saves and writes registered events to the outbox atomically.

## 5. Endpoints

- One `internal sealed class <Feature>Endpoints : IEndpointModule` per feature in `App.Api/<Feature>`; discovered
  automatically. Group under `/api/<plural>`, name every route (`.WithName(SampleRels.X)`).
- Reads: `AppDbContext` directly, `AsNoTracking`, projected into a response record. Writes: repository → command
  method → `IUnitOfWork.CommitAsync()`. Never call `SaveChanges` from the API (architecture test).
- **Every POST/PUT/PATCH/DELETE has a policy** (`.RequireAuthorization(Policies.User|Admin)`); the host refuses to
  start otherwise. Policies are the single source of truth for link visibility.
- Response records derive from `HalResource`; build links with
  `HalLinks.ForAsync(http, SelfEndpointName, new { id }, <Feature>Rels.CommandEndpoints, aggregate.Can)`. Add a
  `CommandEndpoint(typeof(Command), "rel", EndpointName)` for each command that has a route.
- Errors are RFC 9457 ProblemDetails: validation → 400, not found → 404, state/concurrency conflict → 409, rule or
  value violation → 422. Do not catch domain exceptions in endpoints.
- Validation attributes (`[Required]`, `[MaxLength]`) go on the command records; `AddValidation()` runs them before
  the handler.

## 6. Persistence and schema

- Mapping in `IEntityTypeConfiguration<T>` classes next to the repository; picked up by
  `ApplyConfigurationsFromAssembly`. Table names explicit (`ToTable("samples")`), everything else by convention.
- Conventions already in place: enums as strings, snake_case names, single-value converters, `xmin` concurrency
  token on every aggregate root, associations stored as the target id.
- **Schema is SQL-first.** Every mapping change ships with a script `src/App.Infrastructure/Migrations/
  V000N__what_changed.sql` (embedded automatically). Column types as PostgreSQL prints them
  (`character varying(200)`, `timestamp with time zone`, `uuid`), constraint and index names `pk_<table>`,
  `ix_<table>_<columns>`. Never edit an applied script; the runner checks checksums.
- `SchemaValidationTests` (integration) diffs the migrated database against the EF model; run it after any
  mapping or script change.

## 7. Event handlers

- A public class in `App.Application/<Feature>` implementing `IEventHandler<TEvent>`; discovered by assembly
  scan. Inject repositories and `IUnitOfWork`; call `CommitAsync` once.
- Idempotent by construction (set the same value twice = no-op). Each handler runs in its own scope and
  transaction; the inbox row is written in that transaction.
- Failures retry with cooldown (`Outbox:Backoff`), then dead-letter (`outbox_messages.failed_at`) with an error
  log. Nothing else is needed to make a handler "retryable".

## 8. Tests

- Plain xUnit `Assert`. No assertion DSL, no mocking of the domain.
- Domain behaviour: `App.Domain.Tests` with `FakeTimeProvider`.
- Mapping and scripts: `App.Infrastructure.Tests` — builds the model, never connects.
- Architecture: `App.ArchitectureTests` — add a rule when you add a convention.
- HTTP, messaging, schema: `App.IntegrationTests` — one PostgreSQL container per run, real `Program.cs`, header
  authentication (`factory.User()`, `factory.Admin()`), `factory.WaitForOutboxAsync()` for asynchronous effects.
  The HAL link contract is a golden master in `Snapshots/HalLinkContract.json`; accept changes by replacing the
  file (or `UPDATE_SNAPSHOTS=1`).

## 9. Dependencies

Register in `docs/DEPENDENCIES.md`, then `Directory.Packages.props`, then reference; write an ADR that argues why
the kernel needs it (ADR-018). No versions in `.csproj`. MediatR, AutoMapper, MassTransit and FluentAssertions fail
the build; the replacements are named in the register.
