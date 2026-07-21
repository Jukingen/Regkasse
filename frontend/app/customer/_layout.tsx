import { Stack } from 'expo-router';

/**
 * Customer-facing surface (EXPO_PUBLIC_APP_SURFACE=customer).
 * Isolated from POS auth/tabs stacks.
 */
export default function CustomerLayout() {
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        animation: 'fade',
        freezeOnBlur: true,
      }}
    />
  );
}
