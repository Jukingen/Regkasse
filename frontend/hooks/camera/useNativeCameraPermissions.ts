import { useCameraPermissions } from 'expo-camera';

/** Native camera permission hook. Web uses `useNativeCameraPermissions.web.ts`. */
export function useNativeCameraPermissions() {
  return useCameraPermissions();
}
