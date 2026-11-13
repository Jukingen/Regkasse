import { describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';

import { OfflineQueuePanel } from '../components/OfflineQueuePanel';
import { changeLanguage } from '../i18n';
import type { OfflineQueueListItem } from '../services/offline/offlineQueueSnapshot';

const items: OfflineQueueListItem[] = [
  {
    id: 'order-1',
    source: 'order',
    timestamp: '2026-09-30T10:00:00.000Z',
    amount: 12.5,
    status: 'pending',
    canRetry: true,
    canDelete: false,
  },
  {
    id: 'tx-non-fiscal',
    source: 'transaction',
    timestamp: '2026-09-30T10:05:00.000Z',
    amount: 4,
    status: 'failed',
    canRetry: true,
    canDelete: true,
    lastError: 'sync_transport_failed',
  },
];

describe('OfflineQueuePanel', () => {
  it('lists pending transactions with id, amount, and status', async () => {
    await changeLanguage('de');
    const onRetry = jest.fn();
    const onDelete = jest.fn();
    const onRetryAll = jest.fn();

    await render(
      <OfflineQueuePanel
        items={items}
        lastSuccessfulSyncAt={new Date('2026-09-30T09:00:00.000Z')}
        nextRetryAt={new Date('2026-09-30T10:10:00.000Z')}
        now={new Date('2026-09-30T10:09:30.000Z')}
        onRetry={onRetry}
        onDelete={onDelete}
        onRetryAll={onRetryAll}
      />
    );

    expect(await screen.findByTestId('offline-queue-panel')).toBeTruthy();
    expect(screen.getByText('order-1')).toBeTruthy();
    expect(screen.getByText('tx-non-fiscal')).toBeTruthy();
    expect(screen.getByText('€ 12.50')).toBeTruthy();
    expect(screen.getByText('€ 4.00')).toBeTruthy();
    expect(screen.getByText('In Warteschlange')).toBeTruthy();
    expect(screen.getByText('Fehlgeschlagen')).toBeTruthy();
    expect(screen.getByLabelText('Alle erneut versuchen')).toBeTruthy();
    expect(screen.getAllByLabelText('Erneut senden')).toHaveLength(2);
    expect(screen.getByLabelText('Löschen')).toBeTruthy();
    expect(
      screen.getByText('0 von 50 Offline-Transaktionen verwendet')
    ).toBeTruthy();
  });

  it.each([
    { current: 0, tone: 'green' },
    { current: 40, tone: 'yellow' },
    { current: 50, tone: 'red' },
  ] as const)(
    'shows the tenant cap bar at $current of 50 as $tone',
    async ({ current, tone }) => {
      await changeLanguage('de');
      await render(
        <OfflineQueuePanel
          items={[]}
          lastSuccessfulSyncAt={null}
          nextRetryAt={null}
          offlineUsed={current}
          offlineLimit={50}
          onRetry={jest.fn()}
          onDelete={jest.fn()}
          onRetryAll={jest.fn()}
        />
      );

      expect(
        screen.getByText(`${current} von 50 Offline-Transaktionen verwendet`)
      ).toBeTruthy();
      expect(screen.getByTestId(`offline-queue-limit-fill-${tone}`)).toBeTruthy();
    }
  );
});
