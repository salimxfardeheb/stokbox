-- 0001_initial : schéma initial de Stokbox.
-- Le moteur de migrations exécute ce script dans une transaction : pas de BEGIN/COMMIT ici.

CREATE TABLE settings (
    key   TEXT NOT NULL PRIMARY KEY,
    value TEXT
);

CREATE TABLE categories (
    id   INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

CREATE TABLE products (
    id                   INTEGER PRIMARY KEY,
    barcode              TEXT    NOT NULL,
    name                 TEXT    NOT NULL,
    category_id          INTEGER REFERENCES categories (id),
    purchase_price_cents INTEGER NOT NULL,
    sale_price_cents     INTEGER NOT NULL CHECK (sale_price_cents > 0),
    is_archived          INTEGER NOT NULL DEFAULT 0 CHECK (is_archived IN (0, 1)),
    created_at           TEXT    NOT NULL
);

-- Ligne unique (id = 1) : dernier numéro de code-barres interne attribué (RG-02).
CREATE TABLE barcode_sequence (
    id         INTEGER PRIMARY KEY CHECK (id = 1),
    last_value INTEGER NOT NULL
);

INSERT INTO barcode_sequence (id, last_value) VALUES (1, 0);

CREATE TABLE sales (
    id             INTEGER PRIMARY KEY,
    number         TEXT    NOT NULL UNIQUE,
    created_at     TEXT    NOT NULL,
    total_cents    INTEGER NOT NULL,
    received_cents INTEGER NOT NULL,
    change_cents   INTEGER NOT NULL,
    status         TEXT    NOT NULL CHECK (status IN ('VALIDEE', 'ANNULEE'))
);

CREATE TABLE sale_lines (
    id                        INTEGER PRIMARY KEY,
    sale_id                   INTEGER NOT NULL REFERENCES sales (id),
    product_id                INTEGER NOT NULL REFERENCES products (id),
    quantity                  INTEGER NOT NULL,
    unit_price_cents          INTEGER NOT NULL,
    unit_purchase_price_cents INTEGER NOT NULL,
    line_total_cents          INTEGER NOT NULL
);

-- quantity est signée : positive pour une entrée en stock, négative pour une sortie (RG-01).
CREATE TABLE stock_movements (
    id         INTEGER PRIMARY KEY,
    product_id INTEGER NOT NULL REFERENCES products (id),
    type       TEXT    NOT NULL CHECK (type IN ('ENTREE', 'VENTE', 'RETOUR', 'ANNULATION')),
    quantity   INTEGER NOT NULL,
    sale_id    INTEGER REFERENCES sales (id),
    created_at TEXT    NOT NULL
);

CREATE TABLE returns (
    id         INTEGER PRIMARY KEY,
    sale_id    INTEGER NOT NULL REFERENCES sales (id),
    created_at TEXT    NOT NULL
);

CREATE TABLE return_lines (
    id           INTEGER PRIMARY KEY,
    return_id    INTEGER NOT NULL REFERENCES returns (id),
    sale_line_id INTEGER NOT NULL REFERENCES sale_lines (id),
    quantity     INTEGER NOT NULL
);

-- L'index unique porte à la fois la contrainte UNIQUE et la recherche par code-barres.
CREATE UNIQUE INDEX ix_products_barcode ON products (barcode);
CREATE INDEX ix_products_name ON products (name);
CREATE INDEX ix_stock_movements_product_id ON stock_movements (product_id);
CREATE INDEX ix_sales_created_at ON sales (created_at);
