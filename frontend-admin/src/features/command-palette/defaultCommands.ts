import type { AppRouterInstance } from 'next/dist/shared/lib/app-router-context.shared-runtime';

import type { CommandItem } from '@/features/command-palette/types';

export type DefaultCommandsContext = {
  t: (key: string) => string;
  router: AppRouterInstance;
  closePalette: () => void;
  triggerBackup: () => void;
};

function navigate(ctx: DefaultCommandsContext, path: string): () => void {
  return () => {
    ctx.closePalette();
    ctx.router.push(path);
  };
}

/**
 * Pinned shortcuts (German labels via i18n) — always listed before extended sidebar pages.
 */
export function buildDefaultCommands(ctx: DefaultCommandsContext): CommandItem[] {
  const { t } = ctx;
  const go = (path: string) => navigate(ctx, path);

  return [
    {
      id: 'page:dashboard',
      type: 'page',
      label: t('nav.reportingDashboard'),
      description: '/dashboard',
      group: 'Navigation',
      keywords: ['home', 'main', 'dashboard', 'start'],
      menuKey: '/dashboard',
      action: go('/dashboard'),
    },
    {
      id: 'page:users',
      type: 'page',
      label: t('nav.users'),
      description: '/admin/users',
      group: 'Navigation',
      keywords: ['users', 'accounts', 'benutzer', 'konten'],
      menuKey: '/admin/users',
      action: go('/admin/users'),
    },
    {
      id: 'page:registers',
      type: 'page',
      label: t('nav.kassenverwaltung'),
      description: '/kassenverwaltung',
      group: 'Navigation',
      keywords: ['registers', 'cash', 'kasse', 'kassen'],
      menuKey: '/kassenverwaltung',
      action: go('/kassenverwaltung'),
    },
    {
      id: 'page:reports',
      type: 'page',
      label: t('nav.reporting'),
      description: '/reporting',
      group: 'Navigation',
      keywords: ['reports', 'analytics', 'berichte', 'auswertung'],
      menuKey: '/reporting',
      action: go('/reporting'),
    },
    {
      id: 'page:report-center',
      type: 'page',
      label: t('nav.reportCenter'),
      description: '/reporting/report-center',
      group: 'Navigation',
      keywords: ['report center', 'berichtszentrum', 'tagesbericht'],
      menuKey: '/reporting/report-center',
      action: go('/reporting/report-center'),
    },
    {
      id: 'page:receipts',
      type: 'page',
      label: t('nav.receipts'),
      description: '/receipts',
      group: 'Navigation',
      keywords: ['receipts', 'belege', 'bons'],
      menuKey: '/receipts',
      action: go('/receipts'),
    },
    {
      id: 'page:backup',
      type: 'page',
      label: t('nav.backupDr'),
      description: '/backup',
      group: 'Navigation',
      keywords: ['backup', 'restore', 'dr', 'sicherung'],
      menuKey: '/backup',
      action: go('/backup'),
    },
    {
      id: 'page:audit',
      type: 'page',
      label: t('nav.auditLogs'),
      description: '/audit-logs',
      group: 'Navigation',
      keywords: ['audit', 'logs', 'protokoll'],
      menuKey: '/audit-logs',
      action: go('/audit-logs'),
    },
    {
      id: 'page:license',
      type: 'page',
      label: t('nav.licenseManagement'),
      description: '/admin/license',
      group: 'Navigation',
      keywords: ['license', 'subscription', 'lizenz'],
      menuKey: '/admin/license',
      action: go('/admin/license'),
    },
    {
      id: 'action:create-user',
      type: 'action',
      label: t('adminShell.commandPalette.action.createUser'),
      description: '/admin/users?create=1',
      group: 'Actions',
      keywords: ['create user', 'add', 'neu', 'benutzer', 'anlegen'],
      menuKey: '/admin/users',
      action: go('/admin/users?create=1'),
    },
    {
      id: 'action:create-platform-user',
      type: 'action',
      label: t('adminShell.commandPalette.action.createPlatformUser'),
      description: '/admin/users?create=1&platform=1',
      group: 'Actions',
      keywords: ['platform admin', 'super', 'plattform'],
      menuKey: '/admin/users',
      action: go('/admin/users?create=1&platform=1'),
    },
    {
      id: 'action:create-register',
      type: 'action',
      label: t('adminShell.commandPalette.action.createRegister'),
      description: '/kassenverwaltung?create=1',
      group: 'Actions',
      keywords: ['create register', 'kasse', 'neue kasse'],
      menuKey: '/kassenverwaltung',
      action: go('/kassenverwaltung?create=1'),
    },
    {
      id: 'action:trigger-backup',
      type: 'action',
      label: t('adminShell.commandPalette.action.triggerBackup'),
      description: t('adminShell.commandPalette.action.triggerBackupHint'),
      group: 'Actions',
      keywords: ['backup now', 'trigger', 'jetzt', 'starten'],
      menuKey: '/backup',
      action: () => {
        ctx.closePalette();
        ctx.triggerBackup();
      },
    },
    {
      id: 'action:generate-report',
      type: 'action',
      label: t('adminShell.commandPalette.action.generateReport'),
      description: '/reporting/report-center',
      group: 'Actions',
      keywords: ['report generate', 'bericht', 'erstellen', 'generieren'],
      menuKey: '/reporting/report-center',
      action: go('/reporting/report-center'),
    },
  ];
}

/** Hints shown when the query is empty (not selectable). */
export function buildDynamicSearchPlaceholders(t: (key: string) => string): CommandItem[] {
  return [
    {
      id: 'dynamic:users',
      type: 'user',
      label: t('adminShell.commandPalette.dynamic.searchUsers'),
      keywords: [],
      action: () => {},
      dynamic: true,
    },
    {
      id: 'dynamic:receipts',
      type: 'receipt',
      label: t('adminShell.commandPalette.dynamic.searchReceipts'),
      keywords: [],
      action: () => {},
      dynamic: true,
    },
    {
      id: 'dynamic:registers',
      type: 'register',
      label: t('adminShell.commandPalette.dynamic.searchRegisters'),
      keywords: [],
      action: () => {},
      dynamic: true,
    },
  ];
}
