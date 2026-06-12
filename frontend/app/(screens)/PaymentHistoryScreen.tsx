import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  FlatList,
  Modal,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { usePosRegisterReadiness } from '../../contexts/PosRegisterReadinessContext';
import { usePosStatusOverview } from '../../contexts/PosStatusOverviewContext';
import { formatTime } from '../../i18n/formatting';
import { getFormattingLocaleForTextLocale } from '../../i18n/localeUtils';
import {
  usePaymentHistory,
  usePaymentHistoryLabels,
  useRefund,
  useStorno,
  type PaymentHistoryAvailableAction,
  type PaymentHistoryItem,
} from '../../hooks/usePaymentHistory';
import type { StornoResponsePayload } from '../../services/api/paymentHistoryService';
import { receiptPrinter } from '../../services/receiptPrinter';
import { WaveLoader } from '../../src/components/common/WaveLoader';
import { formatPrice } from '../../utils/formatPrice';
import {
  filterPaymentHistoryItems,
  type PaymentHistoryFilterType,
} from '../../utils/paymentHistoryFilter';
import { isValidPosCashRegisterId } from '../../utils/posCashRegister';

const FILTER_OPTIONS: PaymentHistoryFilterType[] = ['all', 'storno', 'refund'];

const HISTORY_HOURS = 24;
const HISTORY_LIMIT = 50;

function parseLocaleDecimal(input: string): number {
  const normalized = input.trim().replace(/\s/g, '').replace(',', '.');
  const n = Number(normalized);
  return Number.isFinite(n) ? n : NaN;
}

function isActionable(action: PaymentHistoryAvailableAction): boolean {
  return action.action === 'storno' || action.action === 'refund';
}

function resolveEffectiveRegisterId(
  readinessId?: string | null,
  overviewId?: string | null
): string | null {
  for (const id of [readinessId, overviewId]) {
    if (isValidPosCashRegisterId(id)) return id!.trim();
  }
  return null;
}

export default function PaymentHistoryScreen() {
  const router = useRouter();
  const { t, i18n } = useTranslation(['paymentHistory', 'common']);
  const {
    data: registerData,
    loading: registerLoading,
    refresh: refreshRegister,
  } = usePosRegisterReadiness();
  const { cashRegister: overviewRegister } = usePosStatusOverview();
  const [registerId, setRegisterId] = useState<string | null>(null);

  useEffect(() => {
    setRegisterId(
      resolveEffectiveRegisterId(
        registerData?.effectiveRegisterId,
        overviewRegister?.effectiveRegisterId
      )
    );
  }, [registerData?.effectiveRegisterId, overviewRegister?.effectiveRegisterId]);

  const isRegisterResolving = registerLoading && !registerId;

  const formatLocale = useMemo(
    () => getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language),
    [i18n.language, i18n.resolvedLanguage]
  );

  const history = usePaymentHistory({
    hours: HISTORY_HOURS,
    limit: HISTORY_LIMIT,
    cashRegisterId: registerId ?? undefined,
    enabled: Boolean(registerId),
  });
  const { resolveLabel } = usePaymentHistoryLabels();
  const refreshSilent = useCallback(() => {
    void history.refresh({ silent: true });
  }, [history.refresh]);
  const stornoMutation = useStorno(refreshSilent);
  const refundMutation = useRefund(refreshSilent);

  const [selectedPayment, setSelectedPayment] = useState<PaymentHistoryItem | null>(null);
  const [selectedAction, setSelectedAction] = useState<PaymentHistoryAvailableAction | null>(null);
  const [selectedReasonCode, setSelectedReasonCode] = useState('');
  const [reasonText, setReasonText] = useState('');
  const [refundAmount, setRefundAmount] = useState('');
  const [modalVisible, setModalVisible] = useState(false);
  const [filterType, setFilterType] = useState<PaymentHistoryFilterType>('all');

  const filteredPayments = useMemo(
    () => filterPaymentHistoryItems(history.payments, filterType),
    [history.payments, filterType]
  );

  const filteredTotalAmount = useMemo(
    () => filteredPayments.reduce((sum, payment) => sum + payment.totalAmount, 0),
    [filteredPayments]
  );

  const closeModal = useCallback(() => {
    setModalVisible(false);
    setSelectedPayment(null);
    setSelectedAction(null);
    setSelectedReasonCode('');
    setReasonText('');
    setRefundAmount('');
  }, []);

  const handleActionPress = useCallback(
    (payment: PaymentHistoryItem, action: PaymentHistoryAvailableAction) => {
      setSelectedPayment(payment);
      setSelectedAction(action);
      setSelectedReasonCode('');
      setReasonText('');
      setRefundAmount('');
      setModalVisible(true);
    },
    []
  );

  const printStornoReceipt = useCallback(
    async (stornoPaymentId: string) => {
      if (!selectedPayment) return;
      try {
        await receiptPrinter.printStornoReceipt(
          {
            paymentId: stornoPaymentId,
            receiptNumber: '',
            createdAt: new Date().toISOString(),
            totalAmount: selectedPayment.totalAmount,
            stornoReasonText: reasonText.trim(),
          },
          {
            receiptNumber: selectedPayment.receiptNumber,
            createdAt: selectedPayment.createdAt,
            totalAmount: selectedPayment.totalAmount,
          }
        );
      } catch {
        Alert.alert(t('common:error'), t('paymentHistory:print.stornoFailed'));
      }
    },
    [reasonText, selectedPayment, t]
  );

  const printRefundReceipt = useCallback(
    async (refundPaymentId: string) => {
      if (!selectedPayment) return;
      const amount = parseLocaleDecimal(refundAmount);
      try {
        await receiptPrinter.printRefundReceipt(
          {
            paymentId: refundPaymentId,
            receiptNumber: '',
            createdAt: new Date().toISOString(),
            totalAmount: Number.isFinite(amount) ? amount : 0,
            refundReason: reasonText.trim() || selectedReasonCode,
          },
          {
            receiptNumber: selectedPayment.receiptNumber,
            createdAt: selectedPayment.createdAt,
            totalAmount: selectedPayment.totalAmount,
          }
        );
      } catch {
        Alert.alert(t('common:error'), t('paymentHistory:print.refundFailed'));
      }
    },
    [refundAmount, reasonText, selectedPayment, selectedReasonCode, t]
  );

  const showStornoResult = useCallback(
    (result: StornoResponsePayload) => {
      if (result.success) {
        closeModal();
        if (result.stornoPaymentId) {
          void printStornoReceipt(result.stornoPaymentId);
        }
        Alert.alert(t('common:success'), t('paymentHistory:messages.stornoSuccess'));
        return;
      }
      if (result.requiresApproval) {
        Alert.alert(t('common:error'), t('paymentHistory:errors.approvalRequired'));
        return;
      }
      const message = stornoMutation.translateBackendKey(
        result.errorKey,
        t('paymentHistory:errors.stornoFailed')
      );
      Alert.alert(t('common:error'), message);
    },
    [closeModal, printStornoReceipt, stornoMutation, t]
  );

  const handleSubmit = useCallback(async () => {
    if (!selectedPayment || !selectedAction) return;

    if (selectedAction.requiresReason && !selectedReasonCode) {
      Alert.alert(t('common:error'), t('paymentHistory:errors.reasonRequired'));
      return;
    }

    if (selectedAction.action === 'storno' && reasonText.trim().length < 5) {
      Alert.alert(t('common:error'), t('paymentHistory:errors.reasonTooShort'));
      return;
    }

    if (selectedAction.action === 'storno') {
      const result = await stornoMutation.mutateAsync({
        paymentId: selectedPayment.id,
        reasonCode: selectedReasonCode,
        reason: reasonText.trim(),
      });
      showStornoResult(result);
      return;
    }

    if (selectedAction.action === 'refund') {
      const amount = parseLocaleDecimal(refundAmount);
      if (!Number.isFinite(amount) || amount <= 0) {
        Alert.alert(t('common:error'), t('paymentHistory:errors.refundAmountInvalid'));
        return;
      }

      const result = await refundMutation.mutateAsync({
        paymentId: selectedPayment.id,
        amount,
        reason: reasonText.trim() || selectedReasonCode,
      });

      if (result.success) {
        closeModal();
        if (result.paymentId) {
          void printRefundReceipt(result.paymentId);
        }
        Alert.alert(t('common:success'), t('paymentHistory:messages.refundSuccess'));
      } else {
        const message = refundMutation.translateBackendKey(
          result.errorKey,
          t('paymentHistory:errors.stornoFailed')
        );
        Alert.alert(t('common:error'), message);
      }
    }
  }, [
    closeModal,
    printRefundReceipt,
    refundAmount,
    refundMutation,
    reasonText,
    selectedAction,
    selectedPayment,
    selectedReasonCode,
    showStornoResult,
    stornoMutation,
    t,
  ]);

  const onRefresh = useCallback(async () => {
    await history.refresh();
  }, [history.refresh]);

  const renderPaymentItem = useCallback(
    ({ item }: { item: PaymentHistoryItem }) => {
      const actionable = item.availableActions.filter(isActionable);
      return (
        <View style={styles.paymentItem}>
          <View style={styles.paymentLeft}>
            <Text style={styles.time}>{formatTime(item.createdAt, formatLocale)}</Text>
            <Text style={styles.receiptNumber}>#{item.receiptNumber}</Text>
            {item.tableNumber != null && item.tableNumber > 0 ? (
              <Text style={styles.table}>
                {t('common:table')} {item.tableNumber}
              </Text>
            ) : null}
            {item.isStorno ? (
              <Text style={styles.badgeStorno}>{t('paymentHistory:actions.storno')}</Text>
            ) : null}
            {item.isRefund ? (
              <Text style={styles.badgeRefund}>{t('paymentHistory:actions.refund')}</Text>
            ) : null}
          </View>

          <View style={styles.paymentCenter}>
            <Text style={styles.customerName} numberOfLines={1}>
              {item.customerName}
            </Text>
            <Text style={styles.paymentMethod}>{item.paymentMethod}</Text>
            <Text style={styles.amount}>{formatPrice(item.totalAmount, formatLocale)}</Text>
          </View>

          <View style={styles.paymentRight}>
            {actionable.map((action) => (
              <Pressable
                key={action.action}
                style={[
                  styles.actionButton,
                  action.action === 'refund' ? styles.actionButtonRefund : styles.actionButtonStorno,
                ]}
                onPress={() => handleActionPress(item, action)}
                accessibilityRole="button"
                accessibilityLabel={resolveLabel(action.labelKey)}
              >
                <Text style={styles.actionText}>{resolveLabel(action.labelKey)}</Text>
              </Pressable>
            ))}
          </View>
        </View>
      );
    },
    [formatLocale, handleActionPress, resolveLabel, t]
  );

  if (isRegisterResolving) {
    return (
      <SafeAreaView style={styles.container} edges={['top']}>
        <View style={styles.centerContainer}>
          <WaveLoader />
          <Text style={styles.loadingText}>{t('paymentHistory:preparingRegister')}</Text>
        </View>
      </SafeAreaView>
    );
  }

  if (!registerId) {
    return (
      <SafeAreaView style={styles.container} edges={['top']}>
        <View style={styles.header}>
          <Pressable
            onPress={() => router.back()}
            style={styles.backButton}
            accessibilityRole="button"
            accessibilityLabel={t('common:back')}
          >
            <Ionicons name="arrow-back" size={24} color="#333" />
          </Pressable>
          <View style={styles.headerText}>
            <Text style={styles.title}>{t('paymentHistory:title')}</Text>
          </View>
        </View>
        <View style={styles.centerContainer}>
          <Ionicons name="warning-outline" size={48} color="#ef6c00" accessibilityElementsHidden />
          <Text style={styles.warningTitle}>{t('paymentHistory:noRegisterTitle')}</Text>
          <Text style={styles.warningText}>{t('paymentHistory:noRegister')}</Text>
          <Pressable
            onPress={() => router.push('/(tabs)/settings' as const)}
            style={styles.selectRegisterButton}
            accessibilityRole="button"
            accessibilityLabel={t('paymentHistory:selectRegister')}
          >
            <Text style={styles.selectRegisterButtonText}>{t('paymentHistory:selectRegister')}</Text>
          </Pressable>
          <Pressable
            onPress={() => refreshRegister()}
            style={styles.retryLinkButton}
            accessibilityRole="button"
            accessibilityLabel={t('common:retry')}
          >
            <Text style={styles.retryLinkText}>{t('common:retry')}</Text>
          </Pressable>
        </View>
      </SafeAreaView>
    );
  }

  if (history.isLoading && history.payments.length === 0) {
    return (
      <SafeAreaView style={styles.container} edges={['top']}>
        <View style={styles.centerContainer}>
          <WaveLoader />
          <Text style={styles.loadingText}>{t('common:loading')}</Text>
        </View>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <View style={styles.header}>
        <Pressable
          onPress={() => router.back()}
          style={styles.backButton}
          accessibilityRole="button"
          accessibilityLabel={t('common:back')}
        >
          <Ionicons name="arrow-back" size={24} color="#333" />
        </Pressable>
        <View style={styles.headerText}>
          <Text style={styles.title}>{t('paymentHistory:title')}</Text>
          <Text style={styles.subtitle}>{t('paymentHistory:last24h')}</Text>
        </View>
      </View>

      {history.error ? (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{history.error}</Text>
          <Pressable onPress={() => void history.refresh()} style={styles.retryButton}>
            <Text style={styles.retryText}>{t('common:retry')}</Text>
          </Pressable>
        </View>
      ) : null}

      <View style={styles.filterRow}>
        {FILTER_OPTIONS.map((option) => {
          const active = filterType === option;
          return (
            <Pressable
              key={option}
              onPress={() => setFilterType(option)}
              style={[styles.filterChip, active && styles.filterChipActive]}
              accessibilityRole="button"
              accessibilityState={{ selected: active }}
              accessibilityLabel={t(`paymentHistory:filters.${option}`)}
            >
              <Text style={[styles.filterChipText, active && styles.filterChipTextActive]}>
                {t(`paymentHistory:filters.${option}`)}
              </Text>
            </Pressable>
          );
        })}
      </View>

      <View style={styles.stats}>
        <View style={styles.statBox}>
          <Text style={styles.statValue}>{filteredPayments.length}</Text>
          <Text style={styles.statLabel}>{t('paymentHistory:transactions')}</Text>
        </View>
        <View style={styles.statBox}>
          <Text style={styles.statValue}>{formatPrice(filteredTotalAmount, formatLocale)}</Text>
          <Text style={styles.statLabel}>{t('paymentHistory:total')}</Text>
        </View>
      </View>

      <FlatList
        data={filteredPayments}
        renderItem={renderPaymentItem}
        keyExtractor={(item) => item.id}
        contentContainerStyle={
          filteredPayments.length === 0 ? styles.emptyList : styles.listContent
        }
        refreshControl={
          <RefreshControl
            refreshing={history.isRefetching}
            onRefresh={() => {
              void onRefresh();
            }}
          />
        }
        ListEmptyComponent={
          <View style={styles.empty}>
            <Text style={styles.emptyText}>
              {history.payments.length === 0
                ? t('paymentHistory:noPayments')
                : t('paymentHistory:filters.noResults')}
            </Text>
          </View>
        }
      />

      <Modal visible={modalVisible} transparent animationType="slide" onRequestClose={closeModal}>
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <Text style={styles.modalTitle}>
              {selectedAction ? resolveLabel(selectedAction.labelKey) : ''}
            </Text>

            {selectedAction?.requiresReason ? (
              <>
                <Text style={styles.modalLabel}>
                  {resolveLabel(
                    selectedAction.reasonLabelKey ?? 'paymentHistory.reasons.stornoTitle'
                  )}
                </Text>
                {selectedAction.reasonOptions.map((opt) => (
                  <Pressable
                    key={opt.code}
                    style={[
                      styles.reasonOption,
                      selectedReasonCode === opt.code && styles.reasonSelected,
                    ]}
                    onPress={() => setSelectedReasonCode(opt.code)}
                  >
                    <Text style={styles.reasonOptionText}>{resolveLabel(opt.labelKey)}</Text>
                  </Pressable>
                ))}

                <TextInput
                  style={styles.textArea}
                  placeholder={t('paymentHistory:fields.detailsNote')}
                  value={reasonText}
                  onChangeText={setReasonText}
                  multiline
                  numberOfLines={3}
                />
              </>
            ) : null}

            {selectedAction?.action === 'refund' ? (
              <TextInput
                style={styles.input}
                keyboardType="decimal-pad"
                placeholder={t('paymentHistory:fields.amount')}
                value={refundAmount}
                onChangeText={setRefundAmount}
              />
            ) : null}

            <View style={styles.modalButtons}>
              <Pressable style={styles.cancelButton} onPress={closeModal}>
                <Text style={styles.cancelButtonText}>{t('common:cancel')}</Text>
              </Pressable>
              <Pressable
                style={[
                  styles.confirmButton,
                  (stornoMutation.isPending || refundMutation.isPending) && styles.confirmDisabled,
                ]}
                onPress={() => {
                  void handleSubmit();
                }}
                disabled={stornoMutation.isPending || refundMutation.isPending}
              >
                <Text style={styles.confirmButtonText}>
                  {stornoMutation.isPending || refundMutation.isPending
                    ? t('common:loading')
                    : t('common:confirm')}
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  centerContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
    gap: 12,
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
  },
  warningTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#333',
    textAlign: 'center',
    marginTop: 8,
  },
  warningText: {
    fontSize: 15,
    color: '#666',
    textAlign: 'center',
    lineHeight: 22,
  },
  selectRegisterButton: {
    marginTop: 16,
    backgroundColor: '#007AFF',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
    minWidth: 200,
    alignItems: 'center',
  },
  selectRegisterButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  retryLinkButton: {
    marginTop: 8,
    padding: 8,
  },
  retryLinkText: {
    color: '#007AFF',
    fontSize: 14,
    fontWeight: '600',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  backButton: {
    padding: 8,
    marginRight: 4,
  },
  headerText: {
    flex: 1,
  },
  title: {
    fontSize: 22,
    fontWeight: 'bold',
    color: '#333',
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  errorBanner: {
    backgroundColor: '#ffebee',
    padding: 12,
    marginHorizontal: 12,
    marginTop: 8,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 8,
  },
  errorText: {
    flex: 1,
    color: '#c62828',
    fontSize: 14,
  },
  retryButton: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    backgroundColor: '#fff',
    borderRadius: 6,
  },
  retryText: {
    color: '#007AFF',
    fontWeight: '600',
  },
  filterRow: {
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 12,
    paddingTop: 8,
    paddingBottom: 4,
    backgroundColor: '#fff',
  },
  filterChip: {
    flex: 1,
    paddingVertical: 10,
    paddingHorizontal: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    backgroundColor: '#f8f9fa',
    alignItems: 'center',
  },
  filterChipActive: {
    borderColor: '#007AFF',
    backgroundColor: '#e3f2fd',
  },
  filterChipText: {
    fontSize: 13,
    fontWeight: '600',
    color: '#666',
  },
  filterChipTextActive: {
    color: '#007AFF',
  },
  stats: {
    flexDirection: 'row',
    padding: 16,
    backgroundColor: '#fff',
    marginBottom: 8,
    gap: 12,
  },
  statBox: {
    flex: 1,
    alignItems: 'center',
    padding: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
  },
  statValue: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
    textAlign: 'center',
  },
  listContent: {
    paddingBottom: 24,
  },
  emptyList: {
    flexGrow: 1,
  },
  empty: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  emptyText: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
  },
  paymentItem: {
    flexDirection: 'row',
    backgroundColor: '#fff',
    marginHorizontal: 12,
    marginVertical: 6,
    padding: 12,
    borderRadius: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 3,
    elevation: 2,
    gap: 8,
  },
  paymentLeft: {
    width: 88,
  },
  time: {
    fontSize: 13,
    fontWeight: '600',
    color: '#333',
  },
  receiptNumber: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  table: {
    fontSize: 11,
    color: '#888',
    marginTop: 2,
  },
  badgeStorno: {
    marginTop: 4,
    fontSize: 10,
    color: '#c62828',
    fontWeight: '700',
  },
  badgeRefund: {
    marginTop: 4,
    fontSize: 10,
    color: '#ef6c00',
    fontWeight: '700',
  },
  paymentCenter: {
    flex: 1,
    minWidth: 0,
  },
  customerName: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  paymentMethod: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  amount: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#007AFF',
    marginTop: 4,
  },
  paymentRight: {
    justifyContent: 'center',
    gap: 6,
    maxWidth: 110,
  },
  actionButton: {
    paddingHorizontal: 8,
    paddingVertical: 6,
    borderRadius: 6,
    alignItems: 'center',
  },
  actionButtonStorno: {
    backgroundColor: '#ffebee',
  },
  actionButtonRefund: {
    backgroundColor: '#fff3e0',
  },
  actionText: {
    fontSize: 11,
    fontWeight: '700',
    color: '#333',
    textAlign: 'center',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    padding: 20,
    maxHeight: '85%',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
  modalLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  reasonOption: {
    padding: 12,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    marginBottom: 8,
  },
  reasonSelected: {
    borderColor: '#007AFF',
    backgroundColor: '#e3f2fd',
  },
  reasonOptionText: {
    fontSize: 14,
    color: '#333',
  },
  textArea: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    minHeight: 80,
    textAlignVertical: 'top',
    marginTop: 4,
    marginBottom: 12,
  },
  input: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
    fontSize: 16,
  },
  modalButtons: {
    flexDirection: 'row',
    gap: 12,
    marginTop: 8,
  },
  cancelButton: {
    flex: 1,
    padding: 14,
    borderRadius: 8,
    backgroundColor: '#f0f0f0',
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#333',
    fontWeight: '600',
  },
  confirmButton: {
    flex: 1,
    padding: 14,
    borderRadius: 8,
    backgroundColor: '#007AFF',
    alignItems: 'center',
  },
  confirmDisabled: {
    opacity: 0.6,
  },
  confirmButtonText: {
    fontSize: 16,
    color: '#fff',
    fontWeight: '700',
  },
});
