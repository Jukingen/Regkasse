import type { AuditLogEntryDto, UserInfoDto } from '@/api/generated/model';

/** Alias for audit actor snapshot (backend `UserInfoDto`). */
export type AuditActorUserInfo = UserInfoDto;

/** @deprecated Prefer `AuditLogEntryDto` — user fields are on the generated model. */
export type AuditLogEntryWithUser = AuditLogEntryDto;

export function resolveAuditActorDisplayName(record: AuditLogEntryDto): string {
  return (
    record.user?.displayName?.trim() ||
    record.userDisplayName?.trim() ||
    record.actorDisplayName?.trim() ||
    record.user?.userName?.trim() ||
    record.userName?.trim() ||
    record.createdBy?.trim() ||
    record.userId?.trim() ||
    ''
  );
}

export function resolveAuditActorUser(record: AuditLogEntryDto): UserInfoDto | undefined {
  if (record.user) return record.user;
  const id = record.userId?.trim();
  const displayName = resolveAuditActorDisplayName(record);
  if (!id && !displayName) return undefined;
  return {
    id: id || undefined,
    userName: record.userName?.trim() || undefined,
    email: record.userEmail?.trim() || undefined,
    displayName: displayName || undefined,
    role: record.userRole?.trim() || undefined,
  };
}
