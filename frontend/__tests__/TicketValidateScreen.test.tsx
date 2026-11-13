import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import React from 'react';

import TicketValidateScreen from '../app/(tabs)/ticket-validate';
import { changeLanguage } from '../i18n';

const mockValidate = jest.fn<(...args: unknown[]) => Promise<unknown>>();
const mockRedeem = jest.fn<(...args: unknown[]) => Promise<unknown>>();

const verticalProfile = {
  current: {
    posFeatures: { ticketScan: true } as Record<string, boolean>,
  },
};

jest.mock('expo-router', () => ({
  useRouter: () => ({ back: jest.fn(), push: jest.fn() }),
}));

jest.mock('../contexts/VerticalProfileContext', () => ({
  useVerticalFeatures: () => verticalProfile.current,
}));

jest.mock('../services/api/ticketService', () => ({
  validatePosTicket: (...args: unknown[]) => mockValidate(...args),
  redeemPosTicket: (...args: unknown[]) => mockRedeem(...args),
}));

jest.mock('../components/BarcodeScannerModal', () => ({
  BarcodeScannerModal: () => null,
}));

jest.mock('@expo/vector-icons', () => {
  const React = require('react');
  const { Text } = require('react-native');
  return {
    Ionicons: (props: { name?: string }) =>
      React.createElement(Text, { accessibilityLabel: props.name }, props.name),
  };
});

describe('ticket-validate screen', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockValidate.mockReset();
    mockRedeem.mockReset();
    verticalProfile.current.posFeatures = { ticketScan: true };
  });

  it('shows valid ticket details and redeems via the API', async () => {
    mockValidate.mockResolvedValue({
      displayCode: 'ABC123',
      status: 'Valid',
      isValid: true,
      canRedeem: true,
    });
    mockRedeem.mockResolvedValue({
      displayCode: 'ABC123',
      status: 'Redeemed',
      isValid: false,
      canRedeem: false,
    });

    await render(<TicketValidateScreen />);
    await fireEvent.changeText(screen.getByLabelText('Ticketcode'), 'TKT-TESTCODE01');
    await fireEvent.press(screen.getByLabelText('Prüfen'));

    expect(await screen.findByText('Gültig')).toBeTruthy();
    await fireEvent.press(screen.getByLabelText('Einlösen'));
    await waitFor(() => {
      expect(mockRedeem).toHaveBeenCalledWith('TKT-TESTCODE01');
    });
  });

  it('shows invalid when validation returns nothing', async () => {
    mockValidate.mockResolvedValue(null);
    await render(<TicketValidateScreen />);
    await fireEvent.changeText(screen.getByLabelText('Ticketcode'), 'NOPE');
    await fireEvent.press(screen.getByLabelText('Prüfen'));
    expect(await screen.findByText('Ungültig')).toBeTruthy();
    expect(mockRedeem).not.toHaveBeenCalled();
  });
});
