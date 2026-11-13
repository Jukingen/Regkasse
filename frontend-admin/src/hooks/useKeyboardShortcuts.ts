'use client';

import { usePathname, useRouter } from 'next/navigation';
import { useCallback, useEffect, useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import {
  DOWNLOAD_HISTORY_PATH,
  GLOBAL_SHORTCUT_DEFINITIONS,
  KEYBOARD_SHORTCUT_EVENTS,
  type ShortcutAction,
  type ShortcutDefinition,
  dispatchShortcutEvent,
  formatShortcutLabel,
  isEditableTarget,
  matchesShortcut,
} from '@/shared/keyboardShortcuts';

const NEW_TENANT_PATH = '/admin/tenants/create';
const PERMISSION_HISTORY_PATH = '/admin/access/permission-history';

function isNewTenantShortcutContext(pathname: string | null): boolean {
  if (!pathname) return false;
  if (pathname === '/admin/tenants') return true;
  if (pathname.startsWith('/admin/tenants/')) {
    return !pathname.includes('/create');
  }
  return false;
}

function navigateIndexForKey(key: string): number | null {
  if (!/^[1-9]$/.test(key)) return null;
  return Number(key) - 1;
}

export type UseKeyboardShortcutsResult = {
  getShortcutLabel: (action: ShortcutAction) => string;
};

/**
 * Global FA power-user shortcuts. Mount once in the protected shell.
 * Page-specific actions (save, tabs, modal close, export) opt in via CustomEvent listeners.
 */
export function useKeyboardShortcuts(): UseKeyboardShortcutsResult {
  const router = useRouter();
  const pathname = usePathname();
  const { logout } = useAuth();
  const { modal } = useAntdApp();
  const { t, textLocale } = useI18n();

  const shortcuts = useMemo(() => GLOBAL_SHORTCUT_DEFINITIONS, []);

  const getShortcutLabel = useCallback(
    (action: ShortcutAction): string => {
      const shortcut = shortcuts.find((item) => item.action === action);
      if (!shortcut) return '';
      return formatShortcutLabel(shortcut, textLocale);
    },
    [shortcuts, textLocale]
  );

  const runAction = useCallback(
    (shortcut: ShortcutDefinition, event: KeyboardEvent) => {
      switch (shortcut.action) {
        case 'openSearch':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.openSearch);
          break;
        case 'newTenant':
          if (!isNewTenantShortcutContext(pathname)) return;
          router.push(NEW_TENANT_PATH);
          break;
        case 'openPermissionHistory':
          router.push(PERMISSION_HISTORY_PATH);
          break;
        case 'save':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.triggerSave);
          break;
        case 'closeModal':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.closeModal);
          break;
        case 'logout':
          modal.confirm({
            title: t('keyboardShortcuts.logoutConfirmTitle'),
            content: t('keyboardShortcuts.logoutConfirmContent'),
            okText: t('keyboardShortcuts.logoutConfirmOk'),
            cancelText: t('common.buttons.cancel'),
            okButtonProps: { danger: true },
            onOk: () => logout({ silent: false, redirectTo: '/login' }),
          });
          break;
        case 'debugMenuPermissions':
          if (process.env.NODE_ENV !== 'development') return;
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.debugMenuPermissions);
          break;
        case 'downloadExport':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.downloadExport);
          break;
        case 'openExportModal':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.openExportModal);
          break;
        case 'openDownloadHistory':
          router.push(DOWNLOAD_HISTORY_PATH);
          break;
        case 'openDownloadPreview':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.openDownloadPreview);
          break;
        case 'openBatchDownload':
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.openBatchDownload);
          break;
        case 'navigate': {
          const index = navigateIndexForKey(event.key);
          if (index === null) return;
          dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.navigateTab, { index });
          break;
        }
        default:
          break;
      }
    },
    [logout, modal, pathname, router, t]
  );

  const handleKeyDown = useCallback(
    (event: KeyboardEvent) => {
      const editable = isEditableTarget(event.target);

      for (const shortcut of shortcuts) {
        if (!matchesShortcut(event, shortcut)) continue;

        if (editable && !shortcut.allowInEditable) {
          continue;
        }

        if (shortcut.action === 'newTenant' && !isNewTenantShortcutContext(pathname)) {
          continue;
        }

        if (
          shortcut.action === 'debugMenuPermissions' &&
          process.env.NODE_ENV !== 'development'
        ) {
          continue;
        }

        event.preventDefault();
        runAction(shortcut, event);
        break;
      }
    },
    [pathname, runAction, shortcuts]
  );

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [handleKeyDown]);

  return { getShortcutLabel };
}
