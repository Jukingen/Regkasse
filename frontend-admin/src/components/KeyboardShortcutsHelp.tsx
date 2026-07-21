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

const HELP_ACTIONS: Array<{
  id: 'search' | 'newTenant' | 'save' | 'closeModal' | 'logout' | 'navigate';
  shortcut: () => string;
}> = [
  {
    id: 'search',
    shortcut: () => formatShortcutLabel({ ctrl: true, key: 'k' }),
  },
  {
    id: 'newTenant',
    shortcut: () => formatShortcutLabel({ ctrl: true, key: 'n' }),
  },
  {
    id: 'save',
    shortcut: () => formatShortcutLabel({ ctrl: true, key: 's' }),
  },
  {
    id: 'closeModal',
    shortcut: () => formatShortcutLabel({ key: 'Escape' }),
  },
  {
    id: 'logout',
    shortcut: () => formatShortcutLabel({ ctrl: true, shift: true, key: 'l' }),
  },
  {
    id: 'navigate',
    shortcut: () => formatNavigateTabsShortcutLabel(),
  },
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
  const { t } = useI18n();
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
        shortcut: item.shortcut(),
        action: t(`keyboardShortcuts.${item.id}`),
      })),
    [t]
  );

  const columns: ColumnsType<ShortcutHelpRow> = useMemo(
    () => [
      {
        title: t('keyboardShortcuts.columnShortcut'),
        dataIndex: 'shortcut',
        key: 'shortcut',
        width: 140,
        render: (value: string) => <kbd className="keyboard-shortcuts-help-kbd">{value}</kbd>,
      },
      {
        title: t('keyboardShortcuts.columnAction'),
        dataIndex: 'action',
        key: 'action',
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
        width={520}
        destroyOnHidden
      >
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
