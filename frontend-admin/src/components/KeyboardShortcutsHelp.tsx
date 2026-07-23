'use client';

/**
 * Help modal listing FA power-user keyboard shortcuts.
 * Mount once in the protected shell (`layout.tsx`). Open via header trigger,
 * user menu, or `regkasse:openShortcutsHelp`.
 */
import { QuestionCircleOutlined } from '@ant-design/icons';
import { Button, Modal, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useCallback, useMemo, useState } from 'react';

import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useI18n } from '@/i18n';
import {
  KEYBOARD_SHORTCUT_EVENTS,
  dispatchShortcutEvent,
  formatNavigateTabsShortcutLabel,
  formatShortcutLabel,
} from '@/shared/keyboardShortcuts';

type ShortcutHelpRow = {
  key: string;
  shortcut: string;
  action: string;
};

export type KeyboardShortcutsHelpProps = {
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
  /** When true, render a trigger button. Default true. */
  showTrigger?: boolean;
  /** Icon-only trigger (header toolbar). Ignored when showTrigger is false. */
  iconOnly?: boolean;
  triggerClassName?: string;
  triggerSize?: 'small' | 'middle' | 'large';
};

type HelpActionId =
  | 'search'
  | 'newTenant'
  | 'openPermissionHistory'
  | 'save'
  | 'closeModal'
  | 'logout'
  | 'navigate'
  | 'downloadExport'
  | 'openExportModal'
  | 'openDownloadHistory'
  | 'openDownloadPreview'
  | 'openBatchDownload';

const HELP_ACTIONS: Array<{
  id: HelpActionId;
  parts?: { ctrl?: boolean; shift?: boolean; alt?: boolean; key: string };
  navigateTabs?: boolean;
}> = [
  { id: 'search', parts: { ctrl: true, key: 'k' } },
  { id: 'newTenant', parts: { ctrl: true, key: 'n' } },
  { id: 'openPermissionHistory', parts: { ctrl: true, key: 'h' } },
  { id: 'save', parts: { ctrl: true, key: 's' } },
  { id: 'closeModal', parts: { key: 'Escape' } },
  { id: 'logout', parts: { ctrl: true, shift: true, key: 'l' } },
  { id: 'navigate', navigateTabs: true },
  { id: 'downloadExport', parts: { ctrl: true, shift: true, key: 'd' } },
  { id: 'openExportModal', parts: { ctrl: true, shift: true, key: 'e' } },
  { id: 'openDownloadPreview', parts: { ctrl: true, shift: true, key: 'p' } },
  { id: 'openDownloadHistory', parts: { ctrl: true, shift: true, key: 'h' } },
  { id: 'openBatchDownload', parts: { ctrl: true, shift: true, key: 'b' } },
];

/** Opens the shell-mounted shortcuts help modal. */
export function openKeyboardShortcutsHelp(): void {
  dispatchShortcutEvent(KEYBOARD_SHORTCUT_EVENTS.openShortcutsHelp);
}

export function KeyboardShortcutsHelp({
  open: controlledOpen,
  onOpenChange,
  showTrigger = true,
  iconOnly = false,
  triggerClassName,
  triggerSize = 'small',
}: KeyboardShortcutsHelpProps) {
  const { t, textLocale } = useI18n();
  const [uncontrolledOpen, setUncontrolledOpen] = useState(false);

  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : uncontrolledOpen;

  const setOpen = useCallback(
    (next: boolean) => {
      if (!isControlled) setUncontrolledOpen(next);
      onOpenChange?.(next);
    },
    [isControlled, onOpenChange]
  );

  const openHelp = useCallback(() => setOpen(true), [setOpen]);
  const close = useCallback(() => setOpen(false), [setOpen]);

  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.openShortcutsHelp, openHelp);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.closeModal, close, open);

  const rows: ShortcutHelpRow[] = useMemo(
    () =>
      HELP_ACTIONS.map((item) => ({
        key: item.id,
        shortcut: item.navigateTabs
          ? formatNavigateTabsShortcutLabel(textLocale)
          : formatShortcutLabel(item.parts!, textLocale),
        action: t(`keyboardShortcuts.${item.id}`),
      })),
    [t, textLocale]
  );

  const columns: ColumnsType<ShortcutHelpRow> = useMemo(
    () => [
      {
        title: t('keyboardShortcuts.columnAction'),
        dataIndex: 'action',
        key: 'action',
      },
      {
        title: t('keyboardShortcuts.columnShortcut'),
        dataIndex: 'shortcut',
        key: 'shortcut',
        width: 160,
        render: (value: string) => <kbd className="keyboard-shortcuts-help-kbd">{value}</kbd>,
      },
    ],
    [t]
  );

  const openLabel = t('keyboardShortcuts.help');

  return (
    <>
      {showTrigger ? (
        <Button
          type="default"
          size={triggerSize}
          icon={<QuestionCircleOutlined />}
          className={triggerClassName}
          onClick={openHelp}
          aria-label={openLabel}
          title={openLabel}
        >
          {iconOnly ? null : openLabel}
        </Button>
      ) : null}

      <Modal
        title={t('keyboardShortcuts.title')}
        open={open}
        onCancel={close}
        footer={null}
        width={560}
        destroyOnHidden
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          {t('keyboardShortcuts.sectionExport')}
        </Typography.Paragraph>
        <Table<ShortcutHelpRow>
          dataSource={rows}
          columns={columns}
          pagination={false}
          size="middle"
          rowKey="key"
        />
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          {t('keyboardShortcuts.inputHint')}
        </Typography.Paragraph>
      </Modal>
    </>
  );
}
