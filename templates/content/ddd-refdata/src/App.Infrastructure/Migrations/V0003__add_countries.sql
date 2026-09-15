-- V0003: the countries reference table. Hand-written and reviewed (ADR-007); SchemaValidationTests checks it
-- against the EF model, so it must stay in step with CountryConfiguration. Rename the file if V0003 is already
-- taken — the version prefix has to be free and unique (SqlMigratorTests).
-- Naming: snake_case, pk_<table>, ix_<table>_<columns>. Reference data is not an aggregate: no xmin, no
-- concurrency token.
-- The name_* columns follow App.Domain.Common.I18nText: one column per language it declares. Change that
-- record and this script has to follow, or SchemaValidationTests fails.

CREATE TABLE countries (
    id      uuid                   NOT NULL,
    code    character varying(10)  NOT NULL,
    name_en character varying(200) NOT NULL,
    name_de character varying(200) NOT NULL,
    CONSTRAINT pk_countries PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_countries_code ON countries (code);

-- Reference data has its own lifecycle and is versioned with the schema (design §4.4). Ids are stable so tests
-- and clients can rely on them; replace the example row with the real entries.

INSERT INTO countries (id, code, name_en, name_de) VALUES
    ('00000000-0000-7000-8000-000000000756', 'CH', 'Switzerland', 'Schweiz')
ON CONFLICT (id) DO NOTHING;
