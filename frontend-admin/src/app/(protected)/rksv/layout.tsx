'use client';

import React, { ReactNode } from 'react';

/**
 * RKSV routes are gated by `PermissionRouteGuard` + `ROUTE_PERMISSIONS` (e.g. finanzonline.manage).
 * Do not add a separate SuperAdmin-only gate here — it hid the shell on deny and blocked Manager.
 */
export default function RksvLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
