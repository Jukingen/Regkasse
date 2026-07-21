'use client';

/**
 * Primary save action with Ctrl/Cmd+S via `regkasse:triggerSave`.
 * Prefer this over ad-hoc shortcut listeners on individual forms.
 */
import { Button, type ButtonProps } from 'antd';
import { useCallback, useRef } from 'react';

import { useKeyboardShortcutLabels } from '@/components/KeyboardShortcutsProvider';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useI18n } from '@/i18n';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

export type SaveButtonProps = {
  onClick?: () => void;
  loading?: boolean;
  disabled?: boolean;
  /** When false, Ctrl/Cmd+S does not trigger this button. Default true. */
  shortcutEnabled?: boolean;
  /**
   * When true (default), label includes the shortcut (e.g. "Save (Ctrl+S)").
   * Set false when the parent already shows a custom label without shortcut text.
   */
  showShortcutInLabel?: boolean;
  children?: React.ReactNode;
  className?: string;
  icon?: ButtonProps['icon'];
  htmlType?: ButtonProps['htmlType'];
  size?: ButtonProps['size'];
  block?: ButtonProps['block'];
};

export function SaveButton({
  onClick,
  loading = false,
  disabled = false,
  shortcutEnabled = true,
  showShortcutInLabel = true,
  children,
  className,
  icon,
  htmlType = 'button',
  size,
  block,
}: SaveButtonProps) {
  const { t } = useI18n();
  const { getShortcutLabel } = useKeyboardShortcutLabels();
  const buttonRef = useRef<HTMLButtonElement>(null);

  const shortcutLabel = getShortcutLabel('save');
  const title = t('keyboardShortcuts.saveWithShortcut', {
    shortcut: shortcutLabel || 'Ctrl+S',
  });

  const label = children ?? (showShortcutInLabel ? title : t('common.buttons.save'));

  const handleShortcutSave = useCallback(() => {
    if (disabled || loading) return;
    if (onClick) {
      onClick();
      return;
    }
    // Native click submits Ant Design Form when htmlType="submit".
    buttonRef.current?.click();
  }, [disabled, loading, onClick]);

  useKeyboardShortcutListener(
    KEYBOARD_SHORTCUT_EVENTS.triggerSave,
    handleShortcutSave,
    shortcutEnabled && !disabled && !loading
  );

  return (
    <Button
      ref={buttonRef}
      type="primary"
      htmlType={htmlType}
      onClick={onClick}
      loading={loading}
      disabled={disabled}
      className={['save-button', className].filter(Boolean).join(' ')}
      icon={icon}
      size={size}
      block={block}
      title={title}
    >
      {label}
    </Button>
  );
}
