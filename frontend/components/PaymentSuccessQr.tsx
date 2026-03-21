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
  /** Server-rendered PNG (GET /api/pos/payment/{id}/qr.png) when qrPayload is missing. */
  qrPngDataUrl?: string | null;
  size?: number;
}

export function PaymentSuccessQr({ tse, qrPngDataUrl, size = 160 }: PaymentSuccessQrProps) {
  const { t } = useTranslation(['payment']);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [payloadQrFailed, setPayloadQrFailed] = useState(false);

  const qrPayload = tse?.qrPayload;
  const isDemoFiscal = tse?.isDemoFiscal === true;

  useEffect(() => {
    if (!qrPayload) {
      setQrDataUrl(null);
      setPayloadQrFailed(false);
      return;
    }
    setPayloadQrFailed(false);
    let cancelled = false;
    import('qrcode')
      .then((QRCode) => QRCode.toDataURL(qrPayload, { width: size, margin: 2 }))
      .then((url: string) => {
        if (!cancelled) setQrDataUrl(url);
      })
      .catch((err) => {
        console.warn('[PaymentSuccessQr] QR toDataURL failed:', err);
        if (!cancelled) {
          setQrDataUrl(null);
          setPayloadQrFailed(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [qrPayload, size]);

  const imageUri = qrDataUrl || qrPngDataUrl || null;
  if (!qrPayload && !qrPngDataUrl) return null;

  const showPayloadGenerating = !!qrPayload && !imageUri && !payloadQrFailed;
  const showPayloadFailed = !!qrPayload && payloadQrFailed && !qrPngDataUrl;

  return (
    <View style={styles.container}>
      {isDemoFiscal && (
        <View style={styles.demoBanner}>
          <Text style={styles.demoText}>{t('tse.demoFiscalWarning', 'DEMO / FISKAL DEĞİL')}</Text>
        </View>
      )}
      <View style={styles.qrWrapper}>
        {imageUri ? (
          <Image source={{ uri: imageUri }} style={[styles.qrImage, { width: size, height: size }]} />
        ) : (
          <View style={[styles.qrPlaceholder, { width: size, height: size }]} />
        )}
      </View>
      {showPayloadGenerating ? (
        <Text style={styles.hintText}>RKSV-QR wird erzeugt…</Text>
      ) : null}
      {showPayloadFailed ? (
        <Text style={styles.hintText}>
          RKSV-QR aus Payload konnte nicht erzeugt werden. Die Zahlung ist gültig — nutzen Sie ggf. Beleg-PDF oder Druck.
        </Text>
      ) : null}
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
  hintText: {
    marginTop: 8,
    fontSize: 12,
    color: '#666',
    textAlign: 'center',
    paddingHorizontal: 12,
    maxWidth: 280,
  },
});

export default PaymentSuccessQr;
