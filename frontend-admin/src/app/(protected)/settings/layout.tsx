'use client';

// Shared shell for the /settings route group: secondary nav + IA info panel.

import React from 'react';
import { usePathname } from 'next/navigation';
import { SettingsHubContextPanel } from '@/features/settings/components/SettingsHubContextPanel';
import { SettingsSecondaryNav } from '@/features/settings/components/SettingsSecondaryNav';
import { isBackupAreaPath } from '@/shared/backupAreaRoutes';

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const showSettingsHub = !isBackupAreaPath(pathname);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {showSettingsHub ? <SettingsHubContextPanel /> : null}
      {showSettingsHub ? <SettingsSecondaryNav /> : null}
      {children}
    </div>
  );
}
