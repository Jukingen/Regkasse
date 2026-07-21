/**
 * Shared RKSV QR renderer via `react-native-qrcode-svg` + `react-native-svg`.
 * Callers must pass an ECL already validated by {@link resolveRksvQrEcl}
 * (oversized payloads should fall back to server PNG elsewhere).
 */
import React, { memo, useCallback } from 'react';
import { Platform, StyleSheet, View } from 'react-native';
import QRCode from 'react-native-qrcode-svg';

import type { RksvQrEcl } from '../utils/rksvQrEncode';

export type RksvQrCodeSvgProps = {
  /** Exact RKSV / TSE machine-code (or Gutschein mini) payload. */
  value: string;
  /** Error-correction level from {@link resolveRksvQrEcl}. */
  ecl: RksvQrEcl;
  size: number;
  /** Quiet zone in px (scanners need margin around the modules). */
  quietZone?: number;
  testID?: string;
  onError?: (error: unknown) => void;
};

/**
 * High-contrast black/white QR for fiscal payloads.
 * Memoized so parent re-renders (payment modal, receipt scroll) do not rebuild the SVG matrix.
 */
export const RksvQrCodeSvg = memo(function RksvQrCodeSvg({
  value,
  ecl,
  size,
  quietZone = 8,
  testID,
  onError,
}: RksvQrCodeSvgProps) {
  const handleError = useCallback(
    (error: unknown) => {
      console.warn('[RksvQrCodeSvg] encode/render failed:', error);
      onError?.(error);
    },
    [onError]
  );

  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  return (
    <View
      style={[styles.box, { width: size, height: size }]}
      accessibilityRole="image"
      pointerEvents="none"
      testID={testID ?? `rksv-qr-svg-${Platform.OS}`}>
      <QRCode
        value={trimmed}
        size={size}
        ecl={ecl}
        backgroundColor="#FFFFFF"
        color="#000000"
        quietZone={quietZone}
        onError={handleError}
      />
    </View>
  );
});

const styles = StyleSheet.create({
  box: {
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
    backgroundColor: '#FFFFFF',
  },
});

export default RksvQrCodeSvg;
