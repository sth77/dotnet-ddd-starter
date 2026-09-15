# Guidance for coding agents

Read `INSTRUCTIONS.md` for the coding rules and `docs/architectural-decisions.md` for why things are the way they
are. This file lists what an agent is most likely to get wrong.

## The starter is a kernel, not a menu

Do not add libraries for things the repository does by hand on purpose (ADR-018): no message bus (the outbox is
in `App.Infrastructure.Messaging`), no value-object generator (identifiers are `readonly record struct`s), no
assertion DSL (plain xUnit `Assert`), no snapshot library (`Snapshot.Match`), no migration tool (`SqlMigrator`),
no mediator, no object mapper. If you believe one is needed, write the ADR first.

## Correct your training cutoff

Most training data predates mid-2025 and 2026. In this repository:

- **MediatR, AutoMapper, MassTransit and FluentAssertions are prohibited** (licence changes; see
  `docs/DEPENDENCIES.md`). The build fails with `D4S-0001` if you add them.
- **.NET 10 / C# 14 / EF Core 10.** `Microsoft.Extensions.Validation` (`AddValidation()`) is in the shared
  framework; do not add it as a package. EF 10 **complex types** replace owned types. `Guid.CreateVersion7()` exists.
- **Swashbuckle is gone.** OpenAPI comes from `Microsoft.AspNetCore.OpenApi`; the UI is Scalar.
- **EF Core migrations are not used.** Schema changes are SQL scripts under `src/App.Infrastructure/Migrations`;
  `dotnet ef` is not installed for this repository and there is no design-time factory.
- **xUnit v3**: `IAsyncLifetime` uses `ValueTask`; `[Fact]` comes from a global using.

## Where things go

| You are adding | Put it in | Then |
|---|---|---|
| an aggregate | `src/App.Domain/<Feature>/` (id, state, commands, events, aggregate, repository interface) | run `dotnet new ddd-aggregate -n <Name>` instead of writing by hand, then the printed steps |
| reference data | `src/App.Domain/ReferenceData/` | `dotnet new ddd-refdata -n <Name>` |
| a mapping change | `src/App.Infrastructure/Persistence/<Feature>/<Name>Configuration.cs` | a new `Migrations/V000N__*.sql`; run the integration tests (`SchemaValidationTests`) |
| an identifier | anywhere in the domain, copying `SampleId.cs` | nothing else — converters are discovered |
| an endpoint | `src/App.Api/<Feature>/<Feature>Endpoints.cs` | give it a name and a policy; add a `CommandEndpoint` for the link |
| an event handler | `src/App.Application/<Feature>/` implementing `IEventHandler<TEvent>` | nothing else — handlers are discovered |
| a repository | `src/App.Infrastructure/Persistence/<Feature>/` | `services.AddScoped<IThings, Things>()` at the `<ddd-scaffold:repositories>` marker in `AddPersistence` |
| a package | `docs/DEPENDENCIES.md` first, then `Directory.Packages.props`, then an ADR | never a version in a `.csproj` |

## Non-negotiables the build checks

- The domain project references nothing but the BCL. EF and ASP.NET Core namespaces are banned there
  (`BannedSymbols.Domain.txt`); `DateTime.Now`, `Guid.NewGuid` and `Newtonsoft.Json` are banned everywhere
  (tests included — use `TimeProvider.System`).
- Public state-changing methods on aggregates take a command as the first parameter (`DDD0001`).
- Identifiers are never created with `new` or `default` (`DDD0002`).
- Every POST/PUT/PATCH/DELETE under `/api` has a policy (startup check).
- The migrated database matches the EF model (`SchemaValidationTests`, integration suite).
- `dotnet format --verify-no-changes` passes: LF line endings, file-scoped namespaces, usings outside the
  namespace with `System` first, braces always, no unused usings (`IDE0005` is an error), private instance fields
  `_camelCase`, private static/const fields `PascalCase`.
- Type names may equal namespace names (`App.Domain.Sample.Sample`). Outside the domain, refer to aggregates as
  `Domain.Sample.Sample` or alias them; never create a namespace segment named after an aggregate in
  infrastructure or tests (ADR-015).

## Known failure signatures

| Signature | Cause | Fix |
|---|---|---|
| `SchemaValidationTests`: `<table>.<column> is missing` / `is text, model expects …` | mapping and SQL script drifted | add a script or fix the mapping |
| `Migration V000N was modified after it was applied` | edited an applied script | revert; add a new script |
| `DDD0001` | aggregate operation without a command | add a command record |
| `DDD0002` | `new`/`default` identifier | `X.New()` / `X.From(...)` |
| `D4S-0001` | prohibited package | see DEPENDENCIES.md §2 |
| `NU1510` | package is in the shared framework | remove the reference |
| `RS0030 … DateTime.UtcNow is banned` | banned API, also in tests | `TimeProvider.System.GetUtcNow()` |
| `CS8927` in a `ValueConverter` | static abstract call in an expression tree | route through a static helper |
| `No suitable constructor was found for the type` | nested value object without parameterless ctor, or get-only property not configured | see ADR-006 |
| `Complex type 'X.Y#Z' has no properties defined` | a component of the complex type is itself a value object; EF discovers only primitives inside a complex type | name each one: `ComplexProperty(x => x.Y, y => { y.Property(p => p.A); ... })` |
| `SchemaValidationTests`: column is `numeric`, model expects `numeric(p,s)` | a single-value value object over `decimal` has no precision | `configurationBuilder.Properties<TMoney>().HavePrecision(18, 2)` in `ConfigureConventions` |
| `CA1822 … 'Can' does not access instance data` | an aggregate whose `Can(Type)` has no state machine | `[SuppressMessage("Performance", "CA1822:Mark members as static", …)]` — the uniform aggregate API is worth the attribute |
| `anchor '…' not found` from `regen-check` | the `// <ddd-scaffold:repositories>` marker was removed | put it back (ADR-020); the scaffolding gates anchor on it |
| `operator does not exist: jsonb ~~ jsonb` | LINQ `Contains` on the outbox payload | filter in memory or use `EF.Functions.JsonContains` |
| `IDE1006 Missing prefix '_'` | private *instance* field without underscore | rename; static/const fields are PascalCase |
| `ENDOFLINE` from `dotnet format` | CRLF written by a tool (Python on Windows writes CRLF by default) | write with `newline='\n'`; `.gitattributes` normalises on commit |

## Verify before you report done

```bash
dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test --filter FullyQualifiedName!~IntegrationTests
```

Run the integration tests too whenever you touched persistence, SQL scripts, endpoints or messaging (needs Docker;
on this Windows machine run them from WSL — see README).
