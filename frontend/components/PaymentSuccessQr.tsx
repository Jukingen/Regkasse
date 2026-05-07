/**
 * RKSV / TSE QR on payment success (PaymentModal success UI).
 *
 * Server PNG: GET `{API_BASE_URL}/pos/payment/{paymentId}/qr.png`
 * (`API_BASE_URL` is configured with trailing `/api`, so this equals `/api/pos/payment/...` on the server.)
 * React Native `Image` cannot send auth headers to remote URLs; we fetch with `Authorization`
 * and display the bytes as a data URL on `Image`.
 *
 * Optional: if `tse.qrPayload` exists, a client-side QR is rendered first; server PNG is still
 * fetched as fallback when `fetchServerPng` is true.
 */
import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, Image, ActivityIndicator } from 'react-native';
import { Buffer } from 'buffer';
import { useTranslation } from 'react-i18next';

import { API_BASE_URL } from '../config';
import type { PaymentTseInfo } from '../services/api/paymentService';
import { sessionManager } from '../services/session/sessionManager';
import { posPaymentQrPngAbsoluteUrl } from '../services/api/posPaymentPaths';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';

interface PaymentSuccessQrProps {
  tse?: PaymentTseInfo | null;
  paymentId?: string | null;
  /** Load QR PNG from the POS payment endpoint while success UI is visible. */
  fetchServerPng?: boolean;
  size?: number;
}

async function fetchQrPngAsDataUrl(paymentId: string): Promise<string | null> {
  const token = await sessionManager.getAccessToken();
  const url = posPaymentQrPngAbsoluteUrl(API_BASE_URL, paymentId);
  const res = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });
  if (!res.ok) {
    console.warn('[PaymentSuccessQr] QR PNG HTTP error:', res.status, url);
    return null;
  }
  const arrayBuffer = await res.arrayBuffer();
  const base64 = Buffer.from(arrayBuffer).toString('base64');
  return `data:image/png;base64,${base64}`;
}

export function PaymentSuccessQr({
  tse,
  paymentId,
  fetchServerPng = false,
  size = 160,
}: PaymentSuccessQrProps) {
  const { t } = useTranslation(['payment']);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [payloadQrFailed, setPayloadQrFailed] = useState(false);
  const [serverPngDataUrl, setServerPngDataUrl] = useState<string | null>(null);
  const [pngLoading, setPngLoading] = useState(false);

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

  useEffect(() => {
    if (!fetchServerPng || !paymentId) {
      setServerPngDataUrl(null);
      setPngLoading(false);
      return;
    }
    let cancelled = false;
    setServerPngDataUrl(null);
    setPngLoading(true);
    fetchQrPngAsDataUrl(paymentId)
      .then((url) => {
        if (cancelled) return;
        if (url) {
          setServerPngDataUrl(url);
          debugPosPaymentTrace('success_flow_qr_ready', { source: 'qr_png_endpoint', paymentId });
        }
      })
      .catch((err) => {
        console.warn('[PaymentSuccessQr] Server QR PNG fetch failed:', err);
      })
      .finally(() => {
        if (!cancelled) setPngLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [fetchServerPng, paymentId]);

  const imageUri = qrDataUrl || serverPngDataUrl || null;
  const isPayloadGenerating = !!qrPayload && !qrDataUrl && !payloadQrFailed;
  const settledNoImage =
    !pngLoading && !isPayloadGenerating && !imageUri && !!(paymentId || qrPayload);

  if (!paymentId && !qrPayload && !pngLoading) {
    return null;
  }

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
          <View style={[styles.qrPlaceholder, { width: size, height: size }]}>
            {(pngLoading || isPayloadGenerating) && (
              <ActivityIndicator size="small" color="#666" />
            )}
          </View>
        )}
      </View>
      {isPayloadGenerating ? (
        <Text style={styles.hintText}>RKSV-QR wird erzeugt…</Text>
      ) : null}
      {pngLoading && !imageUri && !isPayloadGenerating ? (
        <Text style={styles.hintText}>RKSV-QR wird geladen…</Text>
      ) : null}
      {settledNoImage ? (
        <Text style={styles.unavailableText}>QR Code nicht verfügbar</Text>
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
    alignItems: 'center',
    justifyContent: 'center',
  },
  hintText: {
    marginTop: 8,
    fontSize: 12,
    color: '#666',
    textAlign: 'center',
    paddingHorizontal: 12,
    maxWidth: 280,
  },
  unavailableText: {
    marginTop: 8,
    fontSize: 13,
    color: '#888',
    textAlign: 'center',
    paddingHorizontal: 12,
  },
});

export default PaymentSuccessQr;
