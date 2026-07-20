import { Redirect, useRootNavigationState } from 'expo-router';
import { View } from 'react-native';

import { WaveLoader } from '../src/components/common/WaveLoader';

/**
 * App entry:
 * - Default (POS): → login
 * - Shared customer surface: EXPO_PUBLIC_APP_SURFACE=customer → /customer
 *   (single RN binary for all tenants; slug from storage / QR / deep link)
 */
export default function Index() {
  const rootNavigationState = useRootNavigationState();
  const isCustomerSurface =
    (process.env.EXPO_PUBLIC_APP_SURFACE ?? '').trim().toLowerCase() === 'customer';

  if (!rootNavigationState?.key) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <WaveLoader size={32} color="#007AFF" />
      </View>
    );
  }

  if (isCustomerSurface) {
    return <Redirect href="/customer" />;
  }

  return <Redirect href="/(auth)/login" />;
}
