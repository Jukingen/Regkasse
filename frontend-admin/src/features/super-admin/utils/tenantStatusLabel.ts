export function tenantStatusColor(status: string): string {
  const s = status.trim().toLowerCase();
  if (s === 'active') return 'green';
  if (s === 'in_onboarding' || s === 'lead') return 'blue';
  if (s === 'suspended') return 'orange';
  if (s === 'cancelled') return 'gold';
  if (s === 'archived' || s === 'deleted') return 'red';
  return 'default';
}

/** Soft-deleted / cancelled / archived (incl. legacy deleted). */
export function isTenantRemovedStatus(status: string | null | undefined): boolean {
  if (!status) return false;
  const s = status.trim().toLowerCase();
  return s === 'deleted' || s === 'archived' || s === 'cancelled';
}

export function registerStatusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'open') return 'green';
  if (s === 'closed') return 'default';
  if (s === 'maintenance') return 'orange';
  if (s === 'disabled' || s === 'decommissioned') return 'red';
  return 'default';
}
