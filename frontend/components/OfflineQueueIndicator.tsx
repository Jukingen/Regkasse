import { useRouter } from 'expo-router';
import React, { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

import { OfflineQueueIndicatorView } from './OfflineQueueIndicatorView';

import { useOfflineQueueIndicator } from '../hooks/useOfflineQueueIndicator';

export { OfflineQueueIndicatorView } from './OfflineQueueIndicatorView';

export function OfflineQueueIndicator({
  onAtCapChange,
}: {
  /** Single poll owner for the tab shell. Parent reads the cap without a second hook. */
  onAtCapChange?: (atCap: boolean) => void;
}) {
  const { t } = useTranslation(['navigation']);
  const router = useRouter();
  const { color, count, atCap } = useOfflineQueueIndicator();

  useEffect(() => {
    onAtCapChange?.(atCap);
  }, [atCap, onAtCapChange]);

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
