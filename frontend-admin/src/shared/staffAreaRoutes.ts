/**
 * Staff hub: canonical App Router paths for secondary nav (Manager read-only oversight).
 * Permissions: existing keys only — user.view, report.view, shift.view (no staff.* catalog).
 */
export const STAFF_AREA_ROUTE_PATHS = [
    '/staff',
    '/staff/list',
    '/staff/performance',
    '/staff/shifts',
] as const;

export type StaffAreaRoutePath = (typeof STAFF_AREA_ROUTE_PATHS)[number];

export const STAFF_HUB_LANDING_PATH = '/staff' as const;
