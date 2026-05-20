/** @deprecated Import from `@/features/super-admin/api/tenantUsers` */
export {
    type TenantUser,
    type AddTenantUserRequest,
    type UpdateTenantUserRequest,
    type InviteTenantUserRequest,
    type TenantUserInviteResult,
    INVITE_TENANT_ROLES,
    listTenantUsers as listAdminTenantUsers,
    addTenantUser as addAdminTenantUser,
    inviteTenantUser as inviteAdminTenantUser,
    updateTenantUser as updateAdminTenantUser,
    removeTenantUser as removeAdminTenantUser,
} from '@/features/super-admin/api/tenantUsers';
