/**
 * POS keyboard shortcuts (web only).
 *
 * F1 help, F2 customer, F3 receipt list, F4 kitchen, F5 clear cart, F6 pay,
 * Esc closes the top dialog, ? opens the shortcut overview.
 * The mapping is pure so it can be tested without a DOM.
 */

export type PosShortcutAction =
  | 'help'
  | 'customer'
  | 'receipts'
  | 'kitchen'
  | 'clearCart'
  | 'pay'
  | 'escape';

export type PosKeyEventLike = {
  key: string;
  ctrlKey?: boolean;
  metaKey?: boolean;
  altKey?: boolean;
  shiftKey?: boolean;
  repeat?: boolean;
  isComposing?: boolean;
};

/** Function keys in display order. */
export const POS_FUNCTION_KEYS: readonly { key: string; action: PosShortcutAction }[] = [
  { key: 'F1', action: 'help' },
  { key: 'F2', action: 'customer' },
  { key: 'F3', action: 'receipts' },
  { key: 'F4', action: 'kitchen' },
  { key: 'F5', action: 'clearCart' },
  { key: 'F6', action: 'pay' },
];

const FUNCTION_KEY_ACTIONS = new Map(POS_FUNCTION_KEYS.map((entry) => [entry.key, entry.action]));

/** True when typing would be hijacked (input, textarea, select, contenteditable). */
export function isEditableTarget(target: unknown): boolean {
  if (!target || typeof target !== 'object') return false;
  const el = target as { tagName?: unknown; isContentEditable?: unknown };
  if (el.isContentEditable === true) return true;
  if (typeof el.tagName !== 'string') return false;
  const tag = el.tagName.toUpperCase();
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
}

/**
 * Maps a keydown event to a POS action. Returns null for anything we must leave to the browser:
 * modifier combos (Ctrl+R, Cmd+F5, Shift+F5), auto-repeat, IME composition, and `?` while typing.
 */
export function resolvePosShortcut(
  event: PosKeyEventLike,
  options: { editableTarget: boolean }
): PosShortcutAction | null {
  if (event.ctrlKey || event.metaKey || event.altKey) return null;
  if (event.repeat || event.isComposing) return null;

  if (event.key === 'Escape') return 'escape';

  const functionAction = FUNCTION_KEY_ACTIONS.get(event.key);
  if (functionAction) {
    return event.shiftKey ? null : functionAction;
  }

  if (event.key === '?' && !options.editableTarget) return 'help';

  return null;
}
