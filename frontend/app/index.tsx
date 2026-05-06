import { useRouter, useRootNavigationState, Redirect } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';

import { WaveLoader } from '../src/components/common/WaveLoader';

export default function Index() {
  const rootNavigationState = useRootNavigationState();

  // Wait for navigation to be ready
  if (!rootNavigationState?.key) {
    return (
      <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center' }}>
        <WaveLoader size={32} color="#007AFF" />
      </View>
    );
  }

  return <Redirect href="/(auth)/login" />;
}