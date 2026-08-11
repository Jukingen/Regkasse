SELECT id::text AS id, "Name" AS name, "Slug" AS slug, is_active
FROM tenants
ORDER BY "Slug";
