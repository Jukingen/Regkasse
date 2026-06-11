import { Ionicons } from '@expo/vector-icons';
import { CameraView, useCameraPermissions } from 'expo-camera';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  Alert,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
  type ViewStyle,
} from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';
import { customerService, type Customer } from '../../services/api/customerService';
import { WaveLoader } from '../../src/components/common/WaveLoader';

const SCAN_COOLDOWN_MS = 3000;

export type QrCustomerScannerProps = {
  onCustomerFound: (customer: Customer) => void;
  onClose?: () => void;
  style?: ViewStyle;
};

/**
 * Embedded POS customer QR scanner: camera → GET /api/pos/customers/by-qr.
 * UI text: German (de-DE).
 */
export function QrCustomerScanner({ onCustomerFound, onClose, style }: QrCustomerScannerProps) {
  const [permission, requestPermission] = useCameraPermissions();
  const [scanning, setScanning] = useState(true);
  const [lookupBusy, setLookupBusy] = useState(false);
  const [permissionRequested, setPermissionRequested] = useState(false);
  const cooldownTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (Platform.OS === 'web') return;
    if (!permission?.granted && !permissionRequested) {
      setPermissionRequested(true);
      void requestPermission();
    }
  }, [permission?.granted, permissionRequested, requestPermission]);

  useEffect(() => {
    return () => {
      if (cooldownTimerRef.current) clearTimeout(cooldownTimerRef.current);
    };
  }, []);

  const resumeScanning = useCallback(() => {
    cooldownTimerRef.current = setTimeout(() => {
      setScanning(true);
      setLookupBusy(false);
    }, SCAN_COOLDOWN_MS);
  }, []);

  const handleBarCodeScanned = useCallback(
    async ({ data }: { data: string }) => {
      if (!scanning || lookupBusy) return;
      const payload = (data ?? '').trim();
      if (!payload) return;

      setScanning(false);
      setLookupBusy(true);

      try {
        const customer = await customerService.lookupByQrPayload(payload);
        if (customer) {
          onCustomerFound(customer);
        } else {
          Alert.alert('Nicht gefunden', 'Kein Kunde für diesen QR-Code gefunden.');
          resumeScanning();
        }
      } catch {
        Alert.alert('Fehler', 'Kundensuche fehlgeschlagen. Bitte erneut versuchen.');
        resumeScanning();
      }
    },
    [scanning, lookupBusy, onCustomerFound, resumeScanning]
  );

  return (
    <View style={[styles.container, style]}>
      {onClose ? (
        <View style={styles.topBar}>
          <Pressable onPress={onClose} style={styles.closeBtn} accessibilityLabel="Schließen">
            <Ionicons name="close" size={26} color={SoftColors.textInverse} />
          </Pressable>
        </View>
      ) : null}

      {Platform.OS === 'web' ? (
        <View style={styles.fallback}>
          <Ionicons name="scan-outline" size={48} color={SoftColors.textMuted} />
          <Text style={styles.fallbackText}>
            Kamera-Scan ist im Browser nicht verfügbar. Bitte Kundennummer manuell eingeben.
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
          onBarcodeScanned={scanning && !lookupBusy ? handleBarCodeScanned : undefined}
        >
          <View style={styles.overlay}>
            <View style={styles.frame} />
            <Text style={styles.overlayText}>Kunden-QR-Code scannen</Text>
            {lookupBusy ? (
              <View style={styles.busyRow}>
                <WaveLoader size={20} color={SoftColors.textInverse} />
                <Text style={styles.busyText}>Suche Kunde…</Text>
              </View>
            ) : null}
          </View>
        </CameraView>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000',
  },
  topBar: {
    position: 'absolute',
    top: SoftSpacing.lg,
    right: SoftSpacing.md,
    zIndex: 2,
  },
  closeBtn: {
    padding: SoftSpacing.xs,
    backgroundColor: 'rgba(0,0,0,0.45)',
    borderRadius: SoftRadius.full,
  },
  camera: {
    flex: 1,
  },
  overlay: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(0,0,0,0.35)',
    padding: SoftSpacing.lg,
    gap: SoftSpacing.md,
  },
  frame: {
    width: 220,
    height: 220,
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
