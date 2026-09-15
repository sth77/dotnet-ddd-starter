-- Initial schema. Hand-written and reviewed (ADR-007); SchemaValidationTests checks it against the EF model.
-- Naming: snake_case, pk_<table>, ix_<table>_<columns>. Aggregates rely on the xmin system column for
-- optimistic concurrency, so no version column is declared.

CREATE TABLE cities (
    id          uuid                   NOT NULL,
    postal_code integer                NOT NULL,
    name_en     character varying(200) NOT NULL,
    name_de     character varying(200) NOT NULL,
    CONSTRAINT pk_cities PRIMARY KEY (id)
);

CREATE INDEX ix_cities_postal_code ON cities (postal_code);

CREATE TABLE people (
    id    uuid                   NOT NULL,
    name  character varying(200) NOT NULL,
    email character varying(320) NOT NULL,
    CONSTRAINT pk_people PRIMARY KEY (id)
);

CREATE UNIQUE INDEX ix_people_email ON people (email);

CREATE TABLE samples (
    id               uuid                     NOT NULL,
    name_en          character varying(200)   NOT NULL,
    name_de          character varying(200)   NOT NULL,
    description      character varying(1000)  NOT NULL,
    city_postal_code integer                  NULL,
    city_name_en     character varying(200)   NULL,
    city_name_de     character varying(200)   NULL,
    state            character varying(50)    NOT NULL,
    owner            uuid                     NOT NULL,
    owner_name       character varying(200)   NOT NULL,
    created_at       timestamp with time zone NOT NULL,
    CONSTRAINT pk_samples PRIMARY KEY (id)
);

CREATE INDEX ix_samples_owner ON samples (owner);

-- Transactional outbox / idempotent inbox (ADR-008).

CREATE TABLE outbox_messages (
    id           uuid                     NOT NULL,
    type         character varying(400)   NOT NULL,
    payload      jsonb                    NOT NULL,
    occurred_at  timestamp with time zone NOT NULL,
    attempts     integer                  NOT NULL,
    locked_until timestamp with time zone NULL,
    processed_at timestamp with time zone NULL,
    failed_at    timestamp with time zone NULL,
    last_error   text                     NULL,
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id)
);

CREATE INDEX ix_outbox_messages_occurred_at ON outbox_messages (occurred_at)
    WHERE processed_at IS NULL AND failed_at IS NULL;

CREATE TABLE inbox_messages (
    message_id   uuid                     NOT NULL,
    handler      character varying(400)   NOT NULL,
    processed_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_inbox_messages PRIMARY KEY (message_id, handler)
);
