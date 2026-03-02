/**
 * Ödeme başarı ekranında TSE QR kod gösterimi.
 * Backend'den gelen qrPayload string'ten QR üretir.
 * qrcode paketi ile data URL oluşturup Image ile gösterir (mobile + web uyumlu).
 */
import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import type { PaymentTseInfo } from '../services/api/paymentService';

interface PaymentSuccessQrProps {
  tse?: PaymentTseInfo | null;
  size?: number;
}

export function PaymentSuccessQr({ tse, size = 160 }: PaymentSuccessQrProps) {
  const { t } = useTranslation(['payment']);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);

  const qrPayload = tse?.qrPayload;
  const isDemoFiscal = tse?.isDemoFiscal === true;

  useEffect(() => {
    if (!qrPayload) {
      setQrDataUrl(null);
      return;
    }
    import('qrcode').then((QRCode) => {
      QRCode.toDataURL(qrPayload, { width: size, margin: 2 })
        .then((url: string) => setQrDataUrl(url))
        .catch((err) => console.warn('[PaymentSuccessQr] QR toDataURL failed:', err));
    });
  }, [qrPayload, size]);

  if (!qrPayload) return null;

  return (
    <View style={styles.container}>
      {isDemoFiscal && (
        <View style={styles.demoBanner}>
          <Text style={styles.demoText}>{t('tse.demoFiscalWarning', 'DEMO / FISKAL DEĞİL')}</Text>
        </View>
      )}
      <View style={styles.qrWrapper}>
        {qrDataUrl ? (
          <Image source={{ uri: qrDataUrl }} style={[styles.qrImage, { width: size, height: size }]} />
        ) : (
          <View style={[styles.qrPlaceholder, { width: size, height: size }]} />
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    marginVertical: 12,
  },
  demoBanner: {
    backgroundColor: '#f57c00',
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 6,
    marginBottom: 10,
  },
  demoText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: 'bold',
    letterSpacing: 1,
  },
  qrWrapper: {
    backgroundColor: '#fff',
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  qrImage: {
    resizeMode: 'contain',
  },
  qrPlaceholder: {
    backgroundColor: '#f5f5f5',
    borderRadius: 4,
  },
});

export default PaymentSuccessQr;
