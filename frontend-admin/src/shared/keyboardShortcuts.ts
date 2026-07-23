/**
 * FA keyboard shortcut event names and pure matching helpers.
 * Global dispatch lives in `useKeyboardShortcuts`; pages opt in via listeners.
 */

export const KEYBOARD_SHORTCUT_EVENTS = {
  openSearch: 'regkasse:openSearch',
  triggerSave: 'regkasse:triggerSave',
  closeModal: 'regkasse:closeModal',
  navigateTab: 'regkasse:navigateTab',
  openShortcutsHelp: 'regkasse:openShortcutsHelp',
  debugMenuPermissions: 'regkasse:debugMenuPermissions',
  /** Confirm / download the active page export (or open download-preview confirm). */
  downloadExport: 'regkasse:downloadExport',
  /** Open the page export modal. */
  openExportModal: 'regkasse:openExportModal',
  /** Open download history route (handled globally). */
  openDownloadHistory: 'regkasse:openDownloadHistory',
  /** Open file / download preview on the active page. */
  openDownloadPreview: 'regkasse:openDownloadPreview',
  /** Open batch download UI on the active page. */
  openBatchDownload: 'regkasse:openBatchDownload',
} as const;

export type KeyboardShortcutEventName =
  (typeof KEYBOARD_SHORTCUT_EVENTS)[keyof typeof KEYBOARD_SHORTCUT_EVENTS];

export type ShortcutAction =
  | 'openSearch'
  | 'newTenant'
  | 'save'
  | 'closeModal'
  | 'logout'
  | 'navigate'
  | 'debugMenuPermissions'
  | 'openPermissionHistory'
  | 'downloadExport'
  | 'openExportModal'
  | 'openDownloadHistory'
  | 'openDownloadPreview'
  | 'openBatchDownload';

export type ShortcutDefinition = {
  key: string;
  ctrl?: boolean;
  shift?: boolean;
  alt?: boolean;
  action: ShortcutAction;
  /** When true, shortcut may fire while focus is in an editable field. */
  allowInEditable?: boolean;
};

export type NavigateTabDetail = {
  index: number;
};

export function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return (
    tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || Boolean(target.isContentEditable)
  );
}

export function matchesShortcut(
  event: Pick<KeyboardEvent, 'key' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
  shortcut: Pick<ShortcutDefinition, 'key' | 'ctrl' | 'shift' | 'alt'>
): boolean {
  const eventKey = event.key.length === 1 ? event.key.toLowerCase() : event.key;
  const shortcutKey = shortcut.key.length === 1 ? shortcut.key.toLowerCase() : shortcut.key;
  if (eventKey !== shortcutKey) return false;

  const wantsCtrl = Boolean(shortcut.ctrl);
  const hasCtrlOrMeta = event.ctrlKey || event.metaKey;
  if (wantsCtrl !== hasCtrlOrMeta) return false;

  if (Boolean(shortcut.shift) !== event.shiftKey) return false;
  if (Boolean(shortcut.alt) !== event.altKey) return false;

  return true;
}

/** Platform-aware label (Ctrl/Strg vs ⌘). Pass `locale` for German Strg. */
export function formatShortcutLabel(
  parts: {
    ctrl?: boolean;
    shift?: boolean;
    alt?: boolean;
    key: string;
  },
  locale?: string | null
): string {
  const isMac =
    typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/i.test(navigator.platform);
  const isDe = (locale ?? '').toLowerCase().startsWith('de');
  const labels: string[] = [];
  if (parts.ctrl) labels.push(isMac ? '⌘' : isDe ? 'Strg' : 'Ctrl');
  if (parts.shift) labels.push(isMac ? '⇧' : 'Shift');
  if (parts.alt) labels.push(isMac ? '⌥' : 'Alt');
  if (parts.key.length === 1) {
    labels.push(parts.key.toUpperCase());
  } else if (parts.key === 'Escape') {
    labels.push('Esc');
  } else {
    labels.push(parts.key);
  }
  return labels.join(isMac ? '' : '+');
}

/** Tooltip / button title: `Action (Ctrl+Shift+D)`. */
export function formatActionWithShortcut(actionLabel: string, shortcutLabel: string): string {
  const action = actionLabel.trim();
  const shortcut = shortcutLabel.trim();
  if (!shortcut) return action;
  if (!action) return shortcut;
  return `${action} (${shortcut})`;
}

/** Platform-aware label for Ctrl/Cmd+1–9 tab navigation. */
export function formatNavigateTabsShortcutLabel(locale?: string | null): string {
  const first = formatShortcutLabel({ ctrl: true, key: '1' }, locale);
  return first.replace(/1$/, '1–9');
}

export function dispatchShortcutEvent(name: KeyboardShortcutEventName, detail?: unknown): void {
  if (typeof document === 'undefined') return;
  document.dispatchEvent(new CustomEvent(name, { detail }));
}

export const GLOBAL_SHORTCUT_DEFINITIONS: ShortcutDefinition[] = [
  { key: 'k', ctrl: true, action: 'openSearch' },
  { key: 'n', ctrl: true, action: 'newTenant' },
  { key: 'h', ctrl: true, action: 'openPermissionHistory' },
  { key: 's', ctrl: true, action: 'save', allowInEditable: true },
  { key: 'Escape', action: 'closeModal', allowInEditable: true },
  { key: 'l', ctrl: true, shift: true, action: 'logout' },
  { key: 'd', ctrl: true, shift: true, action: 'downloadExport' },
  { key: 'e', ctrl: true, shift: true, action: 'openExportModal' },
  { key: 'h', ctrl: true, shift: true, action: 'openDownloadHistory' },
  { key: 'p', ctrl: true, shift: true, action: 'openDownloadPreview' },
  { key: 'b', ctrl: true, shift: true, action: 'openBatchDownload' },
  /** Dev-only menu permission debug — Alt avoids clashing with preview (Ctrl+Shift+P). */
  { key: 'p', ctrl: true, shift: true, alt: true, action: 'debugMenuPermissions' },
  ...Array.from({ length: 9 }, (_, i) => ({
    key: String(i + 1),
    ctrl: true,
    action: 'navigate' as const,
  })),
];

export const DOWNLOAD_HISTORY_PATH = '/admin/download-history';
