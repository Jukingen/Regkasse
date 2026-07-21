import { Ionicons } from '@expo/vector-icons';
import { CameraView } from 'expo-camera';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { POS_QR_BARCODE_TYPES } from '../constants/posCameraScan';
import { usePosCameraPermission } from '../hooks/usePosCameraPermission';
import {
  paymentService,
  type VoucherValidateFailure,
  type VoucherValidateSuccess,
} from '../services/api/paymentService';
import { WaveLoader } from '../src/components/common/WaveLoader';

const SCAN_COOLDOWN_MS = 2000;

export type VoucherScannerProps = {
  visible: boolean;
  onClose: () => void;
  /** Plain scanned code + server validation snapshot (masked code in snapshot only). */
  onVoucherValidated: (code: string, result: VoucherValidateSuccess) => void;
};

function resolveVoucherErrorMessage(
  t: (key: string) => string,
  failure: VoucherValidateFailure
): string {
  const code = (failure.errorCode ?? '').toUpperCase();
  if (code === 'EXPIRED') return t('checkout:posFlow.payment.voucher.expired');
  if (code === 'CANCELLED') return t('checkout:posFlow.payment.voucher.cancelled');
  if (code === 'REDEEMED' || code === 'NO_BALANCE')
    return t('checkout:posFlow.payment.voucher.redeemed');
  if (code === 'NOT_YET_VALID') return t('checkout:posFlow.payment.voucher.notYetValid');
  if (code === 'NOT_FOUND') return t('checkout:posFlow.payment.voucher.notFound');
  if (code === 'NETWORK') return t('checkout:posFlow.payment.voucher.networkError');
  if (failure.message) return failure.message;
  return t('checkout:posFlow.payment.voucher.invalid');
}

/**
 * Full-screen Gutschein scanner: camera → POST /api/pos/vouchers/validate.
 * QR-only barcode types for faster, more reliable scans.
 */
export function VoucherScanner({ visible, onClose, onVoucherValidated }: VoucherScannerProps) {
  const { t } = useTranslation(['checkout', 'common']);
  const { ui, requestPermission, openSettings } = usePosCameraPermission(visible);
  const [scanning, setScanning] = useState(true);
  const [validating, setValidating] = useState(false);
  const [cameraError, setCameraError] = useState(false);
  const cooldownTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (!visible) {
      setScanning(true);
      setValidating(false);
      setCameraError(false);
    }
  }, [visible]);

  useEffect(() => {
    return () => {
      if (cooldownTimerRef.current) clearTimeout(cooldownTimerRef.current);
    };
  }, []);

  const resumeScanning = useCallback(() => {
    cooldownTimerRef.current = setTimeout(() => {
      setScanning(true);
      setValidating(false);
    }, SCAN_COOLDOWN_MS);
  }, []);

  const handleBarCodeScanned = useCallback(
    async ({ data }: { data: string }) => {
      if (!scanning || validating) return;
      const code = (data ?? '').trim();
      if (!code) return;

      setScanning(false);
      setValidating(true);

      try {
        const result = await paymentService.validateVoucher(code);
        if (result.ok) {
          onVoucherValidated(code, result);
          onClose();
          return;
        }
        Alert.alert(
          t('checkout:posFlow.payment.voucher.invalidTitle'),
          resolveVoucherErrorMessage(t, result)
        );
        resumeScanning();
      } catch {
        Alert.alert(
          t('checkout:posFlow.payment.voucher.invalidTitle'),
          t('checkout:posFlow.payment.voucher.networkError')
        );
        resumeScanning();
      }
    },
    [scanning, validating, onVoucherValidated, onClose, resumeScanning, t]
  );

  const renderCameraBody = () => {
    if (ui === 'web') {
      return (
        <View style={styles.fallback}>
          <Ionicons name="scan-outline" size={48} color={SoftColors.textMuted} />
          <Text style={styles.fallbackText}>
            {t('checkout:posFlow.payment.voucher.scanner.webFallback')}
          </Text>
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
          <Text style={styles.fallbackText}>
            {t('checkout:posFlow.payment.voucher.scanner.cameraRequired')}
          </Text>
          <Pressable style={styles.permissionBtn} onPress={requestPermission}>
            <Text style={styles.permissionBtnText}>
              {t('checkout:posFlow.payment.voucher.scanner.allowCamera')}
            </Text>
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
    if (!visible) return null;

    const scanActive = scanning && !validating;

    return (
      <CameraView
        style={styles.camera}
        facing="back"
        active={scanActive}
        barcodeScannerSettings={{ barcodeTypes: [...POS_QR_BARCODE_TYPES] }}
        onBarcodeScanned={scanActive ? handleBarCodeScanned : undefined}
        onMountError={() => {
          setCameraError(true);
        }}>
        <View style={styles.overlay}>
          <View style={styles.frame} />
          <Text style={styles.overlayText}>
            {t('checkout:posFlow.payment.voucher.scanner.overlayHint')}
          </Text>
          {validating ? (
            <View style={styles.busyRow}>
              <WaveLoader size={20} color={SoftColors.textInverse} />
              <Text style={styles.busyText}>
                {t('checkout:posFlow.payment.voucher.scanner.validating')}
              </Text>
            </View>
          ) : null}
        </View>
      </CameraView>
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
            accessibilityLabel={t('checkout:posFlow.payment.voucher.scanner.closeA11y')}>
            <Ionicons name="close" size={28} color={SoftColors.textInverse} />
          </Pressable>
          <Text style={styles.title}>{t('checkout:posFlow.payment.voucher.scanner.title')}</Text>
          <View style={styles.headerSpacer} />
        </View>

        {renderCameraBody()}
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
  overlay: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0,0,0,0.35)',
    padding: SoftSpacing.lg,
    gap: SoftSpacing.md,
  },
  frame: {
    width: 260,
    height: 140,
    borderWidth: 2,
    borderColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    backgroundColor: 'transparent',
  },
  overlayText: {
    ...SoftTypography.body,
    color: SoftColors.textInverse,
    textAlign: 'center',
    fontWeight: '600',
  },
  busyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  busyText: {
    color: SoftColors.textInverse,
    fontSize: 14,
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
