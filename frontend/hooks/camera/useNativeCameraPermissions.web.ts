type WebCameraPermission = {
  granted: boolean;
  canAskAgain: boolean;
};

const WEB_PERMISSION: WebCameraPermission = { granted: false, canAskAgain: false };

/** Web stub — does not import expo-camera. */
export function useNativeCameraPermissions(): [
  WebCameraPermission,
  () => Promise<WebCameraPermission>,
] {
  return [WEB_PERMISSION, async () => WEB_PERMISSION];
}
