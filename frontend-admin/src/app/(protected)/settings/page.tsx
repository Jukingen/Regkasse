'use client';

import { ManagerSettings } from '@/features/settings/components/ManagerSettings';
import { SuperAdminSettings } from '@/features/settings/components/SuperAdminSettings';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function SettingsPage() {
    const { hasPermission } = usePermissions();
    const canManageSettings = hasPermission(PERMISSIONS.SETTINGS_MANAGE);

    if (canManageSettings) {
        return <SuperAdminSettings />;
    }

    return <ManagerSettings />;
}
