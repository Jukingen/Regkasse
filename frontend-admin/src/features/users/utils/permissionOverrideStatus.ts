export type PermissionOverrideStatus =
  | 'scheduled'
  | 'active'
  | 'expiringSoon'
  | 'expired';

/**
 * Mirrors backend UserPermissionOverrideStatuses.Compute.
 */
export function computePermissionOverrideStatus(
  validFrom: string | Date | null | undefined,
  expiresAt: string | Date | null | undefined,
  now: Date = new Date(),
  expiringSoonHours = 48
): PermissionOverrideStatus {
  const utcNow = now.getTime();
  const expiresMs = expiresAt ? new Date(expiresAt).getTime() : null;
  const validFromMs = validFrom ? new Date(validFrom).getTime() : null;

  if (expiresMs != null && !Number.isNaN(expiresMs) && expiresMs <= utcNow) {
    return 'expired';
  }
  if (validFromMs != null && !Number.isNaN(validFromMs) && validFromMs > utcNow) {
    return 'scheduled';
  }
  if (
    expiresMs != null &&
    !Number.isNaN(expiresMs) &&
    expiresMs <= utcNow + Math.max(1, expiringSoonHours) * 60 * 60 * 1000
  ) {
    return 'expiringSoon';
  }
  return 'active';
}

export function permissionOverrideStatusColor(status: PermissionOverrideStatus): string {
  switch (status) {
    case 'scheduled':
      return 'blue';
    case 'expiringSoon':
      return 'orange';
    case 'expired':
      return 'default';
    default:
      return 'green';
  }
}
