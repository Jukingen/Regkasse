import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

const source = fs.readFileSync(path.join(__dirname, '../app/(tabs)/cash-register.tsx'), 'utf8');

describe('cash register keyboard shortcuts wiring', () => {
  it('registers the shortcuts only while the screen is focused and no payment modal is open', () => {
    expect(source).toContain(
      'usePosKeyboardShortcuts(shortcutHandlers, screenFocused && !isPaymentModalVisible)'
    );
  });

  it('keeps F6 away from a payment while a sheet or picker is open', () => {
    expect(source).toContain('if (shortcutOverlayOpen) return { escape: closeTopOverlay };');
  });

  it('confirms before F5 clears the cart', () => {
    expect(source).toContain('handleClearCartShortcut');
    expect(source).toContain('checkout:posFlow.shortcuts.clearCartConfirmTitle');
  });

  it('mounts the cheat sheet and the header entry point', () => {
    expect(source).toContain('<PosShortcutsHelpModal');
    expect(source).toContain('onOpenShortcutHelp=');
  });

  it('shows a spinner on the kitchen button while sending', () => {
    expect(source).toContain('<BusyButton');
    expect(source).toContain('busy={sendingToKitchen === true}');
  });
});
