'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Dropdown, Tag } from 'antd';
import type { MenuProps } from 'antd';
import { SettingOutlined } from '@ant-design/icons';

import {
  fetchPublicDevelopmentModeSettings,
  publicDevelopmentModeQueryKey,
} from '@/features/development-mode/developmentModeApi';
import { ENVIRONMENT_CONFIG } from '@/shared/config/environmentBadge';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

/**
 * Header badge: shows when persisted development-mode is enabled (effective bypasses still require a Development host).
 */
export function EnvironmentBadge() {
  const { data } = useQuery({
    queryKey: publicDevelopmentModeQueryKey,
    queryFn: fetchPublicDevelopmentModeSettings,
    refetchInterval: 30_000,
  });

  const buildEnvBadge = ENVIRONMENT_CONFIG.getEnvironmentBadge();

  if (!data?.enabled) {
    if (!buildEnvBadge) {
      return null;
    }
    return (
      <Tag color={buildEnvBadge.color} style={{ cursor: 'default', marginInlineEnd: 0 }}>
        {buildEnvBadge.text}
      </Tag>
    );
  }

  const activeBypasses: string[] = [];
  if (data.bypassLicense) activeBypasses.push('Lizenz');
  if (data.bypassNtpCheck) activeBypasses.push('NTP');
  if (data.bypassTseCheck) activeBypasses.push('TSE');

  const items: MenuProps['items'] = [
    { key: 'g', type: 'group', label: 'Entwicklungsmodus Aktiv' },
    { type: 'divider' },
  ];
  if (data.bypassLicense) {
    items.push({ key: 'bl', label: '✓ Lizenzprüfung umgangen' });
  }
  if (data.bypassNtpCheck) {
    items.push({ key: 'bn', label: '✓ NTP-Prüfung umgangen' });
  }
  if (data.bypassTseCheck) {
    items.push({ key: 'bt', label: '✓ TSE-Prüfung umgangen' });
  }
  if (data.simulateOffline) {
    items.push({ key: 'so', label: '⚠ Offline-Simulation' });
  }
  if (data.forceOnline) {
    items.push({ key: 'fo', label: '✓ Online erzwungen' });
  }
  items.push({ type: 'divider' });
  items.push({ key: 'vd', label: `Gültig: ${data.validDays} Tage` });
  items.push({
    key: 'settings',
    label: (
      <Link href="/settings/development-mode">
        <SettingOutlined /> Einstellungen
      </Link>
    ),
  });

  const envLabel = buildEnvBadge?.text ?? '🧪 Entwicklung';

  return (
    <Dropdown
      menu={{ items }}
      trigger={['click']}
      placement="bottomRight"
      overlayClassName="admin-header-dropdown"
      getPopupContainer={getAdminHeaderPopupContainer}
    >
      <Tag color="orange" style={{ cursor: 'pointer', marginInlineEnd: 0 }}>
        {envLabel}
        {activeBypasses.length > 0 ? ` · DEV (${activeBypasses.join(', ')})` : ' · DEV'}
      </Tag>
    </Dropdown>
  );
}
