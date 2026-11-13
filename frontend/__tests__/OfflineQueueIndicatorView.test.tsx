import { describe, expect, it } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { OfflineQueueIndicatorView } from '../components/OfflineQueueIndicatorView';
import { changeLanguage } from '../i18n';

describe('OfflineQueueIndicatorView', () => {
  it('shows a green dot when online and the queue is empty', async () => {
    await changeLanguage('de');
    await render(
      <OfflineQueueIndicatorView
        color="green"
        count={0}
        accessibilityLabel="Online, Warteschlange leer"
        onPress={() => undefined}
      />
    );
    expect(screen.getByTestId('offline-queue-dot-green')).toBeTruthy();
    expect(screen.getByLabelText('Online, Warteschlange leer')).toBeTruthy();
  });

  it('shows a yellow dot when items are syncing', async () => {
    await render(
      <OfflineQueueIndicatorView
        color="yellow"
        count={3}
        accessibilityLabel="Online, 3 Einträge werden synchronisiert"
        onPress={() => undefined}
      />
    );
    expect(screen.getByTestId('offline-queue-dot-yellow')).toBeTruthy();
    expect(screen.getByText('3')).toBeTruthy();
  });

  it('shows an orange dot when offline with queued items', async () => {
    await render(
      <OfflineQueueIndicatorView
        color="orange"
        count={4}
        accessibilityLabel="Offline, 4 Einträge in der Warteschlange"
        onPress={() => undefined}
      />
    );
    expect(screen.getByTestId('offline-queue-dot-orange')).toBeTruthy();
  });

  it('shows a red dot when sync failed and items are stuck', async () => {
    await render(
      <OfflineQueueIndicatorView
        color="red"
        count={2}
        accessibilityLabel="Synchronisation fehlgeschlagen, 2 Einträge blockiert"
        onPress={() => undefined}
      />
    );
    expect(screen.getByTestId('offline-queue-dot-red')).toBeTruthy();
    expect(screen.getByLabelText('Synchronisation fehlgeschlagen, 2 Einträge blockiert')).toBeTruthy();
  });
});
