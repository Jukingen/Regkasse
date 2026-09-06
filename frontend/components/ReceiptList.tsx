import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  Alert,
  FlatList,
  Modal,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';

import { StornoModal } from './StornoModal';
import {
  POS_RECEIPT_REPRINT_REASONS,
  cancelReceipt,
  fetchReceiptById,
  reprintReceipt,
  type PosReceiptListItem,
} from '../services/api/receiptService';
import { readPosApiErrorMessage } from '../utils/readPosApiErrorMessage';
import { receiptPrinter } from '../services/receiptPrinter';
import {
  canShowReceiptStornoButton,
  type PosReceiptStornoActor,
} from '../utils/posReceiptStorno';
import { WaveLoader } from '../src/components/common/WaveLoader';
import {
  SoftColors,
  SoftRadius,
  SoftSpacing,
  SoftTypography,
} from '../constants/SoftTheme';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import { formatUserDateTime } from '../utils/dateFormatter';
import { isPrintCancelled } from '../utils/expoPrintShare';
import { formatPrice } from '../utils/formatPrice';
import { normalizeReceiptDto } from '../utils/normalizeReceiptDto';

function receiptStatusLabel(status: string, t: (key: string) => string): string {
  switch (status) {
    case 'Storno':
      return t('receipts:statusStorno');
    case 'Refund':
      return t('receipts:statusRefund');
    case 'Paid':
      return t('receipts:statusPaid');
    default:
      return status || t('receipts:statusPaid');
  }
}

export type ReceiptListProps = {
  receipts: PosReceiptListItem[];
  cashRegisterId: string;
  loading: boolean;
  refreshing: boolean;
  onRefresh: () => void;
  canReprint: boolean;
  canStorno?: boolean;
  stornoActor?: PosReceiptStornoActor | null;
  formatLocale: string;
  emptyMessage: string;
};

export function ReceiptList({
  receipts,
  cashRegisterId,
  loading,
  refreshing,
  onRefresh,
  canReprint,
  canStorno = false,
  stornoActor = null,
  formatLocale,
  emptyMessage,
}: ReceiptListProps) {
  const { t } = useTranslation(['receipts', 'common']);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selected, setSelected] = useState<PosReceiptListItem | null>(null);
  const [detail, setDetail] = useState<ReceiptDTO | null>(null);
  const [reprintingId, setReprintingId] = useState<string | null>(null);
  const [stornoTarget, setStornoTarget] = useState<PosReceiptListItem | null>(null);
  const [stornoSubmitting, setStornoSubmitting] = useState(false);
  const [toast, setToast] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const showToast = useCallback((type: 'success' | 'error', message: string) => {
    setToast({ type, message });
    setTimeout(() => setToast(null), 4000);
  }, []);

  const rowAllowsStorno = useCallback(
    (row: PosReceiptListItem) =>
      canStorno &&
      canShowReceiptStornoButton(row, stornoActor),
    [canStorno, stornoActor]
  );

  const stornoErrorMessage = useCallback(
    (errorKey?: string | null, diagnostic?: string | null, fallbackErr?: unknown) => {
      switch (errorKey) {
        case 'errors.alreadyCancelled':
        case 'errors.specialReceiptNotStornoable':
          return t('receipts:stornoNotAvailable');
        case 'errors.stornoNotOwnReceipt':
          return t('receipts:stornoNotOwnReceipt');
        case 'errors.stornoTimeLimitExceeded':
          return t('receipts:stornoNotToday');
        case 'errors.approvalRequired':
          return t('receipts:stornoApprovalRequired');
        case 'errors.reasonRequired':
          return t('receipts:stornoReasonTooShort');
        default:
          break;
      }
      const raw = fallbackErr
        ? readPosApiErrorMessage(fallbackErr, diagnostic || t('receipts:stornoFailed'))
        : diagnostic || t('receipts:stornoFailed');
      return t('receipts:stornoFailedWithError', { error: raw });
    },
    [t]
  );

  const closeDetail = useCallback(() => {
    setDetailVisible(false);
    setSelected(null);
    setDetail(null);
    setDetailLoading(false);
  }, []);

  const openDetail = useCallback(
    async (row: PosReceiptListItem) => {
      setSelected(row);
      setDetail(null);
      setDetailVisible(true);
      setDetailLoading(true);
      const dto = await fetchReceiptById({
        receiptId: row.receiptId,
        cashRegisterId,
      });
      setDetailLoading(false);
      if (!dto) {
        Alert.alert(t('common:error'), t('receipts:detailLoadError'));
        return;
      }
      setDetail(normalizeReceiptDto(dto));
    },
    [cashRegisterId, t]
  );

  const handleReprint = useCallback(
    async (row: PosReceiptListItem) => {
      if (!canReprint) {
        Alert.alert(t('common:error'), t('receipts:reprintNoPermission'));
        return;
      }
      setReprintingId(row.receiptId);
      try {
        const result = await reprintReceipt({
          receiptId: row.receiptId,
          cashRegisterId,
          reasonCode: POS_RECEIPT_REPRINT_REASONS.CUSTOMER_REQUEST,
        });
        await receiptPrinter.print(result.paymentId);
      } catch (err) {
        if (!isPrintCancelled(err)) {
          Alert.alert(t('common:error'), t('receipts:reprintFailed'));
        }
      } finally {
        setReprintingId(null);
      }
    },
    [canReprint, cashRegisterId, t]
  );

  const openStorno = useCallback(
    (row: PosReceiptListItem) => {
      if (!canStorno) {
        Alert.alert(t('common:error'), t('receipts:stornoNoPermission'));
        return;
      }
      if (!rowAllowsStorno(row)) {
        Alert.alert(t('common:error'), t('receipts:stornoNotAvailable'));
        return;
      }
      setStornoTarget(row);
    },
    [canStorno, rowAllowsStorno, t]
  );

  const submitStorno = useCallback(
    async (reason: string) => {
      if (!stornoTarget) return;
      const trimmed = reason.trim();
      if (trimmed.length > 0 && trimmed.length < 5) {
        showToast('error', t('receipts:stornoReasonTooShort'));
        return;
      }
      setStornoSubmitting(true);
      try {
        const result = await cancelReceipt({
          receiptId: stornoTarget.receiptId,
          cashRegisterId,
          reason,
        });
        if (!result.success) {
          showToast('error', stornoErrorMessage(result.errorKey, result.diagnosticCode));
          return;
        }
        setStornoTarget(null);
        showToast('success', t('receipts:stornoSuccess'));
        onRefresh();
      } catch (err) {
        showToast('error', stornoErrorMessage(null, null, err));
      } finally {
        setStornoSubmitting(false);
      }
    },
    [cashRegisterId, onRefresh, showToast, stornoErrorMessage, stornoTarget, t]
  );

  const renderItem = useCallback(
    ({ item }: { item: PosReceiptListItem }) => {
      const busy = reprintingId === item.receiptId || stornoTarget?.receiptId === item.receiptId;
      const canStornoRow = rowAllowsStorno(item);
      return (
        <View style={styles.row}>
          <Pressable
            style={styles.rowMain}
            onPress={() => {
              void openDetail(item);
            }}
            accessibilityRole="button"
            accessibilityLabel={t('receipts:openA11y', { number: item.receiptNumber })}>
            <Text style={[styles.receiptNumber, styles.colNumber]} numberOfLines={1}>
              {item.receiptNumber}
            </Text>
            <Text style={[styles.meta, styles.colDate]} numberOfLines={1}>
              {formatUserDateTime(item.issuedAt) || '—'}
            </Text>
            <Text style={[styles.amount, styles.colAmount]} numberOfLines={1}>
              {formatPrice(item.grandTotal, formatLocale)}
            </Text>
            <Text style={[styles.status, styles.colStatus]} numberOfLines={1}>
              {receiptStatusLabel(item.status, (key) => t(key))}
            </Text>
          </Pressable>
          <View style={styles.colAction}>
            {canReprint ? (
              <Pressable
                style={[styles.reprintButton, busy && styles.reprintButtonDisabled]}
                onPress={() => {
                  void handleReprint(item);
                }}
                disabled={busy}
                accessibilityRole="button"
                accessibilityLabel={t('receipts:reprintA11y', { number: item.receiptNumber })}>
                {busy && reprintingId === item.receiptId ? (
                  <ActivityIndicator size="small" color={SoftColors.textInverse} />
                ) : (
                  <Text style={styles.reprintButtonText}>{t('receipts:reprint')}</Text>
                )}
              </Pressable>
            ) : null}
            {canStornoRow ? (
              <Pressable
                style={[styles.stornoButton, busy && styles.reprintButtonDisabled]}
                onPress={() => openStorno(item)}
                disabled={busy}
                accessibilityRole="button"
                accessibilityLabel={t('receipts:stornoA11y', { number: item.receiptNumber })}>
                {busy && stornoTarget?.receiptId === item.receiptId ? (
                  <ActivityIndicator size="small" color={SoftColors.textInverse} />
                ) : (
                  <Text style={styles.stornoButtonText}>{t('receipts:storno')}</Text>
                )}
              </Pressable>
            ) : null}
          </View>
        </View>
      );
    },
    [canReprint, formatLocale, handleReprint, openDetail, openStorno, reprintingId, rowAllowsStorno, stornoTarget, t]
  );

  if (loading && receipts.length === 0) {
    return (
      <View style={styles.centered}>
        <WaveLoader size={32} color={SoftColors.accent} />
      </View>
    );
  }

  return (
    <View style={styles.root}>
      <FlatList
        data={receipts}
        keyExtractor={(item) => item.receiptId}
        renderItem={renderItem}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            tintColor={SoftColors.accent}
          />
        }
        contentContainerStyle={receipts.length === 0 ? styles.emptyList : styles.listContent}
        ListHeaderComponent={
          receipts.length === 0 ? null : (
            <View style={styles.columnHeader} accessibilityRole="header">
              <Text style={[styles.columnHeaderText, styles.colNumber]}>{t('receipts:receiptNumber')}</Text>
              <Text style={[styles.columnHeaderText, styles.colDate]}>{t('receipts:issuedAt')}</Text>
              <Text style={[styles.columnHeaderText, styles.colAmount]}>{t('receipts:total')}</Text>
              <Text style={[styles.columnHeaderText, styles.colStatus]}>{t('receipts:status')}</Text>
              <View style={styles.colAction} />
            </View>
          )
        }
        ListEmptyComponent={
          <View style={styles.empty}>
            <Text style={styles.emptyText}>{emptyMessage}</Text>
          </View>
        }
      />

      <Modal visible={detailVisible} transparent animationType="slide" onRequestClose={closeDetail}>
        <View style={styles.modalOverlay}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>{t('receipts:detailTitle')}</Text>
            {detailLoading ? (
              <WaveLoader size={28} color={SoftColors.accent} />
            ) : (
              <>
                <Text style={styles.modalLabel}>{t('receipts:receiptNumber')}</Text>
                <Text style={styles.modalValue}>
                  {detail?.receiptNumber || selected?.receiptNumber || '—'}
                </Text>
                <Text style={styles.modalLabel}>{t('receipts:issuedAt')}</Text>
                <Text style={styles.modalValue}>
                  {formatUserDateTime(detail?.date || selected?.issuedAt) || '—'}
                </Text>
                <Text style={styles.modalLabel}>{t('receipts:total')}</Text>
                <Text style={styles.modalValue}>
                  {formatPrice(detail?.grandTotal ?? selected?.grandTotal ?? 0, formatLocale)}
                </Text>
                <Text style={styles.modalLabel}>{t('receipts:status')}</Text>
                <Text style={styles.modalValue}>
                  {receiptStatusLabel(selected?.status ?? 'Paid', (key) => t(key))}
                </Text>
                {detail?.items?.length ? (
                  <View style={styles.itemsBlock}>
                    {detail.items.slice(0, 12).map((line, index) => (
                      <Text key={`${line.name}-${index}`} style={styles.itemLine}>
                        {line.quantity}× {line.name}
                      </Text>
                    ))}
                  </View>
                ) : null}
              </>
            )}
            <View style={styles.modalActions}>
              <Pressable style={styles.modalSecondary} onPress={closeDetail}>
                <Text style={styles.modalSecondaryText}>{t('receipts:close')}</Text>
              </Pressable>
              {canReprint && selected ? (
                <Pressable
                  style={styles.reprintButton}
                  onPress={() => {
                    void handleReprint(selected);
                  }}
                  accessibilityRole="button"
                  accessibilityLabel={t('receipts:reprintA11y', {
                    number: selected.receiptNumber,
                  })}>
                  <Text style={styles.reprintButtonText}>{t('receipts:reprint')}</Text>
                </Pressable>
              ) : null}
              {selected && rowAllowsStorno(selected) ? (
                <Pressable
                  style={styles.stornoButton}
                  onPress={() => openStorno(selected)}
                  accessibilityRole="button"
                  accessibilityLabel={t('receipts:stornoA11y', { number: selected.receiptNumber })}>
                  <Text style={styles.stornoButtonText}>{t('receipts:storno')}</Text>
                </Pressable>
              ) : null}
            </View>
          </View>
        </View>
      </Modal>

      <StornoModal
        key={stornoTarget?.receiptId ?? 'storno-idle'}
        visible={stornoTarget != null}
        receiptNumber={stornoTarget?.receiptNumber ?? ''}
        submitting={stornoSubmitting}
        onCancel={() => {
          if (!stornoSubmitting) setStornoTarget(null);
        }}
        onConfirm={(reason) => {
          void submitStorno(reason);
        }}
      />

      {toast ? (
        <View
          style={[styles.toast, toast.type === 'error' ? styles.toastError : styles.toastSuccess]}
          accessibilityLiveRegion="polite">
          <Text style={styles.toastText}>{toast.message}</Text>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
  },
  listContent: {
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.lg,
  },
  emptyList: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: SoftSpacing.lg,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  empty: {
    alignItems: 'center',
  },
  emptyText: {
    ...SoftTypography.body,
    color: SoftColors.textMuted,
    textAlign: 'center',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  rowMain: {
    flex: 1,
    minWidth: 0,
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  receiptNumber: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  meta: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  amount: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    textAlign: 'right',
  },
  status: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontWeight: '600',
  },
  columnHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingBottom: SoftSpacing.xs,
    marginBottom: SoftSpacing.xs,
  },
  columnHeaderText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontWeight: '700',
    textTransform: 'uppercase',
    fontSize: 11,
  },
  colNumber: {
    flex: 1.1,
    minWidth: 72,
  },
  colDate: {
    flex: 1.4,
    minWidth: 96,
  },
  colAmount: {
    width: 80,
    textAlign: 'right',
  },
  colStatus: {
    width: 84,
  },
  colAction: {
    minWidth: 88,
    alignItems: 'flex-end',
    gap: 6,
  },
  reprintButton: {
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 6,
    borderRadius: SoftRadius.full,
    minHeight: 32,
    minWidth: 88,
    alignItems: 'center',
    justifyContent: 'center',
  },
  reprintButtonDisabled: {
    opacity: 0.7,
  },
  reprintButtonText: {
    ...SoftTypography.label,
    fontSize: 12,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
  stornoButton: {
    backgroundColor: SoftColors.error,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 6,
    borderRadius: SoftRadius.full,
    minHeight: 32,
    minWidth: 88,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stornoButtonText: {
    ...SoftTypography.label,
    fontSize: 12,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'flex-end',
  },
  modalCard: {
    backgroundColor: SoftColors.bgCard,
    borderTopLeftRadius: SoftRadius.lg,
    borderTopRightRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    gap: 4,
  },
  modalTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  modalLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.sm,
  },
  modalValue: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    fontWeight: '600',
  },
  itemsBlock: {
    marginTop: SoftSpacing.sm,
    gap: 4,
  },
  itemLine: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
  },
  modalActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.md,
  },
  modalSecondary: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: 8,
    minHeight: 32,
    justifyContent: 'center',
  },
  modalSecondaryText: {
    ...SoftTypography.label,
    color: SoftColors.textMuted,
  },
  toast: {
    position: 'absolute',
    left: SoftSpacing.md,
    right: SoftSpacing.md,
    bottom: SoftSpacing.lg,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  toastSuccess: {
    backgroundColor: SoftColors.success,
  },
  toastError: {
    backgroundColor: SoftColors.error,
  },
  toastText: {
    ...SoftTypography.label,
    color: SoftColors.textInverse,
    fontWeight: '700',
    textAlign: 'center',
  },
});
