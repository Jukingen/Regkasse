import { Ionicons } from '@expo/vector-icons';
import { CameraView } from 'expo-camera';
import React, { useCallback, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, View, Text, StyleSheet, Pressable } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { POS_PRODUCT_BARCODE_TYPES } from '../constants/posCameraScan';
import { usePosCameraPermission } from '../hooks/usePosCameraPermission';

export type BarcodeScannerModalProps = {
  visible: boolean;
  title: string;
  hint?: string;
  onClose: () => void;
  onScan: (payload: string) => void;
};

/**
 * Full-screen QR/barcode scanner for POS (customer card, voucher code, product codes).
 * Uses expo-camera native barcode pipeline (no separate barcode-detector package).
 */
export function BarcodeScannerModal({
  visible,
  title,
  hint,
  onClose,
  onScan,
}: BarcodeScannerModalProps) {
  const { t } = useTranslation(['common']);
  const { ui, requestPermission, openSettings } = usePosCameraPermission(visible);
  const scannedRef = useRef(false);
  const [cameraError, setCameraError] = useState(false);

  React.useEffect(() => {
    if (!visible) {
      scannedRef.current = false;
      setCameraError(false);
    }
  }, [visible]);

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

  const renderBody = () => {
    if (ui === 'web') {
      return (
        <View style={styles.fallback}>
          <Ionicons name="scan-outline" size={48} color={SoftColors.textMuted} />
          <Text style={styles.fallbackText}>{t('common:barcodeScanner.webFallback')}</Text>
        </View>
      );
    }
    if (ui === 'loading') {
      return (
        <View style={styles.fallback}>
          <Text style={styles.fallbackText}>{t('common:barcodeScanner.loading')}</Text>
        </View>
      );
    }
    if (ui === 'blocked') {
      return (
        <View style={styles.fallback}>
          <Text style={styles.fallbackText}>{t('common:barcodeScanner.permissionBlocked')}</Text>
          <Pressable style={styles.permissionBtn} onPress={openSettings}>
            <Text style={styles.permissionBtnText}>{t('common:barcodeScanner.openSettings')}</Text>
          </Pressable>
        </View>
      );
    }
    if (ui === 'prompt') {
      return (
        <View style={styles.fallback}>
          <Text style={styles.fallbackText}>{t('common:barcodeScanner.cameraRequired')}</Text>
          <Pressable style={styles.permissionBtn} onPress={requestPermission}>
            <Text style={styles.permissionBtnText}>{t('common:barcodeScanner.allowCamera')}</Text>
          </Pressable>
        </View>
      );
    }
    if (cameraError) {
      return (
        <View style={styles.fallback}>
          <Text style={styles.fallbackText}>{t('common:barcodeScanner.cameraUnavailable')}</Text>
        </View>
      );
    }

    // Only mount when modal is visible + permission granted — frees the camera session.
    if (!visible) return null;

    return (
      <CameraView
        style={styles.camera}
        facing="back"
        active={visible}
        barcodeScannerSettings={{ barcodeTypes: [...POS_PRODUCT_BARCODE_TYPES] }}
        onBarcodeScanned={handleBarcode}
        onMountError={() => {
          setCameraError(true);
        }}
      />
    );
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="fullScreen"
      onRequestClose={onClose}>
      <View style={styles.container}>
        <View style={styles.header}>
          <Pressable
            onPress={onClose}
            style={styles.closeBtn}
            accessibilityLabel={t('common:barcodeScanner.closeA11y')}>
            <Ionicons name="close" size={28} color={SoftColors.textInverse} />
          </Pressable>
          <Text style={styles.title}>{title}</Text>
          <View style={styles.headerSpacer} />
        </View>

        {renderBody()}

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
