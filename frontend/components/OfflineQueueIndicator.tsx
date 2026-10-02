import { useRouter } from 'expo-router';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { OfflineQueueIndicatorView } from './OfflineQueueIndicatorView';

import { useOfflineQueueIndicator } from '../hooks/useOfflineQueueIndicator';

export { OfflineQueueIndicatorView } from './OfflineQueueIndicatorView';

export function OfflineQueueIndicator() {
  const { t } = useTranslation(['navigation']);
  const router = useRouter();
  const { color, count } = useOfflineQueueIndicator();

  const labelKey =
    color === 'green'
      ? 'navigation:offlineQueue.indicator.onlineEmpty'
      : color === 'yellow'
        ? 'navigation:offlineQueue.indicator.syncing'
        : color === 'orange'
          ? 'navigation:offlineQueue.indicator.offlineQueued'
          : 'navigation:offlineQueue.indicator.syncFailed';

  return (
    <OfflineQueueIndicatorView
      color={color}
      count={count}
      accessibilityLabel={t(labelKey, { count })}
      onPress={() => {
        router.push('/(screens)/offline-queue');
      }}
    />
  );
}
