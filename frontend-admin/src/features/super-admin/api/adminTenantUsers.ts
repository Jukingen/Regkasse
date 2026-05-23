/** @deprecated Import from `@/features/super-admin/api/tenantUsers` */
export {
    type TenantUser,
    type AddTenantUserRequest,
    type UpdateTenantUserRequest,
    type CreateTenantUserRequest,
    type CreateTenantUserResult,
    TENANT_CREATE_ROLES,
    listTenantUsers as listAdminTenantUsers,
    createTenantUser,
    assignTenantUser,
    addTenantUser as addAdminTenantUser,
    updateTenantUser as updateAdminTenantUser,
    removeTenantUser as removeAdminTenantUser,
} from '@/features/super-admin/api/tenantUsers';
