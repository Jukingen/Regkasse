import { useEffect, useRef } from 'react';
import { Platform } from 'react-native';

import {
  isEditableTarget,
  resolvePosShortcut,
  type PosShortcutAction,
} from '../utils/posKeyboardShortcuts';

/**
 * A handler counts as "handled" unless it returns `false`.
 * Unhandled keys (no handler, or `false`) keep their browser default.
 */
export type PosShortcutHandlers = Partial<Record<PosShortcutAction, () => boolean | void>>;

/**
 * Registers POS keyboard shortcuts on web. No-op on native or when `enabled` is false.
 * Only actions with a handler are intercepted, so F5 reload still works where F5 is not mapped.
 */
export function usePosKeyboardShortcuts(handlers: PosShortcutHandlers, enabled = true): void {
  const handlersRef = useRef(handlers);

  // Keep the newest handlers without re-registering the DOM listener on every render.
  useEffect(() => {
    handlersRef.current = handlers;
  });

  useEffect(() => {
    if (!enabled || Platform.OS !== 'web' || typeof document === 'undefined') return undefined;

    const onKeyDown = (event: KeyboardEvent) => {
      const action = resolvePosShortcut(event, {
        editableTarget: isEditableTarget(event.target),
      });
      if (!action) return;

      const handler = handlersRef.current[action];
      if (!handler) return;

      if (handler() === false) return;
      event.preventDefault();
    };

    const doc = document;
    doc.addEventListener('keydown', onKeyDown);
    return () => {
      doc.removeEventListener('keydown', onKeyDown);
    };
  }, [enabled]);
}
