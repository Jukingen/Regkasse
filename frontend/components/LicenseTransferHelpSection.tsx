import React, { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';

import { API_BASE_URL } from '../config';
import { useLicenseStatus } from '../hooks/useLicenseStatus';

export type LicenseTransferRequestInfoDto = {
  eligible: boolean;
  message: string;
  customerNameMasked: string | null;
  expiryAtUtc: string | null;
  newServerRequiresMachineFingerprint: boolean;
  licenseKeyMasked: string;
};

function formatDeDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString('de-AT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

async function fetchTransferRequestInfo(licenseKey: string): Promise<LicenseTransferRequestInfoDto> {
  const base = API_BASE_URL.replace(/\/$/, '');
  const url = `${base}/admin/license/transfer-request/${encodeURIComponent(licenseKey.trim())}`;
  const res = await fetch(url, { method: 'GET', headers: { Accept: 'application/json' } });
  if (res.status === 404) {
    const err = new Error('NOT_FOUND');
    (err as Error & { code?: string }).code = 'NOT_FOUND';
    throw err;
  }
  if (!res.ok) {
    const err = new Error('HTTP');
    (err as Error & { code?: string }).code = 'HTTP';
    throw err;
  }
  return (await res.json()) as LicenseTransferRequestInfoDto;
}

/**
 * Self-service transfer prep: checks server-side eligibility and shows this device's machine hash for support.
 * UI copy is German-first (license namespace).
 */
export function LicenseTransferHelpSection() {
  const { t } = useTranslation('license');
  const { status } = useLicenseStatus();
  const [inputKey, setInputKey] = useState('');
  const [loading, setLoading] = useState(false);
  const [info, setInfo] = useState<LicenseTransferRequestInfoDto | null>(null);
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const machineHash = status?.machineHash?.trim() ?? '';

  const onCheck = useCallback(async () => {
    setErrorKey(null);
    setInfo(null);
    const key = inputKey.trim();
    if (!key) {
      setErrorKey('transferErrorKeyRequired');
      return;
    }
    setLoading(true);
    try {
      const dto = await fetchTransferRequestInfo(key);
      setInfo(dto);
    } catch (e) {
      const code = typeof e === 'object' && e !== null && 'code' in e ? String((e as { code?: string }).code) : '';
      if (code === 'NOT_FOUND') setErrorKey('transferErrorNotFound');
      else setErrorKey('transferErrorGeneric');
    } finally {
      setLoading(false);
    }
  }, [inputKey]);

  const detailLines = useMemo(() => {
    if (!info) return null;
    return (
      <View style={styles.detailBlock}>
        <Text style={styles.detailMuted}>
          {t('transferDetailKey')}: {info.licenseKeyMasked}
        </Text>
        {info.customerNameMasked ? (
          <Text style={styles.detailMuted}>
            {t('transferDetailCustomer')}: {info.customerNameMasked}
          </Text>
        ) : null}
        <Text style={styles.detailMuted}>
          {t('transferDetailExpiry')}: {formatDeDateTime(info.expiryAtUtc)}
        </Text>
        <Text style={styles.detailServer}>{info.message}</Text>
      </View>
    );
  }, [info, t]);

  return (
    <View style={styles.wrap}>
      <Text style={styles.intro}>{t('transferSectionIntro')}</Text>
      <TextInput
        style={styles.input}
        value={inputKey}
        onChangeText={setInputKey}
        placeholder={t('transferKeyPlaceholder')}
        placeholderTextColor="#999"
        autoCapitalize="characters"
        autoCorrect={false}
      />
      <TouchableOpacity style={styles.button} onPress={() => void onCheck()} disabled={loading}>
        {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>{t('transferCheckCta')}</Text>}
      </TouchableOpacity>

      <Text style={styles.machineLabel}>{t('transferMachineLabel')}</Text>
      <Text style={styles.machineValue} selectable>
        {machineHash.length > 0 ? machineHash : t('transferMachineEmpty')}
      </Text>

      {errorKey ? <Text style={styles.errorText}>{t(errorKey)}</Text> : null}

      {info ? (
        <View
          style={[
            styles.resultBox,
            info.eligible ? styles.resultOk : styles.resultWarn,
          ]}
        >
          <Text style={styles.resultTitle}>
            {info.eligible ? t('transferEligibleTitle') : t('transferNotEligibleTitle')}
          </Text>
          {detailLines}
          {info.eligible ? <Text style={styles.nextSteps}>{t('transferNextSteps')}</Text> : null}
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: 4 },
  intro: { fontSize: 14, color: '#555', marginBottom: 10, lineHeight: 20 },
  input: {
    borderWidth: 1,
    borderColor: '#ccc',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    backgroundColor: '#fff',
    marginBottom: 10,
  },
  button: {
    backgroundColor: '#007AFF',
    paddingVertical: 12,
    borderRadius: 8,
    alignItems: 'center',
    marginBottom: 16,
  },
  buttonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
  machineLabel: { fontSize: 13, fontWeight: '600', color: '#333', marginBottom: 4 },
  machineValue: { fontSize: 12, color: '#222', fontFamily: 'monospace', marginBottom: 8 },
  errorText: { color: '#c00', fontSize: 13, marginBottom: 8 },
  resultBox: { borderRadius: 8, padding: 12, borderWidth: 1 },
  resultOk: { backgroundColor: '#f0fff4', borderColor: '#9d9' },
  resultWarn: { backgroundColor: '#fff8f0', borderColor: '#ec9' },
  resultTitle: { fontSize: 15, fontWeight: '700', marginBottom: 8, color: '#222' },
  detailBlock: { gap: 4 },
  detailMuted: { fontSize: 13, color: '#444' },
  detailServer: { fontSize: 12, color: '#666', marginTop: 6, fontStyle: 'italic' },
  nextSteps: { fontSize: 14, color: '#1a5f1a', marginTop: 10, lineHeight: 20 },
});
