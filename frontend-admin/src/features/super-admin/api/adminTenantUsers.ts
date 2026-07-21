/** @deprecated Import from `@/features/super-admin/api/tenantUsers` */
export {
  addTenantUser as addAdminTenantUser,
  type AddTenantUserRequest,
  assignTenantUser,
  createTenantUser,
  type CreateTenantUserRequest,
  type CreateTenantUserResult,
  listTenantUsers as listAdminTenantUsers,
  removeTenantUser as removeAdminTenantUser,
  TENANT_CREATE_ROLES,
  type TenantUser,
  updateTenantUser as updateAdminTenantUser,
  type UpdateTenantUserRequest,
} from '@/features/super-admin/api/tenantUsers';
