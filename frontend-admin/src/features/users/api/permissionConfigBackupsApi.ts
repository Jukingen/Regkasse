import { customInstance } from '@/lib/axios';

export type PermissionConfigBackupListItemDto = {
  id: string;
  name: string;
  note?: string | null;
  createdAt: string;
  createdByUserId?: string | null;
  trigger: string;
  schemaVersion: number;
};

export type CreatePermissionConfigBackupRequest = {
  name?: string | null;
  note?: string | null;
};

export type PermissionConfigBackupSettingsDto = {
  autoBackupBeforeChanges: boolean;
};

export type PermissionConfigRestorePreviewDto = {
  backupId: string;
  customRolesChanged: number;
  packagesChanged: number;
  overridesChanged: number;
  warnings: string[];
  sampleRoleDeltas: string[];
};

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}

function asStringList(value: unknown): string[] {
  return Array.isArray(value) ? value.map((v) => String(v)).filter(Boolean) : [];
}

function mapBackup(raw: unknown): PermissionConfigBackupListItemDto | null {
  const row = asRecord(raw);
  const id = String(row.id ?? row.Id ?? '');
  if (!id) return null;
  return {
    id,
    name: String(row.name ?? row.Name ?? ''),
    note: (row.note ?? row.Note ?? null) as string | null,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    createdByUserId: (row.createdByUserId ?? row.CreatedByUserId ?? null) as string | null,
    trigger: String(row.trigger ?? row.Trigger ?? ''),
    schemaVersion: Number(row.schemaVersion ?? row.SchemaVersion ?? 1),
  };
}

function mapPreview(raw: unknown): PermissionConfigRestorePreviewDto {
  const row = asRecord(raw);
  return {
    backupId: String(row.backupId ?? row.BackupId ?? ''),
    customRolesChanged: Number(row.customRolesChanged ?? row.CustomRolesChanged ?? 0),
    packagesChanged: Number(row.packagesChanged ?? row.PackagesChanged ?? 0),
    overridesChanged: Number(row.overridesChanged ?? row.OverridesChanged ?? 0),
    warnings: asStringList(row.warnings ?? row.Warnings),
    sampleRoleDeltas: asStringList(row.sampleRoleDeltas ?? row.SampleRoleDeltas),
  };
}

export async function listPermissionConfigBackups(): Promise<PermissionConfigBackupListItemDto[]> {
  const res = await customInstance<unknown[]>({
    url: '/api/admin/permission-config-backups',
    method: 'GET',
  });
  return (Array.isArray(res) ? res : [])
    .map(mapBackup)
    .filter((b): b is PermissionConfigBackupListItemDto => b !== null);
}

export async function createPermissionConfigBackup(
  body?: CreatePermissionConfigBackupRequest
): Promise<PermissionConfigBackupListItemDto> {
  const res = await customInstance<unknown>({
    url: '/api/admin/permission-config-backups',
    method: 'POST',
    data: body ?? {},
  });
  const mapped = mapBackup(res);
  if (!mapped) throw new Error('Create permission config backup failed');
  return mapped;
}

export async function previewPermissionConfigRestore(
  id: string
): Promise<PermissionConfigRestorePreviewDto> {
  const res = await customInstance<unknown>({
    url: `/api/admin/permission-config-backups/${id}/preview-restore`,
    method: 'GET',
  });
  return mapPreview(res);
}

export async function restorePermissionConfigBackup(id: string): Promise<void> {
  await customInstance<void>({
    url: `/api/admin/permission-config-backups/${id}/restore`,
    method: 'POST',
  });
}

export async function getPermissionConfigBackupSettings(): Promise<PermissionConfigBackupSettingsDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: '/api/admin/permission-config-backups/settings',
    method: 'GET',
  });
  return {
    autoBackupBeforeChanges: Boolean(
      res.autoBackupBeforeChanges ?? res.AutoBackupBeforeChanges ?? false
    ),
  };
}

export async function setPermissionConfigBackupSettings(
  body: PermissionConfigBackupSettingsDto
): Promise<PermissionConfigBackupSettingsDto> {
  const res = await customInstance<Record<string, unknown>>({
    url: '/api/admin/permission-config-backups/settings',
    method: 'PUT',
    data: body,
  });
  return {
    autoBackupBeforeChanges: Boolean(
      res.autoBackupBeforeChanges ?? res.AutoBackupBeforeChanges ?? body.autoBackupBeforeChanges
    ),
  };
}
