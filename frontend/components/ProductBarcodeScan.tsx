import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text } from 'react-native';

import { BarcodeScannerModal } from './BarcodeScannerModal';
import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { getProductByBarcode, type Product } from '../services/api/productService';
import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';

type NotifyKind = 'error' | 'success';

type ProductBarcodeScanProps = {
  enabled: boolean;
  /** While the variant picker is open, a scan is ignored and the picker stays open. */
  scanBlocked: boolean;
  onProduct: (product: Product) => void;
  onNotify: (kind: NotifyKind, message: string) => void;
};

function isBarcodeNotFound(error: unknown): boolean {
  const response = (error as { response?: { status?: number; data?: { code?: string } } } | null)
    ?.response;
  return response?.status === 404 && response.data?.code === 'BARCODE_NOT_FOUND';
}

/**
 * Retail cash-register scan. Shown only when `pos_features.barcodeScan` is true.
 * Reuses BarcodeScannerModal in product mode. Does not store the scanned code.
 */
export function ProductBarcodeScan({
  enabled,
  scanBlocked,
  onProduct,
  onNotify,
}: ProductBarcodeScanProps) {
  const { t } = useTranslation(['verticalProfiles']);
  const [open, setOpen] = useState(false);

  const handleScan = useCallback(
    (code: string) => {
      if (scanBlocked) return;
      void (async () => {
        try {
          const product = await getProductByBarcode(code);
          onProduct(product);
        } catch (error) {
          if (isBarcodeNotFound(error)) {
            onNotify('error', t('verticalProfiles:screens.barcode.barcodeNotFound'));
            return;
          }
          onNotify(
            'error',
            readPosApiErrorMessage(error, t('verticalProfiles:screens.barcode.barcodeNotFound'))
          );
        }
      })();
    },
    [onNotify, onProduct, scanBlocked, t]
  );

  if (!enabled) return null;

  return (
    <>
      <Pressable
        onPress={() => setOpen(true)}
        disabled={scanBlocked}
        style={styles.button}
        accessibilityRole="button"
        accessibilityLabel={t('verticalProfiles:screens.barcode.scanButton')}
        accessibilityHint={t('verticalProfiles:screens.barcode.barcodeCameraPermission')}>
        <Text style={styles.buttonText}>{t('verticalProfiles:screens.barcode.scanButton')}</Text>
      </Pressable>
      <BarcodeScannerModal
        visible={open}
        mode="product"
        title={t('verticalProfiles:screens.barcode.scanProductTitle')}
        onClose={() => setOpen(false)}
        onScan={handleScan}
      />
    </>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 36,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accentDark,
    alignItems: 'center',
    justifyContent: 'center',
  },
  buttonText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
});
