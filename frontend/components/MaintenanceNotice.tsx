import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useMaintenance } from '../contexts/MaintenanceContext';
import { useMaintenanceNotifications } from '../hooks/useMaintenanceNotifications';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';

/**
 * POS maintenance notice — modal when force-display / within force window;
 * otherwise a dismissible top banner. Never blocks cart/payment APIs by itself
 * (payment block lives in MaintenanceContext / PaymentModal).
 *
 * Super Admin never gets the blocking force modal — they see a warning banner
 * with an optional "end maintenance" action (backend already bypasses write locks).
 */
export function MaintenanceNotice() {
  const { t, i18n } = useTranslation(['system', 'common']);
  const {
    activeNotification,
    isForceDisplay,
    canDismiss,
    countdownLabel,
    dismissNotification,
    refresh: refreshNotifications,
  } = useMaintenanceNotifications();
  const { canBypass, disableMaintenance, isDisabling, status, refresh: refreshMode } =
    useMaintenance();
  const [modalVisible, setModalVisible] = useState(false);

  useEffect(() => {
    if (!activeNotification) {
      setModalVisible(false);
      return;
    }
    // Super Admin: never open the blocking force modal.
    setModalVisible(isForceDisplay && !canBypass);
  }, [activeNotification, isForceDisplay, canBypass]);

  const formatLocale = getFormattingLocaleForTextLocale(i18n.language);

  const handleEndMaintenance = () => {
    void (async () => {
      try {
        await disableMaintenance();
        await Promise.all([refreshMode(), refreshNotifications()]);
      } catch {
        Alert.alert(
          t('system:maintenanceNotice.modalTitle'),
          t('system:maintenanceNotice.endFailed')
        );
      }
    })();
  };

  // Limited mode active for Super Admin with no force notification UI path.
  if (!activeNotification) {
    if (canBypass && status?.isActive) {
      const endLabel = status.scheduledEndAt
        ? new Date(status.scheduledEndAt).toLocaleString(formatLocale)
        : null;
      return (
        <View style={styles.superAdminBanner} accessibilityRole="summary">
          <View style={styles.bannerTextWrap}>
            <Text style={styles.superAdminTitle}>
              {t('system:maintenanceNotice.superAdminBannerTitle')}
            </Text>
            <Text style={styles.superAdminBody} numberOfLines={3}>
              {status.message?.trim() ||
                t('system:maintenanceNotice.superAdminBannerDescription')}
            </Text>
            {endLabel ? (
              <Text style={styles.bannerMeta}>
                {t('system:maintenanceNotice.scheduledEnd')}: {endLabel}
              </Text>
            ) : null}
          </View>
          <Pressable
            accessibilityRole="button"
            disabled={isDisabling}
            onPress={handleEndMaintenance}
            style={[styles.endChip, isDisabling && styles.endChipDisabled]}
          >
            <Text style={styles.endChipText}>
              {isDisabling
                ? t('system:maintenanceNotice.disabling')
                : t('system:maintenanceNotice.endMaintenance')}
            </Text>
          </Pressable>
        </View>
      );
    }
    return null;
  }

  const startLabel = new Date(activeNotification.scheduledStartAt).toLocaleString(formatLocale);
  const endLabel = new Date(activeNotification.scheduledEndAt).toLocaleString(formatLocale);

  // Super Admin bypass: non-blocking banner instead of force modal.
  if (isForceDisplay && canBypass) {
    return (
      <View style={styles.superAdminBanner} accessibilityRole="summary">
        <View style={styles.bannerTextWrap}>
          <Text style={styles.superAdminTitle}>
            {t('system:maintenanceNotice.superAdminBannerTitle')}
          </Text>
          <Text style={styles.superAdminBody} numberOfLines={3}>
            {activeNotification.message ||
              t('system:maintenanceNotice.superAdminBannerDescription')}
          </Text>
          <Text style={styles.bannerMeta}>
            {t('system:maintenanceNotice.windowLabel')} {startLabel} → {endLabel}
          </Text>
          {countdownLabel ? (
            <Text style={styles.bannerMeta}>
              {t('system:maintenanceNotice.countdownEndsLabel')}: {countdownLabel}
            </Text>
          ) : null}
        </View>
        <Pressable
          accessibilityRole="button"
          disabled={isDisabling}
          onPress={handleEndMaintenance}
          style={[styles.endChip, isDisabling && styles.endChipDisabled]}
        >
          <Text style={styles.endChipText}>
            {isDisabling
              ? t('system:maintenanceNotice.disabling')
              : t('system:maintenanceNotice.endMaintenance')}
          </Text>
        </Pressable>
      </View>
    );
  }

  if (isForceDisplay) {
    return (
      <Modal
        animationType="slide"
        transparent
        visible={modalVisible}
        onRequestClose={() => {
          // Mandatory / force-display: keep open.
        }}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent} accessibilityRole="alert">
            <View style={styles.warningHeader}>
              <Text style={styles.warningTitle}>{t('system:maintenanceNotice.modalTitle')}</Text>
            </View>

            <Text style={styles.messageTitle}>{activeNotification.title}</Text>
            <Text style={styles.messageBody}>{activeNotification.message}</Text>

            <View style={styles.timeContainer}>
              <Text style={styles.timeLabel}>{t('system:maintenanceNotice.windowLabel')}</Text>
              <Text style={styles.timeValue}>{startLabel}</Text>
              <Text style={styles.timeValue}>→ {endLabel}</Text>
            </View>

            <View style={styles.countdownContainer}>
              <Text style={styles.countdownLabel}>
                {t('system:maintenanceNotice.countdownEndsLabel')}
              </Text>
              <Text style={styles.countdownValue}>{countdownLabel}</Text>
            </View>

            <View style={styles.forceNotice}>
              <Text style={styles.forceText}>{t('system:maintenanceNotice.forceNotice')}</Text>
            </View>

            <Text style={styles.thanksNote}>{t('system:maintenanceNotice.note')}</Text>
          </View>
        </View>
      </Modal>
    );
  }

  return (
    <View style={styles.banner} accessibilityRole="summary">
      <View style={styles.bannerTextWrap}>
        <Text style={styles.bannerTitle}>{activeNotification.title}</Text>
        <Text style={styles.bannerBody} numberOfLines={2}>
          {activeNotification.message}
        </Text>
        <Text style={styles.bannerMeta}>
          {t('system:maintenanceNotice.starts')}: {startLabel}
        </Text>
      </View>
      {canDismiss ? (
        <Pressable
          accessibilityRole="button"
          onPress={() => void dismissNotification(activeNotification.id)}
          style={styles.dismissChip}
        >
          <Text style={styles.dismissChipText}>{t('system:maintenanceNotice.dismiss')}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.55)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.md,
  },
  modalContent: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    width: '100%',
    maxWidth: 400,
  },
  warningHeader: {
    marginBottom: SoftSpacing.md,
  },
  warningTitle: {
    fontSize: 18,
    fontWeight: '700',
    color: SoftColors.error,
  },
  messageTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
  },
  messageBody: {
    fontSize: 14,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
  },
  timeContainer: {
    backgroundColor: SoftColors.bgSecondary,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    marginBottom: SoftSpacing.sm,
  },
  timeLabel: {
    fontSize: 12,
    color: SoftColors.textSecondary,
  },
  timeValue: {
    fontSize: 14,
    fontWeight: '500',
    marginTop: 2,
    color: SoftColors.textPrimary,
  },
  countdownContainer: {
    alignItems: 'center',
    padding: SoftSpacing.md,
    backgroundColor: SoftColors.warningBg,
    borderRadius: SoftRadius.md,
    marginBottom: SoftSpacing.md,
  },
  countdownLabel: {
    fontSize: 12,
    color: SoftColors.textSecondary,
  },
  countdownValue: {
    marginTop: 4,
    fontSize: 20,
    fontVariant: ['tabular-nums'],
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  forceNotice: {
    backgroundColor: SoftColors.errorBg,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
  },
  forceText: {
    fontSize: 13,
    color: SoftColors.error,
    textAlign: 'center',
  },
  thanksNote: {
    marginTop: SoftSpacing.md,
    fontSize: 13,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    fontStyle: 'italic',
  },
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: SoftColors.infoBg,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  superAdminBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: SoftColors.warningBg,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: SoftColors.warning,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  bannerTextWrap: {
    flex: 1,
  },
  bannerTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  bannerBody: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  superAdminTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  superAdminBody: {
    fontSize: 13,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  bannerMeta: {
    fontSize: 12,
    color: SoftColors.textSecondary,
    marginTop: 4,
  },
  dismissChip: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  dismissChipText: {
    color: SoftColors.textInverse,
    fontSize: 13,
    fontWeight: '600',
  },
  endChip: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.error,
  },
  endChipDisabled: {
    opacity: 0.6,
  },
  endChipText: {
    color: SoftColors.textInverse,
    fontSize: 13,
    fontWeight: '600',
  },
});
