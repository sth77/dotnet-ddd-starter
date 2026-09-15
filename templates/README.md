# D4S.Ddd.Templates

`dotnet new` template pack for the tactical-DDD starter (design §12). Two templates generate the same shape
the `Sample` aggregate and the `City` reference data have, straight into the existing projects — no new
project, no nested folder, no Node.

| Short name      | Identity                  | `sourceName` | What it adds                                                    |
| --------------- | ------------------------- | ------------ | --------------------------------------------------------------- |
| `ddd-aggregate` | `D4S.Ddd.Aggregate`      | `Todo`       | Aggregate: domain model, repository, EF mapping, SQL migration, endpoints, tests |
| `ddd-refdata`   | `D4S.Ddd.ReferenceData`  | `Country`    | Reference-data entity: model, repository, EF mapping, SQL migration with seed, read endpoints |

## Install

From the repository root, straight from the folder:

```bash
dotnet new install ./templates
```

Or from the packed NuGet package (what you would publish to an internal feed):

```bash
dotnet pack templates/D4S.Ddd.Templates.csproj -o artifacts
dotnet new install artifacts/D4S.Ddd.Templates.0.1.0.nupkg
```

Uninstall with the same argument you installed with:

```bash
dotnet new uninstall ./templates          # or: dotnet new uninstall D4S.Ddd.Templates
```

> The template project is deliberately **not** a member of the repository's `.slnx` solution. It is packed on its own,
> so `dotnet build` at the repository root never sees the template content.

## Use

Both templates are *item* templates and must be run **from the repository root** — they write into
`src/App.Domain`, `src/App.Infrastructure`, `src/App.Api` and `tests/App.Domain.Tests` relative to the
current directory, and they never create a directory of their own (`preferNameDirectory: false`).

```bash
dotnet new ddd-aggregate -n Todo                              # /api/todos
dotnet new ddd-aggregate -n Invoice --plural Invoices         # explicit plural
dotnet new ddd-refdata   -n Country --migration-version V0004 # /api/countries
```

### What `ddd-aggregate -n Todo` writes

```
src/App.Domain/Todo/TodoId.cs             strongly-typed id (readonly record struct, UUID v7)
src/App.Domain/Todo/TodoState.cs          Draft | Active | Closed
src/App.Domain/Todo/TodoCommand.cs        Create | Update | Activate | Close (closed hierarchy)
src/App.Domain/Todo/TodoEvent.cs          Created | Updated | Activated | Closed
src/App.Domain/Todo/Todo.cs               AggregateRoot<Todo, TodoId> + Can<TCommand>() state machine
src/App.Domain/Todo/ITodos.cs             repository interface (no IQueryable)
src/App.Infrastructure/Persistence/Todo/TodoConfiguration.cs
src/App.Infrastructure/Persistence/Todo/Todos.cs
src/App.Infrastructure/Migrations/V0003__add_todos.sql   CREATE TABLE todos, matching the configuration
src/App.Api/Todo/TodoRepresentations.cs   TodoSummary, TodoDetail, TodoRels (+ CommandEndpoints)
src/App.Api/Todo/TodoEndpoints.cs         GET list, GET by id, POST, PUT, POST activate, POST close
tests/App.Domain.Tests/Todo/TodoTests.cs  xUnit tests for the state machine
```

The identifier is hand-written: a `readonly record struct` implementing `IIdentifier<TSelf, Guid>` with a
private constructor, `New()`/`From()` factories and a `[JsonConverter(typeof(SingleValueJsonConverter<…>))]`
attribute (ADR-004). Analyzer DDD0002 rejects `new TodoId()` and `default(TodoId)`. **Nothing has to be
registered for persistence**: `RegisterDomainValueObjects` finds every value object in the domain assembly and
gives it an EF converter.

Note the persistence namespace: the files live in a `Todo` folder but declare `namespace
App.Infrastructure.Persistence`, flat, exactly like `Sample`/`City` do. A namespace segment equal to the
aggregate name would clash with the type name.

### What `ddd-refdata -n Country` writes

```
src/App.Domain/ReferenceData/CountryId.cs
src/App.Domain/ReferenceData/Country.cs           IEntity<CountryId>, Code + I18nText Name
src/App.Domain/ReferenceData/ICountries.cs
src/App.Infrastructure/Persistence/ReferenceData/CountryConfiguration.cs
src/App.Infrastructure/Persistence/ReferenceData/Countries.cs
src/App.Infrastructure/Migrations/V0003__add_countries.sql   table, unique index, example seed row
src/App.Api/ReferenceData/CountryEndpoints.cs     read-only list + get
```

## Parameters

| Parameter             | Default            | Effect                                                              |
| --------------------- | ------------------ | -------------------------------------------------------------------- |
| `-n`                  | `Todo` / `Country` | PascalCase name. Renames the files and replaces the type names.      |
| `--plural`            | derived            | PascalCase plural: repository type, endpoint names, route segment, table name. |
| `--migration-version` | `V0003`            | Version prefix of the generated SQL script; also renames the file.   |

The default plural is a deliberately naive rule — lower-case the name, `y` → `ies`, otherwise append `s`
(`Todo` → `Todos`, `City` → `Cities`, `Country` → `Countries`). It is right often enough to be useful and
wrong often enough to be worth overriding: `dotnet new ddd-aggregate -n Person --plural People`. The
lower-case form of the plural becomes the route segment (`/api/people`), the HAL collection rel, the table
name and the migration file name (`V0003__add_people.sql`).

`--migration-version` defaults to `V0003` in **both** templates, which is only ever right for the first one you
run. Migration versions have to be free and unique: `SqlMigratorTests` asserts that every embedded script is
named `Vnnnn__lower_snake_case` and that no two scripts share a `Vnnnn` prefix. Look at
`src/App.Infrastructure/Migrations/`, take the next free number, and either pass it
(`--migration-version V0007`) or rename the generated file afterwards. That is why the regeneration check
below generates the reference data with `--migration-version V0004`.

## Why two templates and not four

Design §12 sketches `ddd-feature`, `ddd-aggregate`, `ddd-endpoints` and `ddd-refdata`. The starter organises
features as folders inside the existing projects (design §3, option A), not as projects: there is nothing for
a `ddd-feature` template to create that `ddd-aggregate` does not already create, and no feature-level file
(no `.csproj`, no module registration, no DI class) that would exist on its own. `ddd-endpoints` would be a
second template that can only ever be run immediately after the first one, on the same name, and would need
the command hierarchy to already exist to be useful. Both are therefore folded into `ddd-aggregate`, which
emits the whole vertical slice in one call. If a feature ever does become a project of its own (ADR-002),
`ddd-feature` comes back as a separate template that emits the `.csproj` and the solution entry.

## The two manual steps

`dotnet new` creates files well but cannot *inject* into existing ones. The starter removes that problem
where it can — endpoint modules are discovered by assembly scan (`MapApi()`), EF mappings by
`ApplyConfigurationsFromAssembly()`, EF value converters by `RegisterDomainValueObjects()`, event handlers by
the scan in `AddMessaging()`, migration scripts by the `Migrations/*.sql` embedded-resource glob, and the
repositories use `db.Set<T>()` rather than a `DbSet` property on `AppDbContext` — but two things remain. They
are printed as manual instructions after generation:

1. **`src/App.Infrastructure/InfrastructureServiceCollectionExtensions.cs`** — add
   `services.AddScoped<ITodos, Todos>();` inside `AddPersistence` (and the `using App.Domain.Todo;`).
   Reference data needs no new `using`: `App.Domain.ReferenceData` is already imported there.
2. **The migration version** — check that the generated
   `src/App.Infrastructure/Migrations/V0003__add_todos.sql` carries the next free `Vnnnn` number and rename it
   if not (see `--migration-version` above). Review its columns against the generated `…Configuration`; for
   reference data, replace the example `INSERT … ON CONFLICT DO NOTHING` row with the real entries, keeping
   the ids stable. `SchemaValidationTests` (integration suite) diffs the database against the EF model, so a
   column the mapping declares and the script forgets fails there.

Then `dotnet build -warnaserror && dotnet test`.

## The regeneration check

`build/regen-check.ps1` (PowerShell 7) and `build/regen-check.sh` (bash) are the `-Phygen-it` equivalent and
run as the last gate of `build/ci.ps1` / `build/ci.sh`. Each one copies the repository to a temp directory,
installs this pack, generates `Todo` and `Country --migration-version V0004` into the copy, applies the
repository registration by text edit, asserts that the two generated SQL scripts exist and contain
`CREATE TABLE todos` / `CREATE TABLE countries`, then runs `dotnet build -warnaserror`,
`dotnet format --verify-no-changes` and the domain, architecture and infrastructure test suites — and finally
uninstalls the pack and deletes the copy. A template that drifts away from the starter's conventions (a new
analyzer rule, a renamed base class, a changed HAL helper) fails the build there rather than in the first
project that uses it.

```bash
pwsh -NoProfile -File build/regen-check.ps1     # add -KeepTemp to inspect the generated copy
./build/regen-check.sh                          # --keep-temp
```
