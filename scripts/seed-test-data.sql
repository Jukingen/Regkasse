-- Test tenant
INSERT INTO tenants (id, name, slug, is_active, status)
VALUES ('22222222-2222-2222-2222-222222222222', 'Test Cafe', 'test_cafe', true, 'Active')
ON CONFLICT (slug) DO NOTHING;

-- Test user (Super Admin)
INSERT INTO "AspNetUsers" (id, user_name, normalized_user_name, email, normalized_email, role, is_active)
VALUES ('11111111-1111-1111-1111-111111111111', 'testsprite_admin', 'TESTSPRITE_ADMIN', 'testsprite@regkasse.at', 'TESTSPRITE@REGKASSE.AT', 'SuperAdmin', true)
ON CONFLICT (email) DO NOTHING;

-- Test product
INSERT INTO products (id, name, price, tax_type, category, stock_quantity, tenant_id)
VALUES ('33333333-3333-3333-3333-333333333333', 'Test Product', 10.00, 1, 'Test', 100, '22222222-2222-2222-2222-222222222222')
ON CONFLICT (id) DO NOTHING;
