import { Ionicons } from '@expo/vector-icons';
import { CameraView, useCameraPermissions } from 'expo-camera';
import React, { useCallback, useRef, useState } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  Pressable,
  Platform,
} from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';

export type BarcodeScannerModalProps = {
  visible: boolean;
  title: string;
  hint?: string;
  onClose: () => void;
  onScan: (payload: string) => void;
};

/**
 * Full-screen QR/barcode scanner for POS (customer card, voucher code).
 * Web: shows manual-entry hint (camera scan not supported in browser).
 */
export function BarcodeScannerModal({
  visible,
  title,
  hint,
  onClose,
  onScan,
}: BarcodeScannerModalProps) {
  const [permission, requestPermission] = useCameraPermissions();
  const scannedRef = useRef(false);
  const [permissionRequested, setPermissionRequested] = useState(false);

  const handleBarcode = useCallback(
    ({ data }: { data: string }) => {
      if (scannedRef.current) return;
      const trimmed = (data ?? '').trim();
      if (!trimmed) return;
      scannedRef.current = true;
      onScan(trimmed);
      onClose();
    },
    [onScan, onClose]
  );

  React.useEffect(() => {
    if (!visible) {
      scannedRef.current = false;
      return;
    }
    if (Platform.OS === 'web') return;
    if (!permission?.granted && !permissionRequested) {
      setPermissionRequested(true);
      void requestPermission();
    }
  }, [visible, permission?.granted, permissionRequested, requestPermission]);

  return (
    <Modal visible={visible} animationType="slide" presentationStyle="fullScreen" onRequestClose={onClose}>
      <View style={styles.container}>
        <View style={styles.header}>
          <Pressable onPress={onClose} style={styles.closeBtn} accessibilityLabel="Schließen">
            <Ionicons name="close" size={28} color={SoftColors.textInverse} />
          </Pressable>
          <Text style={styles.title}>{title}</Text>
          <View style={styles.headerSpacer} />
        </View>

        {Platform.OS === 'web' ? (
          <View style={styles.fallback}>
            <Ionicons name="scan-outline" size={48} color={SoftColors.textMuted} />
            <Text style={styles.fallbackText}>
              Kamera-Scan ist im Browser nicht verfügbar. Bitte Code manuell eingeben.
            </Text>
          </View>
        ) : !permission?.granted ? (
          <View style={styles.fallback}>
            <Text style={styles.fallbackText}>Kamerazugriff wird benötigt.</Text>
            <Pressable style={styles.permissionBtn} onPress={() => void requestPermission()}>
              <Text style={styles.permissionBtnText}>Kamera erlauben</Text>
            </Pressable>
          </View>
        ) : (
          <CameraView
            style={styles.camera}
            facing="back"
            barcodeScannerSettings={{
              barcodeTypes: ['qr', 'code128', 'code39', 'ean13', 'ean8'],
            }}
            onBarcodeScanned={handleBarcode}
          />
        )}

        {hint ? <Text style={styles.hint}>{hint}</Text> : null}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: SoftSpacing.lg,
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.sm,
    backgroundColor: 'rgba(0,0,0,0.6)',
  },
  closeBtn: { padding: SoftSpacing.xs },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textInverse,
    flex: 1,
    textAlign: 'center',
  },
  headerSpacer: { width: 40 },
  camera: { flex: 1 },
  hint: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    textAlign: 'center',
    padding: SoftSpacing.md,
    backgroundColor: 'rgba(0,0,0,0.5)',
  },
  fallback: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.xl,
    gap: SoftSpacing.md,
  },
  fallbackText: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    textAlign: 'center',
  },
  permissionBtn: {
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
  },
  permissionBtnText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});
