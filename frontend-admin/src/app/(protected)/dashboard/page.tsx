'use client';

import { ManagerDashboard } from '@/features/dashboard/components/ManagerDashboard';
import { SuperAdminDashboard } from '@/features/dashboard/components/SuperAdminDashboard';
import { usePermissions } from '@/hooks/usePermissions';

export default function DashboardPage() {
  const { isSuperAdmin } = usePermissions();

  if (isSuperAdmin) {
    return <SuperAdminDashboard />;
  }

  return <ManagerDashboard />;
}
