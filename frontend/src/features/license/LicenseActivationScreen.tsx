import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useRouter } from 'expo-router';

import {
  buildLicenseRenewalMailtoUrl,
  getLicenseExtensionHttpUrl,
  LICENSE_SUPPORT_EMAIL,
} from '../../../constants/licenseRenewal';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import { useLicenseStatus } from '../../../hooks/useLicenseStatus';
import { licenseApi } from '../../../api/license';
import { openHttpOrHttpsUrl, openMailtoUrl } from '../../../utils/openLink';

const LICENSE_KEY_PATTERN = /^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$/i;

function sanitizeLicenseKeyInput(raw: string): string {
  return raw
    .toUpperCase()
    .replace(/[^A-Z0-9-]/g, '')
    .slice(0, 22);
}

type ApiErrorShape = { status?: number; data?: { message?: string; Message?: string } };

function readApiErrorMessage(err: unknown): string | undefined {
  if (!err || typeof err !== 'object') return undefined;
  const e = err as ApiErrorShape;
  const d = e.data;
  if (!d || typeof d !== 'object') return undefined;
  const m = d.message ?? d.Message;
  return typeof m === 'string' ? m : undefined;
}

/**
 * POS screen: enter REGK display key and call anonymous POST /api/license/activate.
 */
export default function LicenseActivationScreen() {
  const router = useRouter();
  const { t } = useTranslation(['license', 'common']);
  const { status, loading: statusLoading, refetch } = useLicenseStatus();

  const [licenseKey, setLicenseKey] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [feedback, setFeedback] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);

  const unlimitedPaid =
    !!status && status.isValid && !status.isTrial && !status.isExpired && !status.expiryDate;

  const statusHeadline = useMemo(() => {
    if (statusLoading && !status) return t('license:activationStatusLoading');
    if (!status) return t('license:activationStatusUnknown');
    if (status.isExpired) return t('license:typeExpired');
    const lt = (status.licenseType ?? '').trim().toLowerCase();
    if (lt === 'demo') return t('license:typeDemo');
    if (lt === 'licensed' || lt === 'paid') return t('license:typeLicensed');
    if (status.isTrial) return t('license:typeTrial');
    return t('license:typePaid');
  }, [status, statusLoading, t]);

  const statusSubline = useMemo(() => {
    if (!status) return null;
    if (unlimitedPaid) return t('license:expiryNone');
    if (status.expiryDate) {
      try {
        const d = new Date(status.expiryDate);
        if (!Number.isNaN(d.getTime())) {
          return t('license:activationExpiryLine', {
            date: d.toLocaleString('de-AT', {
              year: 'numeric',
              month: '2-digit',
              day: '2-digit',
              hour: '2-digit',
              minute: '2-digit',
            }),
          });
        }
      } catch {
        /* ignore */
      }
    }
    return t('license:daysRemainingValue', { count: status.daysRemaining });
  }, [status, unlimitedPaid, t]);

  const hasConfiguredExtensionUrl = useMemo(() => Boolean(getLicenseExtensionHttpUrl()), []);

  const openRenewPrimary = useCallback(async () => {
    let httpUrl = getLicenseExtensionHttpUrl();
    if (httpUrl) {
      if (status?.machineHash) {
        const sep = httpUrl.includes('?') ? '&' : '?';
        httpUrl = `${httpUrl}${sep}machineHash=${encodeURIComponent(status.machineHash)}`;
      }
      const ok = await openHttpOrHttpsUrl(httpUrl);
      if (!ok) {
        Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedBody'));
      }
      return;
    }

    const mailto = buildLicenseRenewalMailtoUrl(
      status
        ? {
            machineHash: status.machineHash,
            daysRemaining: status.daysRemaining,
            isTrial: status.isTrial,
            isExpired: status.isExpired,
          }
        : null,
    );
    const ok = await openMailtoUrl(mailto);
    if (!ok) {
      Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedMailBody'));
    }
  }, [t, status]);

  const onChangeKey = useCallback((next: string) => {
    setFeedback(null);
    setLicenseKey(sanitizeLicenseKeyInput(next));
  }, []);

  const onActivate = useCallback(async () => {
    setFeedback(null);
    const trimmed = licenseKey.trim().toUpperCase();
    if (!LICENSE_KEY_PATTERN.test(trimmed)) {
      setFeedback({ kind: 'error', text: t('license:activationInvalidFormat') });
      return;
    }

    setSubmitting(true);
    try {
      const res = await licenseApi.activate(trimmed);
      if (res.success) {
        setFeedback({ kind: 'success', text: t('license:activationSuccess') });
        await refetch();
        return;
      }
      setFeedback({ kind: 'error', text: t('license:activationFailed') });
    } catch (err: unknown) {
      const st =
        err && typeof err === 'object' && 'status' in err && typeof (err as ApiErrorShape).status === 'number'
          ? (err as ApiErrorShape).status
          : undefined;
      const serverMsg = readApiErrorMessage(err);
      const noResponse = st === undefined;
      setFeedback({
        kind: 'error',
        text: noResponse && !serverMsg ? t('license:activationNetwork') : mapActivationFailureToGerman(serverMsg, t),
      });
    } finally {
      setSubmitting(false);
    }
  }, [licenseKey, refetch, t]);

  return (
    <View style={styles.root}>
      <View style={styles.header}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backBtn} accessibilityRole="button">
          <Text style={styles.backText}>← {t('common:back')}</Text>
        </TouchableOpacity>
        <Text style={styles.title}>{t('license:activationScreenTitle')}</Text>
      </View>

      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
        <View style={styles.card}>
          <Text style={styles.sectionLabel}>{t('license:activationCurrentStatus')}</Text>
          {statusLoading && !status ? (
            <ActivityIndicator color={SoftColors.accentDark} style={{ marginVertical: 8 }} />
          ) : (
            <>
              <Text style={styles.statusHeadline}>{statusHeadline}</Text>
              {statusSubline ? <Text style={styles.statusSub}>{statusSubline}</Text> : null}
            </>
          )}
        </View>

        <View style={styles.card}>
          <Text style={styles.sectionLabel}>{t('license:activationKeyLabel')}</Text>
          <TextInput
            value={licenseKey}
            onChangeText={onChangeKey}
            placeholder={t('license:activationKeyPlaceholder')}
            placeholderTextColor={SoftColors.textMuted}
            autoCapitalize="characters"
            autoCorrect={false}
            editable={!submitting}
            style={styles.input}
            accessibilityLabel={t('license:activationKeyLabel')}
          />
          <Text style={styles.hint}>{t('license:activationKeyHint')}</Text>

          {feedback ? (
            <View
              style={[styles.feedbackBox, feedback.kind === 'success' ? styles.feedbackOk : styles.feedbackErr]}
              accessibilityLiveRegion="polite"
            >
              <Ionicons
                name={feedback.kind === 'success' ? 'checkmark-circle' : 'alert-circle'}
                size={18}
                color={feedback.kind === 'success' ? '#1b5e20' : '#b71c1c'}
              />
              <Text style={[styles.feedbackText, feedback.kind === 'success' ? styles.feedbackTextOk : styles.feedbackTextErr]}>
                {feedback.text}
              </Text>
            </View>
          ) : null}

          <TouchableOpacity
            style={[styles.primaryBtn, submitting && styles.primaryBtnDisabled]}
            onPress={() => void onActivate()}
            disabled={submitting}
            accessibilityRole="button"
            accessibilityLabel={t('license:activationSubmit')}
          >
            {submitting ? (
              <ActivityIndicator color={SoftColors.textInverse} />
            ) : (
              <Text style={styles.primaryBtnText}>{t('license:activationSubmit')}</Text>
            )}
          </TouchableOpacity>
        </View>

        <View style={styles.card}>
          <Text style={styles.sectionLabel}>{t('license:activationExtendSection')}</Text>
          <Text style={styles.extendBody}>{t('license:activationExtendBody', { email: LICENSE_SUPPORT_EMAIL })}</Text>
          <TouchableOpacity style={styles.secondaryBtn} onPress={() => void openRenewPrimary()} accessibilityRole="button">
            <Ionicons
              name={hasConfiguredExtensionUrl ? 'open-outline' : 'mail-outline'}
              size={18}
              color={SoftColors.accentDark}
            />
            <Text style={styles.secondaryBtnText}>{t('license:renewCta')}</Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </View>
  );
}

function mapActivationFailureToGerman(
  serverMsg: string | undefined,
  t: (key: string, opts?: Record<string, string>) => string,
): string {
  if (!serverMsg) return t('license:activationFailed');
  const m = serverMsg.trim();
  if (m.includes('Invalid license key format')) return t('license:activationInvalidFormat');
  if (m.includes('LicenseKey is required')) return t('license:activationKeyRequired');
  if (m.includes('Machine fingerprint does not match')) return t('license:activationFingerprintMismatch');
  if (m.includes('transferred or revoked')) return t('license:activationRevokedOrTransferred');
  if (m.includes('Remote license server')) return t('license:activationRemoteRejected');
  if (m.includes('OpenAPI export mode')) return t('license:activationDisabledExport');
  if (m.includes('could not be recorded in the database')) return t('license:activationDbRecordFailed');
  if (m.includes('internal error')) return t('license:activationInternalError');
  if (m.includes('OfflineVerificationPublicKeyPem is not configured')) return t('license:activationOfflinePemMissing');
  return t('license:activationFailed');
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: SoftColors.bgSecondary,
  },
  header: {
    paddingHorizontal: SoftSpacing.md,
    paddingTop: SoftSpacing.md,
    paddingBottom: SoftSpacing.sm,
    backgroundColor: SoftColors.bgCard,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.borderLight,
  },
  backBtn: {
    alignSelf: 'flex-start',
    paddingVertical: SoftSpacing.xs,
    marginBottom: SoftSpacing.xs,
  },
  backText: {
    ...SoftTypography.body,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  title: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  scroll: {
    padding: SoftSpacing.lg,
    paddingBottom: SoftSpacing.xl * 2,
  },
  card: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    marginBottom: SoftSpacing.md,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.borderLight,
  },
  sectionLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.sm,
    fontWeight: '600',
  },
  statusHeadline: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    fontSize: 20,
  },
  statusSub: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginTop: 4,
  },
  input: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 12,
    fontSize: 16,
    fontFamily: 'monospace',
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgPrimary,
  },
  hint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.xs,
  },
  feedbackBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: SoftSpacing.xs,
    marginTop: SoftSpacing.md,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
  },
  feedbackOk: {
    backgroundColor: '#e8f5e9',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: '#a5d6a7',
  },
  feedbackErr: {
    backgroundColor: '#ffebee',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: '#ffcdd2',
  },
  feedbackText: {
    ...SoftTypography.body,
    flex: 1,
    fontWeight: '600',
  },
  feedbackTextOk: { color: '#1b5e20' },
  feedbackTextErr: { color: '#b71c1c' },
  primaryBtn: {
    marginTop: SoftSpacing.lg,
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 48,
  },
  primaryBtnDisabled: { opacity: 0.65 },
  primaryBtnText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  extendBody: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  secondaryBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgPrimary,
  },
  secondaryBtnText: {
    ...SoftTypography.label,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
});
