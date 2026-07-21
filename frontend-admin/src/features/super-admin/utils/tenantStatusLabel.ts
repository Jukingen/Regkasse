export function tenantStatusColor(status: string): string {
  if (status === 'active') return 'green';
  if (status === 'suspended') return 'orange';
  if (status === 'deleted') return 'red';
  return 'default';
}

export function registerStatusColor(status: string): string {
  const s = status.toLowerCase();
  if (s === 'open') return 'green';
  if (s === 'closed') return 'default';
  if (s === 'maintenance') return 'orange';
  if (s === 'disabled' || s === 'decommissioned') return 'red';
  return 'default';
}
