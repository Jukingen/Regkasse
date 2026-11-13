import { describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { BusyButton } from '../components/BusyButton';

describe('BusyButton', () => {
  it('shows a spinner and ignores presses while the request is in flight', async () => {
    const onPress = jest.fn();
    await render(<BusyButton busy label="Speichern" onPress={onPress} />);

    expect(screen.getByTestId('busy-button-spinner')).toBeTruthy();
    fireEvent.press(screen.getByRole('button'));
    expect(onPress).not.toHaveBeenCalled();
  });

  it('presses when the request is idle', async () => {
    const onPress = jest.fn();
    await render(<BusyButton busy={false} label="Speichern" onPress={onPress} />);

    expect(screen.queryByTestId('busy-button-spinner')).toBeNull();
    fireEvent.press(screen.getByRole('button'));
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
