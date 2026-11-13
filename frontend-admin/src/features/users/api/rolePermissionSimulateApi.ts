import { customInstance } from '@/lib/axios';

export type RolePermissionSimulateRequest = {
  proposedPermissions: string[];
  page?: number;
  pageSize?: number;
};

export type RolePermissionSimulateUserImpactDto = {
  userId: string;
  userName: string;
  displayRole: string;
  permissionsGained: number;
  permissionsLost: number;
  gainedKeysSample: string[];
  lostKeysSample: string[];
};

export type RolePermissionSimulateResultDto = {
  roleName: string;
  currentPermissions: string[];
  proposedPermissions: string[];
  added: string[];
  removed: string[];
  affectedUserCount: number;
  users: RolePermissionSimulateUserImpactDto[];
  page: number;
  pageSize: number;
  total: number;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function asStringList(value: unknown): string[] {
  return Array.isArray(value) ? value.map((v) => String(v)).filter(Boolean) : [];
}

function mapUser(raw: unknown): RolePermissionSimulateUserImpactDto {
  const row = asRecord(raw);
  return {
    userId: String(row.userId ?? row.UserId ?? ''),
    userName: String(row.userName ?? row.UserName ?? ''),
    displayRole: String(row.displayRole ?? row.DisplayRole ?? ''),
    permissionsGained: Number(row.permissionsGained ?? row.PermissionsGained ?? 0),
    permissionsLost: Number(row.permissionsLost ?? row.PermissionsLost ?? 0),
    gainedKeysSample: asStringList(row.gainedKeysSample ?? row.GainedKeysSample),
    lostKeysSample: asStringList(row.lostKeysSample ?? row.LostKeysSample),
  };
}

export async function simulateRolePermissions(
  roleName: string,
  body: RolePermissionSimulateRequest
): Promise<RolePermissionSimulateResultDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: `/api/admin/permission-packages/roles/${encodeURIComponent(roleName)}/permissions/simulate`,
    method: 'POST',
    data: {
      proposedPermissions: body.proposedPermissions,
      page: body.page ?? 1,
      pageSize: body.pageSize ?? 50,
    },
  });

  const usersRaw = res.users ?? res.Users ?? [];
  return {
    roleName: String(res.roleName ?? res.RoleName ?? roleName),
    currentPermissions: asStringList(res.currentPermissions ?? res.CurrentPermissions),
    proposedPermissions: asStringList(res.proposedPermissions ?? res.ProposedPermissions),
    added: asStringList(res.added ?? res.Added),
    removed: asStringList(res.removed ?? res.Removed),
    affectedUserCount: Number(res.affectedUserCount ?? res.AffectedUserCount ?? 0),
    users: (Array.isArray(usersRaw) ? usersRaw : []).map(mapUser),
    page: Number(res.page ?? res.Page ?? 1),
    pageSize: Number(res.pageSize ?? res.PageSize ?? 50),
    total: Number(res.total ?? res.Total ?? 0),
  };
}
