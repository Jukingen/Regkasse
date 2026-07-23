import { customInstance } from '@/lib/axios';

export type PermissionPackageDto = {
  id: string;
  slug: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  permissionCount: number;
  permissions: string[];
  createdAt: string;
  updatedAt: string;
};

export type UpsertPermissionPackageRequest = {
  slug?: string | null;
  name: string;
  description?: string | null;
  permissions: string[];
};

export type RoleAssignedPackageDto = {
  id: string;
  slug: string;
  name: string;
  permissionCount: number;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function mapPackage(raw: unknown): PermissionPackageDto | null {
  const row = asRecord(raw);
  const id = String(row.id ?? row.Id ?? '');
  if (!id) return null;
  const permissionsRaw = row.permissions ?? row.Permissions ?? [];
  const permissions = Array.isArray(permissionsRaw)
    ? permissionsRaw.map((p) => String(p)).filter(Boolean)
    : [];
  return {
    id,
    slug: String(row.slug ?? row.Slug ?? ''),
    name: String(row.name ?? row.Name ?? ''),
    description: (row.description ?? row.Description ?? null) as string | null,
    isSystem: Boolean(row.isSystem ?? row.IsSystem ?? false),
    permissionCount: Number(row.permissionCount ?? row.PermissionCount ?? permissions.length),
    permissions,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
  };
}

function mapAssigned(raw: unknown): RoleAssignedPackageDto | null {
  const row = asRecord(raw);
  const id = String(row.id ?? row.Id ?? '');
  if (!id) return null;
  return {
    id,
    slug: String(row.slug ?? row.Slug ?? ''),
    name: String(row.name ?? row.Name ?? ''),
    permissionCount: Number(row.permissionCount ?? row.PermissionCount ?? 0),
  };
}

export async function listPermissionPackages(): Promise<PermissionPackageDto[]> {
  const res = await customInstance<unknown[]>({
    url: '/api/admin/permission-packages',
    method: 'GET',
  });
  return (Array.isArray(res) ? res : []).map(mapPackage).filter((p): p is PermissionPackageDto => p !== null);
}

export async function getPermissionPackage(id: string): Promise<PermissionPackageDto> {
  const res = await customInstance<unknown>({
    url: `/api/admin/permission-packages/${id}`,
    method: 'GET',
  });
  const mapped = mapPackage(res);
  if (!mapped) throw new Error('Permission package not found');
  return mapped;
}

export async function createPermissionPackage(
  body: UpsertPermissionPackageRequest
): Promise<PermissionPackageDto> {
  const res = await customInstance<unknown>({
    url: '/api/admin/permission-packages',
    method: 'POST',
    data: body,
  });
  const mapped = mapPackage(res);
  if (!mapped) throw new Error('Create permission package failed');
  return mapped;
}

export async function updatePermissionPackage(
  id: string,
  body: UpsertPermissionPackageRequest
): Promise<PermissionPackageDto> {
  const res = await customInstance<unknown>({
    url: `/api/admin/permission-packages/${id}`,
    method: 'PUT',
    data: body,
  });
  const mapped = mapPackage(res);
  if (!mapped) throw new Error('Update permission package failed');
  return mapped;
}

export async function deletePermissionPackage(id: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/permission-packages/${id}`,
    method: 'DELETE',
  });
}

export async function listRoleAssignedPackages(
  roleName: string
): Promise<RoleAssignedPackageDto[]> {
  const res = await customInstance<unknown[]>({
    url: `/api/admin/permission-packages/roles/${encodeURIComponent(roleName)}/packages`,
    method: 'GET',
  });
  return (Array.isArray(res) ? res : [])
    .map(mapAssigned)
    .filter((p): p is RoleAssignedPackageDto => p !== null);
}

export async function addPackageToRole(roleName: string, packageId: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/permission-packages/roles/${encodeURIComponent(roleName)}/packages/${packageId}`,
    method: 'POST',
  });
}

export async function removePackageFromRole(roleName: string, packageId: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/permission-packages/roles/${encodeURIComponent(roleName)}/packages/${packageId}`,
    method: 'DELETE',
  });
}
