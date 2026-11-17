import { test } from '@playwright/test';

/**
 * Requested flow: F1 opens the payment modal and Escape closes it.
 * FA shortcuts are Ctrl/Cmd combinations plus Escape (`shared/keyboardShortcuts.ts`).
 * POS has no F1 listener on the payment modal.
 */
test.skip('F1 opens the payment modal and Escape closes it', () => {
  // Not implemented. Re-enable when a payment-modal F1 binding exists.
});
