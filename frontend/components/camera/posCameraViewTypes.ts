import type { ReactNode } from 'react';
import type { StyleProp, ViewStyle } from 'react-native';

/** Shared POS camera surface — native implementation uses expo-camera `CameraView`. */
export type PosCameraViewProps = {
  style?: StyleProp<ViewStyle>;
  facing?: 'front' | 'back';
  active?: boolean;
  barcodeScannerSettings?: { barcodeTypes: readonly string[] | string[] };
  onBarcodeScanned?: (event: { data: string }) => void;
  onMountError?: () => void;
  children?: ReactNode;
};
