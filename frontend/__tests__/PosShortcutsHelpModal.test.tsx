import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { PosShortcutsHelpModal } from '../components/PosShortcutsHelpModal';
import { changeLanguage } from '../i18n';

describe('PosShortcutsHelpModal', () => {
  beforeEach(async () => {
    await changeLanguage('de');
  });

  it('lists only the shortcuts that exist on this screen, in German', async () => {
    await render(
      <PosShortcutsHelpModal
        visible
        onClose={jest.fn()}
        available={['help', 'customer', 'clearCart', 'pay', 'escape']}
      />
    );

    expect(screen.getByText('Tastenkürzel')).toBeTruthy();
    expect(screen.getByText('F6')).toBeTruthy();
    expect(screen.getByText('Bezahlen')).toBeTruthy();
    expect(screen.getByText('Kunde wählen')).toBeTruthy();
    expect(screen.getByText('Warenkorb leeren')).toBeTruthy();
    expect(screen.getByText('Dialog schließen')).toBeTruthy();
    expect(screen.queryByTestId('pos-shortcut-kitchen')).toBeNull();
    expect(screen.queryByTestId('pos-shortcut-receipts')).toBeNull();
  });

  it('closes from the close button', async () => {
    const onClose = jest.fn();
    await render(<PosShortcutsHelpModal visible onClose={onClose} available={['help']} />);

    await fireEvent.press(screen.getAllByLabelText('Schließen')[1]);

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('renders nothing while hidden', async () => {
    await render(<PosShortcutsHelpModal visible={false} onClose={jest.fn()} available={['help']} />);

    expect(screen.queryByTestId('pos-shortcuts-help')).toBeNull();
  });
});
