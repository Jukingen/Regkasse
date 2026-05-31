-- Full duplicate verification after PG demo import (cafe tenant)
SELECT tenant_id, "Name", COUNT(*) AS cnt
FROM categories GROUP BY tenant_id, "Name" HAVING COUNT(*) > 1;

SELECT tenant_id, lower(trim("Name")) AS name_ci, COUNT(*) AS cnt
FROM categories GROUP BY tenant_id, lower(trim("Name")) HAVING COUNT(*) > 1;

SELECT tenant_id, category_key, COUNT(*) AS cnt
FROM categories GROUP BY tenant_id, category_key HAVING COUNT(*) > 1;

SELECT t."Slug", COUNT(c.id) AS category_count
FROM tenants t
LEFT JOIN categories c ON c.tenant_id = t.id
GROUP BY t."Slug"
ORDER BY t."Slug";

SELECT indexname FROM pg_indexes WHERE tablename = 'categories' AND indexname LIKE '%Name%';
