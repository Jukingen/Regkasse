/**
 * RKSV / TSE QR on payment success (PaymentModal success UI).
 *
 * Client: `react-native-qrcode-svg` encodes `tse.qrPayload` (exact machine code).
 * ECC M→L selection via `resolveRksvQrEcl` (aligned with backend QrImageService).
 *
 * Server PNG fallback: GET `{API_BASE_URL}/pos/payment/{paymentId}/qr.png`
 * (`API_BASE_URL` includes trailing `/api`). React Native `Image` cannot send auth
 * headers to remote URLs; we fetch with `Authorization` and show a data URL.
 *
 * Gutschein redemptions: server may compact with `|G:` on the PNG endpoint; the
 * client still renders the payment response payload as-is when present.
 */
import { Buffer } from 'buffer';
import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, Image, ActivityIndicator, Platform } from 'react-native';

import { API_BASE_URL } from '../config';
import { RksvQrCodeSvg } from './RksvQrCodeSvg';
import { resolveTenantFetchHeaders } from '../services/api/config';
import type { PaymentTseInfo } from '../services/api/paymentService';
import { posPaymentQrPngAbsoluteUrl } from '../services/api/posPaymentPaths';
import { sessionManager } from '../services/session/sessionManager';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import { resolveRksvQrEcl } from '../utils/rksvQrEncode';

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
    headers: await resolveTenantFetchHeaders(token ? { Authorization: `Bearer ${token}` } : {}),
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
  const [serverPngDataUrl, setServerPngDataUrl] = useState<string | null>(null);
  const [pngLoading, setPngLoading] = useState(false);

  const qrPayload = tse?.qrPayload?.trim() || '';
  const isDemoFiscal = tse?.isDemoFiscal === true;

  const clientEcl = useMemo(() => (qrPayload ? resolveRksvQrEcl(qrPayload) : null), [qrPayload]);

  const showClientSvg = !!qrPayload && !!clientEcl;

  useEffect(() => {
    if (!fetchServerPng || !paymentId) {
      setServerPngDataUrl(null);
      setPngLoading(false);
      return;
    }
    // Exact client SVG wins when encodeable; server PNG covers oversized / Gutschein-compact cases.
    if (clientEcl) {
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
          debugPosPaymentTrace('success_flow_qr_ready', {
            source: 'qr_png_endpoint',
            paymentId,
            platform: Platform.OS,
          });
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
  }, [fetchServerPng, paymentId, clientEcl]);

  const imageUri = !showClientSvg ? serverPngDataUrl : null;
  const settledNoImage = !pngLoading && !showClientSvg && !imageUri && !!(paymentId || qrPayload);

  if (!paymentId && !qrPayload && !pngLoading) {
    return null;
  }

  return (
    <View
      style={styles.container}
      accessibilityLabel={t('tse.qrAccessibilityLabel', 'RKSV QR-Code')}
      testID="payment-success-qr">
      {isDemoFiscal && (
        <View style={styles.demoBanner}>
          <Text style={styles.demoText}>{t('tse.demoFiscalWarning', 'DEMO / FISKAL DEĞİL')}</Text>
        </View>
      )}
      <View style={styles.qrWrapper}>
        {showClientSvg && clientEcl ? (
          <View
            style={[styles.qrImage, { width: size, height: size }]}
            accessibilityRole="image"
            accessibilityLabel={t('tse.qrAccessibilityLabel', 'RKSV QR-Code')}>
            <RksvQrCodeSvg
              value={qrPayload}
              ecl={clientEcl}
              size={size}
              quietZone={8}
              testID={`payment-success-qr-svg-${Platform.OS}`}
            />
          </View>
        ) : imageUri ? (
          <Image
            source={{ uri: imageUri }}
            style={[styles.qrImage, { width: size, height: size }]}
            accessibilityLabel={t('tse.qrAccessibilityLabel', 'RKSV QR-Code')}
            testID={`payment-success-qr-png-${Platform.OS}`}
          />
        ) : (
          <View style={[styles.qrPlaceholder, { width: size, height: size }]}>
            {pngLoading && <ActivityIndicator size="small" color="#666" />}
          </View>
        )}
      </View>
      {pngLoading && !showClientSvg && !imageUri ? (
        <Text style={styles.hintText}>RKSV-QR wird geladen…</Text>
      ) : null}
      {settledNoImage ? <Text style={styles.unavailableText}>QR Code nicht verfügbar</Text> : null}
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
    alignItems: 'center',
    justifyContent: 'center',
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
