import { describe, expect, it, jest } from '@jest/globals';
import { fireEvent, render, screen } from '@testing-library/react-native';
import React from 'react';

import { OfflineLimitReachedModal } from '../components/OfflineLimitReachedModal';
import { shouldShowOfflineLimitReachedModal } from '../utils/offlineQueueLimit';

const copy = {
  title: 'Offline-Limit erreicht',
  body: 'Offline-Limit erreicht. Verkäufe blockiert, bis die Synchronisierung abgeschlossen ist.',
  retry: 'Jetzt synchronisieren',
  cancel: 'Abbrechen',
};

describe('OfflineLimitReachedModal', () => {
  it('appears at 100% of the tenant offline cap', async () => {
    expect(shouldShowOfflineLimitReachedModal({ limitReached: true })).toBe(true);

    await render(
      <OfflineLimitReachedModal
        visible
        title={copy.title}
        body={copy.body}
        retryLabel={copy.retry}
        cancelLabel={copy.cancel}
        onRetryNow={() => undefined}
        onCancel={() => undefined}
      />
    );

    expect(screen.getByTestId('offline-limit-reached-modal')).toBeTruthy();
    expect(screen.getByText(copy.title)).toBeTruthy();
    expect(screen.getByText(copy.body)).toBeTruthy();
    expect(screen.getByTestId('offline-limit-retry')).toBeTruthy();
    expect(screen.getByTestId('offline-limit-cancel')).toBeTruthy();
  });

  it('appears on HTTP 409 LIMIT_EXCEEDED for the offline queue', async () => {
    expect(
      shouldShowOfflineLimitReachedModal({
        status: 409,
        error: 'LIMIT_EXCEEDED',
        limitKey: 'maxOfflineTransactions',
      })
    ).toBe(true);

    const onRetryNow = jest.fn();
    const onCancel = jest.fn();
    await render(
      <OfflineLimitReachedModal
        visible={shouldShowOfflineLimitReachedModal({
          status: 409,
          error: 'LIMIT_EXCEEDED',
          limitKey: 'maxOfflineTransactions',
        })}
        title={copy.title}
        body={copy.body}
        retryLabel={copy.retry}
        cancelLabel={copy.cancel}
        onRetryNow={onRetryNow}
        onCancel={onCancel}
      />
    );

    expect(screen.getByTestId('offline-limit-reached-modal')).toBeTruthy();
    fireEvent.press(screen.getByTestId('offline-limit-retry'));
    fireEvent.press(screen.getByTestId('offline-limit-cancel'));
    expect(onRetryNow).toHaveBeenCalledTimes(1);
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('does not render the blocking copy when hidden', async () => {
    await render(
      <OfflineLimitReachedModal
        visible={false}
        title={copy.title}
        body={copy.body}
        retryLabel={copy.retry}
        cancelLabel={copy.cancel}
      />
    );

    expect(screen.queryByTestId('offline-limit-reached-modal')).toBeNull();
  });
});
