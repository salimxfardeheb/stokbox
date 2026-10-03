-- 0002_dashboard_indexes : index des agrégations du tableau de bord (ventes et retours par période).
-- Le moteur de migrations exécute ce script dans une transaction : pas de BEGIN/COMMIT ici.

CREATE INDEX ix_sale_lines_sale_id ON sale_lines (sale_id);
CREATE INDEX ix_returns_created_at ON returns (created_at);
CREATE INDEX ix_returns_sale_id ON returns (sale_id);
CREATE INDEX ix_return_lines_return_id ON return_lines (return_id);
CREATE INDEX ix_return_lines_sale_line_id ON return_lines (sale_line_id);
