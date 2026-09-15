-- Reference data has its own lifecycle and is versioned with the schema (design §4.4).
-- Ids are stable so tests and clients can rely on them; see App.Infrastructure.Persistence.SeededCities.

INSERT INTO cities (id, postal_code, name_en, name_de) VALUES
    ('00000000-0000-7000-8000-000000001000', 1000, 'Lausanne', 'Lausanne'),
    ('00000000-0000-7000-8000-000000003000', 3000, 'Bern',     'Bern'),
    ('00000000-0000-7000-8000-000000008000', 8000, 'Zurich',   'Zürich')
ON CONFLICT (id) DO NOTHING;
