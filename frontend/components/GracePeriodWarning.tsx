import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Modal,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { useMandantLicenseWarning } from '../hooks/useMandantLicenseWarning';
import { areLicenseChecksBypassedInDevelopment } from '../utils/licenseCriticalActionGuard';
import {
  isGracePeriodWarningUrgent,
  shouldAutoShowGracePeriodModal,
} from '../utils/gracePeriodWarning';
import { openLicenseExtension } from '../utils/openAdmin';

/**
 * POS grace-period banner + modal while mandant license is in the grace window.
 * Auto-opens the modal once per remaining-day value when ≤5 days remain.
 */
export function GracePeriodWarning() {
  const { t } = useTranslation('license');
  const { shouldShowGrace, state } = useMandantLicenseWarning();
  const { status: deploymentStatus } = useLicenseStatus();
  const [showModal, setShowModal] = useState(false);
  const autoShownForDaysRef = useRef<number | null>(null);

  const daysLeft = state?.gracePeriodRemaining ?? 0;
  const isUrgent = isGracePeriodWarningUrgent(daysLeft);

  useEffect(() => {
    if (!shouldShowGrace) {
      autoShownForDaysRef.current = null;
      setShowModal(false);
      return;
    }

    if (!shouldAutoShowGracePeriodModal(true, daysLeft)) return;
    if (autoShownForDaysRef.current === daysLeft) return;

    autoShownForDaysRef.current = daysLeft;
    setShowModal(true);
  }, [shouldShowGrace, daysLeft]);

  const onRenew = useCallback(() => {
    setShowModal(false);
    const machineHash = deploymentStatus?.machineHash ?? '';
    void openLicenseExtension(machineHash);
  }, [deploymentStatus?.machineHash]);

  if (areLicenseChecksBypassedInDevelopment()) return null;
  if (!shouldShowGrace || !state) return null;
  if (state.canAccess === false) return null;

  const bannerBg = isUrgent ? '#cf1322' : '#faad14';
  const titleColor = isUrgent ? '#cf1322' : '#d48806';
  const primaryBtnBg = isUrgent ? '#cf1322' : '#faad14';

  return (
    <>
      <View
        style={[styles.banner, { backgroundColor: bannerBg }]}
        accessibilityRole="alert"
      >
        <Text style={styles.bannerText} numberOfLines={2}>
          {isUrgent
            ? t('gracePeriodWarning.bannerUrgent', { days: daysLeft })
            : t('gracePeriodWarning.banner', { days: daysLeft })}
        </Text>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('gracePeriodWarning.details')}
          onPress={() => setShowModal(true)}
          style={({ pressed }) => [styles.detailsHit, pressed && styles.pressed]}
        >
          <Text style={styles.bannerLink}>{t('gracePeriodWarning.details')}</Text>
        </Pressable>
      </View>

      <Modal
        animationType="slide"
        transparent
        visible={showModal}
        onRequestClose={() => setShowModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent} accessibilityRole="summary">
            <Text style={[styles.modalTitle, { color: titleColor }]}>
              {isUrgent
                ? t('gracePeriodWarning.modalTitleUrgent')
                : t('gracePeriodWarning.modalTitle')}
            </Text>
            <Text style={styles.modalText}>
              {t('gracePeriodWarning.modalBody', { days: daysLeft })}
            </Text>
            <Text style={styles.modalWarning}>
              {t('gracePeriodWarning.modalLockdown')}
            </Text>
            <View style={styles.modalActions}>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={t('gracePeriodWarning.renewNow')}
                onPress={onRenew}
                style={({ pressed }) => [
                  styles.modalButton,
                  { backgroundColor: primaryBtnBg },
                  pressed && styles.pressed,
                ]}
              >
                <Text style={styles.modalButtonText}>
                  {t('gracePeriodWarning.renewNow')}
                </Text>
              </Pressable>
              <Pressable
                accessibilityRole="button"
                accessibilityLabel={t('gracePeriodWarning.later')}
                onPress={() => setShowModal(false)}
                style={({ pressed }) => [
                  styles.modalSecondaryButton,
                  pressed && styles.pressed,
                ]}
              >
                <Text style={styles.modalSecondaryButtonText}>
                  {t('gracePeriodWarning.later')}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  banner: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  bannerText: {
    flex: 1,
    color: '#ffffff',
    fontWeight: '700',
    fontSize: 14,
  },
  detailsHit: {
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: SoftSpacing.xs,
  },
  bannerLink: {
    color: '#ffffff',
    textDecorationLine: 'underline',
    fontSize: 14,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
  },
  modalContent: {
    backgroundColor: '#ffffff',
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    width: '100%',
    maxWidth: 400,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: '700',
    marginBottom: SoftSpacing.sm,
  },
  modalText: {
    fontSize: 16,
    color: '#333333',
    marginBottom: SoftSpacing.xs,
    lineHeight: 22,
  },
  modalWarning: {
    fontSize: 14,
    color: '#cf1322',
    marginBottom: SoftSpacing.md,
    lineHeight: 20,
  },
  modalActions: {
    flexDirection: 'column',
    gap: SoftSpacing.sm,
  },
  modalButton: {
    paddingVertical: 14,
    borderRadius: SoftRadius.md,
    alignItems: 'center',
  },
  modalButtonText: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '700',
  },
  modalSecondaryButton: {
    paddingVertical: 14,
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#d9d9d9',
  },
  modalSecondaryButtonText: {
    color: '#666666',
    fontSize: 16,
    fontWeight: '600',
  },
  pressed: {
    opacity: 0.85,
  },
});
