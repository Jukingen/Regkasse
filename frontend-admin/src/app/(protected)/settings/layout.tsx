'use client';

// Shared shell for the /settings route group: secondary nav + IA info panel.

import React from 'react';
import { SettingsHubContextPanel } from '@/features/settings/components/SettingsHubContextPanel';
import { SettingsSecondaryNav } from '@/features/settings/components/SettingsSecondaryNav';

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <SettingsHubContextPanel />
      <SettingsSecondaryNav />
      {children}
    </div>
  );
}
