import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';

import { licenseApi } from '../../../api/license';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../../constants/SoftTheme';
import {
  buildLicenseRenewalMailtoUrl,
  LICENSE_SUPPORT_EMAIL,
} from '../../../constants/licenseRenewal';
import { useLicenseStatus, type LicenseStatus } from '../../../hooks/useLicenseStatus';
import { formatUserDateTime } from '../../../utils/dateFormatter';
import { preferLicenseHoursRemaining } from '../../../utils/licenseExpiryRemaining';
import { isValidPosLicenseKey, sanitizeLicenseKeyInput } from '../../../utils/licenseKeyFormat';
import { openMailtoUrl } from '../../../utils/openLink';

import { openAdmin, openLicenseExtension } from '@/src/features/admin-navigation/openAdmin';

function formatExpiryDeAt(iso: string | null): string {
  if (!iso) return '—';
  return formatUserDateTime(iso) || '—';
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

function mapActivationFailureToGerman(
  serverMsg: string | undefined,
  t: (key: string, opts?: Record<string, string>) => string
): string {
  if (!serverMsg) return t('license:activationFailed');
  const m = serverMsg.trim();
  if (m.includes('Invalid license key format')) return t('license:activationInvalidFormat');
  if (m.includes('LicenseKey is required')) return t('license:activationKeyRequired');
  if (m.includes('Machine fingerprint does not match'))
    return t('license:activationFingerprintMismatch');
  if (m.includes('transferred or revoked')) return t('license:activationRevokedOrTransferred');
  if (m.includes('Remote license server')) return t('license:activationRemoteRejected');
  if (m.includes('OpenAPI export mode')) return t('license:activationDisabledExport');
  if (m.includes('could not be recorded in the database'))
    return t('license:activationDbRecordFailed');
  if (m.includes('internal error')) return t('license:activationInternalError');
  if (m.includes('OfflineVerificationPublicKeyPem is not configured'))
    return t('license:activationOfflinePemMissing');
  return m;
}

export type LicenseModalProps = {
  visible: boolean;
  onClose: () => void;
  status: LicenseStatus | null;
  loading: boolean;
  unlimitedPaid: boolean;
  refetch: () => Promise<void>;
};

/**
 * POS license detail sheet: status, optional REGK activation (POST /api/license/activate), renewal shortcuts.
 */
export function LicenseModal({
  visible,
  onClose,
  status,
  loading,
  unlimitedPaid,
  refetch,
}: LicenseModalProps) {
  const router = useRouter();
  const { t } = useTranslation(['license', 'common']);
  const { applyLicenseActivation, setIsActivating } = useLicenseStatus();
  const [techDetailsOpen, setTechDetailsOpen] = useState(false);
  const [licenseKey, setLicenseKey] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [activationSucceeded, setActivationSucceeded] = useState(false);
  const [activatedExpiryIso, setActivatedExpiryIso] = useState<string | null>(null);
  const [activationFeedback, setActivationFeedback] = useState<{
    kind: 'success' | 'error';
    text: string;
  } | null>(null);

  const hasConfiguredExtensionUrl = useMemo(() => true, []);

  /** Active paid license (not trial, not expired) — hide key entry per product rules. */
  const isLicensedActive = Boolean(
    status && status.isValid && !status.isTrial && !status.isExpired
  );

  const displayExpiryIso = activatedExpiryIso ?? status?.expiryDate ?? null;

  const handleExtendLicense = useCallback(async () => {
    const machineHash = status?.machineHash?.trim();

    const ok = machineHash
      ? await openLicenseExtension(machineHash)
      : await openAdmin('licenseOverview', undefined, {
          fallbackToMail: true,
          mailtoSubject: 'Lizenzinformationen anfordern',
        });

    if (!ok) {
      Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedBody'));
    }
  }, [t, status]);

  const openRenewMail = useCallback(async () => {
    const mailto = buildLicenseRenewalMailtoUrl(
      status
        ? {
            machineHash: status.machineHash,
            daysRemaining: status.daysRemaining,
            isTrial: status.isTrial,
            isExpired: status.isExpired,
          }
        : null
    );
    const ok = await openMailtoUrl(mailto);
    if (!ok) {
      Alert.alert(t('license:renewOpenFailedTitle'), t('license:renewOpenFailedMailBody'));
    }
  }, [t, status]);

  const onChangeKey = useCallback((next: string) => {
    setActivationFeedback(null);
    setLicenseKey(sanitizeLicenseKeyInput(next));
  }, []);

  const resetActivationUi = useCallback(() => {
    setTechDetailsOpen(false);
    setActivationFeedback(null);
    setLicenseKey('');
    setActivationSucceeded(false);
    setActivatedExpiryIso(null);
    setSubmitting(false);
    setIsActivating(false);
  }, [setIsActivating]);

  const handleClose = useCallback(() => {
    resetActivationUi();
    onClose();
  }, [onClose, resetActivationUi]);

  const onActivate = useCallback(async () => {
    setActivationFeedback(null);
    const trimmed = licenseKey.trim().toUpperCase();
    if (!isValidPosLicenseKey(trimmed)) {
      setActivationFeedback({ kind: 'error', text: t('license:activationInvalidFormat') });
      return;
    }

    setSubmitting(true);
    setIsActivating(true);
    try {
      const res = await licenseApi.activate(trimmed);
      if (res.success) {
        const expiry =
          typeof res.validUntil === 'string' && res.validUntil.trim().length > 0
            ? res.validUntil.trim()
            : null;
        setActivatedExpiryIso(expiry);
        setActivationSucceeded(true);
        setActivationFeedback({
          kind: 'success',
          text: t('license:activation.success'),
        });
        setLicenseKey('');
        await applyLicenseActivation(res, trimmed);
        return;
      }
      setActivationFeedback({
        kind: 'error',
        text: t('license:activation.error', { error: t('license:activationFailed') }),
      });
    } catch (err: unknown) {
      const st =
        err &&
        typeof err === 'object' &&
        'status' in err &&
        typeof (err as ApiErrorShape).status === 'number'
          ? (err as ApiErrorShape).status
          : undefined;
      const serverMsg = readApiErrorMessage(err);
      const noResponse = st === undefined;
      const mapped =
        noResponse && !serverMsg
          ? t('license:activationNetwork')
          : mapActivationFailureToGerman(serverMsg, t);
      setActivationFeedback({
        kind: 'error',
        text: t('license:activation.error', { error: mapped }),
      });
    } finally {
      setSubmitting(false);
      setIsActivating(false);
    }
  }, [applyLicenseActivation, licenseKey, setIsActivating, t]);

  const openFullActivationScreen = useCallback(() => {
    router.push('/(screens)/license-activate');
    handleClose();
  }, [handleClose, router]);

  return (
    <Modal visible={visible} animationType="fade" transparent onRequestClose={handleClose}>
      <View style={styles.modalRoot}>
        <Pressable
          style={styles.modalBackdropFill}
          onPress={handleClose}
          accessibilityRole="button"
          accessibilityLabel={t('license:close')}
        />
        <View style={styles.modalLayerAboveBackdrop} pointerEvents="box-none">
          <View style={styles.sheet}>
            <Text style={styles.sheetTitle}>{t('license:modalTitle')}</Text>

            <ScrollView
              keyboardShouldPersistTaps="handled"
              showsVerticalScrollIndicator={false}
              contentContainerStyle={styles.scrollContent}>
              {!status && loading ? (
                <ActivityIndicator
                  style={{ marginVertical: SoftSpacing.md }}
                  color={SoftColors.accentDark}
                />
              ) : null}

              {!status && !loading ? (
                <Text style={styles.bodyMuted}>{t('license:loadFailedHint')}</Text>
              ) : null}

              {status || activationSucceeded ? (
                <>
                  <View style={styles.row}>
                    <Text style={styles.label}>{t('license:typeLabel')}</Text>
                    <Text style={styles.valueStrong}>
                      {(() => {
                        if (activationSucceeded) return t('license:typeLicensed');
                        if (!status) return t('license:typeUnknown');
                        const lt = (status.licenseType ?? '').trim().toLowerCase();
                        if (status.isExpired) return t('license:typeExpired');
                        if (lt === 'demo') return t('license:typeDemo');
                        if (status.isTrial) return t('license:typeTrial');
                        if (lt === 'licensed' || lt === 'paid') return t('license:typeLicensed');
                        return t('license:typePaid');
                      })()}
                    </Text>
                  </View>

                  <View style={styles.row}>
                    <Text style={styles.label}>{t('license:expiryLabel')}</Text>
                    <Text style={styles.value}>
                      {displayExpiryIso
                        ? formatExpiryDeAt(displayExpiryIso)
                        : t('license:expiryNone')}
                    </Text>
                  </View>

                  <View style={styles.row}>
                    <Text style={styles.label}>{t('license:daysRemainingLabel')}</Text>
                    <Text style={styles.value}>
                      {unlimitedPaid && !activationSucceeded
                        ? '—'
                        : (() => {
                            const remaining = preferLicenseHoursRemaining(
                              status?.daysRemaining ?? 0,
                              displayExpiryIso
                            );
                            if (remaining?.kind === 'hours') {
                              return t('license:hoursRemainingValue', { count: remaining.hours });
                            }
                            return t('license:daysRemainingValue', {
                              count: status?.daysRemaining ?? 0,
                            });
                          })()}
                    </Text>
                  </View>

                  {status?.machineHash?.trim() ? (
                    <>
                      <Pressable
                        onPress={() => {
                          setTechDetailsOpen((v) => !v);
                        }}
                        style={({ pressed }) => [styles.techToggle, pressed && { opacity: 0.75 }]}
                        accessibilityRole="button">
                        <Text style={styles.techToggleText}>
                          {techDetailsOpen
                            ? t('license:technicalToggleHide')
                            : t('license:technicalToggleShow')}
                        </Text>
                      </Pressable>
                      {techDetailsOpen ? (
                        <View style={styles.techBox}>
                          <Text style={styles.label}>{t('license:machineFingerprintLabel')}</Text>
                          <Text style={styles.techMono} selectable>
                            {status.machineHash.trim()}
                          </Text>
                        </View>
                      ) : null}
                    </>
                  ) : null}

                  {status?.isExpired && !activationSucceeded ? (
                    <View style={styles.warnBox}>
                      <Text style={styles.warnText}>{t('license:warningExpired')}</Text>
                    </View>
                  ) : null}

                  {!loading || activationSucceeded ? (
                    <>
                      {activationSucceeded ? (
                        <View style={styles.successBlock} accessibilityLiveRegion="polite">
                          <Ionicons name="checkmark-circle" size={28} color="#2E7D32" />
                          <Text style={styles.successTitle}>{t('license:activation.success')}</Text>
                          <Text style={styles.successMessage}>
                            {t('license:activation.successMessage', {
                              date: displayExpiryIso
                                ? formatExpiryDeAt(displayExpiryIso)
                                : t('license:expiryNone'),
                            })}
                          </Text>
                          <TouchableOpacity
                            style={styles.successOkBtn}
                            onPress={handleClose}
                            accessibilityRole="button"
                            accessibilityLabel={t('common:continue')}>
                            <Text style={styles.successOkBtnText}>{t('common:continue')}</Text>
                          </TouchableOpacity>
                        </View>
                      ) : (
                        <>
                          {status && !isLicensedActive ? (
                            <Pressable
                              style={({ pressed }) => [
                                styles.openFullActivationBtn,
                                pressed && { opacity: 0.88 },
                              ]}
                              onPress={openFullActivationScreen}
                              accessibilityRole="button"
                              accessibilityLabel={t('license:openActivationFromModal')}>
                              <Ionicons
                                name="document-text-outline"
                                size={18}
                                color={SoftColors.accentDark}
                              />
                              <Text style={styles.openFullActivationBtnText}>
                                {t('license:openActivationFromModal')}
                              </Text>
                            </Pressable>
                          ) : null}

                          {isLicensedActive ? (
                            <View style={styles.alreadyLicensedBox}>
                              <Ionicons name="checkmark-circle" size={18} color="#2E7D32" />
                              <Text style={styles.alreadyLicensedText}>
                                {t('license:modalAlreadyLicensed')}
                              </Text>
                            </View>
                          ) : (
                            <View style={styles.activationBlock}>
                              <Text style={styles.activationLabel}>
                                {t('license:modalActivationInputLabel')}
                              </Text>
                              <View style={styles.activationRow}>
                                <TextInput
                                  value={licenseKey}
                                  onChangeText={onChangeKey}
                                  placeholder={t('license:activationKeyPlaceholder')}
                                  placeholderTextColor={SoftColors.textMuted}
                                  autoCapitalize="characters"
                                  autoCorrect={false}
                                  editable={!submitting}
                                  style={[
                                    styles.activationInput,
                                    submitting && styles.activationInputDisabled,
                                  ]}
                                  accessibilityLabel={t('license:modalActivationInputLabel')}
                                />
                                <TouchableOpacity
                                  style={[
                                    styles.activateBtn,
                                    submitting && styles.activateBtnDisabled,
                                  ]}
                                  onPress={() => void onActivate()}
                                  disabled={submitting}
                                  accessibilityRole="button"
                                  accessibilityLabel={t('license:activationSubmit')}>
                                  {submitting ? (
                                    <ActivityIndicator
                                      size="small"
                                      color={SoftColors.textInverse}
                                    />
                                  ) : (
                                    <Text style={styles.activateBtnText}>
                                      {t('license:activationSubmit')}
                                    </Text>
                                  )}
                                </TouchableOpacity>
                              </View>
                              {submitting ? (
                                <View style={styles.loadingRow}>
                                  <ActivityIndicator size="small" color={SoftColors.accentDark} />
                                  <Text style={styles.loadingText}>
                                    {t('license:activation.loading')}
                                  </Text>
                                </View>
                              ) : (
                                <Text style={styles.activationHint}>
                                  {t('license:activationKeyHint')}
                                </Text>
                              )}
                            </View>
                          )}

                          {activationFeedback?.kind === 'error' ? (
                            <View
                              style={[styles.feedbackBox, styles.feedbackErr]}
                              accessibilityLiveRegion="polite">
                              <Ionicons name="alert-circle" size={16} color="#b71c1c" />
                              <Text style={[styles.feedbackText, styles.feedbackTextErr]}>
                                {activationFeedback.text}
                              </Text>
                            </View>
                          ) : null}

                          <Pressable
                            style={({ pressed }) => [
                              styles.ctaPrimary,
                              pressed && { opacity: 0.88 },
                            ]}
                            onPress={() => void handleExtendLicense()}>
                            <Ionicons
                              name={hasConfiguredExtensionUrl ? 'open-outline' : 'mail-outline'}
                              size={18}
                              color={SoftColors.textInverse}
                            />
                            <Text style={styles.ctaPrimaryText}>{t('license:renewCta')}</Text>
                          </Pressable>
                          <Pressable
                            onPress={() => void handleExtendLicense()}
                            hitSlop={12}
                            style={({ pressed }) => [
                              styles.ctaHintPressable,
                              pressed && { opacity: 0.7 },
                            ]}>
                            <Text style={styles.ctaHint}>
                              {t(
                                hasConfiguredExtensionUrl
                                  ? 'license:renewPrimaryHintBrowser'
                                  : 'license:renewPrimaryHintMail'
                              )}
                            </Text>
                          </Pressable>

                          {hasConfiguredExtensionUrl ? (
                            <Pressable
                              style={styles.ctaSecondary}
                              onPress={() => void openRenewMail()}>
                              <Ionicons
                                name="mail-outline"
                                size={18}
                                color={SoftColors.accentDark}
                              />
                              <Text style={styles.ctaSecondaryText}>
                                {t('license:renewOpenMail')}
                              </Text>
                            </Pressable>
                          ) : null}
                        </>
                      )}
                    </>
                  ) : null}

                  <Text style={styles.contactFoot}>
                    {t('license:contactBody', { email: LICENSE_SUPPORT_EMAIL })}
                  </Text>
                </>
              ) : null}
            </ScrollView>

            <View style={styles.modalFooter}>
              <Pressable style={styles.dismissBtnFlex} onPress={handleClose}>
                <Text style={styles.dismissText}>
                  {activationSucceeded ? t('common:continue') : t('license:close')}
                </Text>
              </Pressable>
              {!activationSucceeded ? (
                <Pressable
                  style={styles.iconRefreshBtn}
                  onPress={() => {
                    void refetch();
                  }}
                  accessibilityRole="button"
                  accessibilityLabel={t('common:retry')}>
                  <Ionicons name="refresh" size={22} color={SoftColors.accentDark} />
                </Pressable>
              ) : null}
            </View>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modalRoot: {
    flex: 1,
  },
  modalBackdropFill: {
    ...StyleSheet.absoluteFill,
    backgroundColor: 'rgba(0,0,0,0.45)',
  },
  modalLayerAboveBackdrop: {
    flex: 1,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
    width: '100%',
    zIndex: 1,
    elevation: 4,
  },
  sheet: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    maxWidth: 420,
    maxHeight: '88%',
    alignSelf: 'center',
    width: '100%',
  },
  scrollContent: {
    paddingBottom: SoftSpacing.sm,
  },
  sheetTitle: {
    ...SoftTypography.h3,
    marginBottom: SoftSpacing.md,
    textAlign: 'center',
    color: SoftColors.textPrimary,
  },
  row: {
    marginBottom: SoftSpacing.sm,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: 2,
  },
  value: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  valueStrong: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  bodyMuted: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
    textAlign: 'center',
  },
  warnBox: {
    backgroundColor: SoftColors.warningBg,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.warning,
  },
  warnText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  alreadyLicensedBox: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    marginTop: SoftSpacing.md,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.successBg,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.success,
  },
  alreadyLicensedText: {
    ...SoftTypography.body,
    flex: 1,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  openFullActivationBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    marginTop: SoftSpacing.md,
    borderWidth: 1,
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.bgPrimary,
  },
  openFullActivationBtnText: {
    ...SoftTypography.label,
    color: SoftColors.accentDark,
    fontWeight: '700',
  },
  activationBlock: {
    marginTop: SoftSpacing.md,
  },
  activationLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.xs,
    fontWeight: '600',
  },
  activationRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  activationInput: {
    flex: 1,
    minWidth: 0,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 10,
    fontSize: 14,
    fontFamily: 'monospace',
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgPrimary,
  },
  activationInputDisabled: {
    opacity: 0.65,
  },
  activationHint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.xs,
  },
  loadingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    marginTop: SoftSpacing.sm,
  },
  loadingText: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  activateBtn: {
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 10,
    minWidth: 100,
    alignItems: 'center',
    justifyContent: 'center',
    minHeight: 44,
  },
  activateBtnDisabled: {
    opacity: 0.65,
  },
  activateBtnText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  successBlock: {
    marginTop: SoftSpacing.md,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: '#e8f5e9',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: '#a5d6a7',
    alignItems: 'center',
    gap: SoftSpacing.xs,
  },
  successTitle: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: '#1b5e20',
    textAlign: 'center',
  },
  successMessage: {
    ...SoftTypography.caption,
    color: '#1b5e20',
    textAlign: 'center',
    marginBottom: SoftSpacing.sm,
  },
  successOkBtn: {
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.sm,
    minHeight: 44,
    minWidth: 120,
    alignItems: 'center',
    justifyContent: 'center',
  },
  successOkBtnText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  feedbackBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: SoftSpacing.xs,
    marginTop: SoftSpacing.sm,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
  },
  feedbackErr: {
    backgroundColor: '#ffebee',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: '#ffcdd2',
  },
  feedbackText: {
    ...SoftTypography.caption,
    flex: 1,
    fontWeight: '600',
  },
  feedbackTextErr: { color: '#b71c1c' },
  techToggle: {
    marginTop: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
  },
  techToggleText: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    textDecorationLine: 'underline',
    fontWeight: '600',
  },
  techBox: {
    marginTop: SoftSpacing.xs,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: SoftColors.borderLight,
  },
  techMono: {
    ...SoftTypography.caption,
    fontFamily: 'monospace',
    color: SoftColors.textPrimary,
    marginTop: 4,
  },
  ctaPrimary: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
  },
  ctaPrimaryText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  ctaHintPressable: {
    alignSelf: 'center',
    marginTop: 4,
    paddingVertical: 4,
    paddingHorizontal: SoftSpacing.sm,
  },
  ctaHint: {
    ...SoftTypography.caption,
    color: SoftColors.accentDark,
    textAlign: 'center',
    textDecorationLine: 'underline',
  },
  ctaSecondary: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    marginTop: SoftSpacing.sm,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgPrimary,
  },
  ctaSecondaryText: {
    ...SoftTypography.label,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  contactFoot: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.sm,
  },
  modalFooter: {
    flexDirection: 'row',
    alignItems: 'stretch',
    marginTop: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  dismissBtnFlex: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    minHeight: 44,
  },
  dismissText: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  iconRefreshBtn: {
    width: 48,
    minHeight: 44,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgCard,
    justifyContent: 'center',
    alignItems: 'center',
  },
});
