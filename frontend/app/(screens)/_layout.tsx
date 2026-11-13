import { Stack } from 'expo-router';

/**
 * Secondary POS screens (history, offline queue, split, license, etc.).
 * Nested under the root Stack so they sit above tabs without a second tab bar.
 */
export default function ScreensLayout() {
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        animation: 'slide_from_right',
        gestureEnabled: true,
        freezeOnBlur: true,
      }}
    />
  );
}
