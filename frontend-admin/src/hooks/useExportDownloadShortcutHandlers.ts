'use client';

import { useCallback, useRef } from 'react';

import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { KEYBOARD_SHORTCUT_EVENTS } from '@/shared/keyboardShortcuts';

export type ExportDownloadShortcutHandlers = {
  /** Ctrl+Shift+D — download active export / confirm download preview. */
  onDownloadExport?: () => void;
  /** Ctrl+Shift+E — open export modal. */
  onOpenExportModal?: () => void;
  /** Ctrl+Shift+P — open preview (file or download preview). */
  onOpenPreview?: () => void;
  /** Ctrl+Shift+B — open / start batch download. */
  onOpenBatchDownload?: () => void;
};

/**
 * Opt-in page handlers for export/download keyboard shortcuts.
 * Global key matching lives in `useKeyboardShortcuts`; this only listens for CustomEvents.
 */
export function useExportDownloadShortcutHandlers(
  handlers: ExportDownloadShortcutHandlers,
  enabled = true
): void {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  const onDownload = useCallback(() => {
    handlersRef.current.onDownloadExport?.();
  }, []);

  const onExportModal = useCallback(() => {
    handlersRef.current.onOpenExportModal?.();
  }, []);

  const onPreview = useCallback(() => {
    handlersRef.current.onOpenPreview?.();
  }, []);

  const onBatch = useCallback(() => {
    handlersRef.current.onOpenBatchDownload?.();
  }, []);

  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.downloadExport, onDownload, enabled);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.openExportModal, onExportModal, enabled);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.openDownloadPreview, onPreview, enabled);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.openBatchDownload, onBatch, enabled);
}
