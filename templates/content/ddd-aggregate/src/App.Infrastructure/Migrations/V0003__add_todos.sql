-- V0003: the todos table. Hand-written and reviewed (ADR-007); SchemaValidationTests checks it against the
-- EF model, so it must stay in step with TodoConfiguration. Rename the file if V0003 is already taken —
-- the version prefix has to be free and unique (SqlMigratorTests).
-- Naming: snake_case, pk_<table>, ix_<table>_<columns>. The aggregate uses the xmin system column for
-- optimistic concurrency, so no version column is declared.

CREATE TABLE todos (
    id          uuid                     NOT NULL,
    name        character varying(200)   NOT NULL,
    description character varying(1000)  NOT NULL,
    state       character varying(50)    NOT NULL,
    created_at  timestamp with time zone NOT NULL,
    CONSTRAINT pk_todos PRIMARY KEY (id)
);

-- Add one CREATE INDEX per builder.HasIndex(...) in TodoConfiguration, for example:
-- CREATE INDEX ix_todos_created_at ON todos (created_at);
