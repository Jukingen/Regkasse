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

import {
  POS_RECEIPT_REPRINT_REASONS,
  fetchReceiptById,
  reprintReceipt,
  type PosReceiptListItem,
} from '../services/api/receiptService';
import { receiptPrinter } from '../services/receiptPrinter';
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

export type ReceiptListProps = {
  receipts: PosReceiptListItem[];
  cashRegisterId: string;
  loading: boolean;
  refreshing: boolean;
  onRefresh: () => void;
  canReprint: boolean;
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
  formatLocale,
  emptyMessage,
}: ReceiptListProps) {
  const { t } = useTranslation(['receipts', 'common']);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selected, setSelected] = useState<PosReceiptListItem | null>(null);
  const [detail, setDetail] = useState<ReceiptDTO | null>(null);
  const [reprintingId, setReprintingId] = useState<string | null>(null);

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

  const renderItem = useCallback(
    ({ item }: { item: PosReceiptListItem }) => {
      const busy = reprintingId === item.receiptId;
      return (
        <View style={styles.row}>
          <Pressable
            style={styles.rowMain}
            onPress={() => {
              void openDetail(item);
            }}
            accessibilityRole="button"
            accessibilityLabel={t('receipts:openA11y', { number: item.receiptNumber })}>
            <Text style={styles.receiptNumber}>{item.receiptNumber}</Text>
            <Text style={styles.meta}>{formatUserDateTime(item.issuedAt) || '—'}</Text>
          </Pressable>
          <View style={styles.rowRight}>
            <Text style={styles.amount}>{formatPrice(item.grandTotal, formatLocale)}</Text>
            {canReprint ? (
              <Pressable
                style={[styles.reprintButton, busy && styles.reprintButtonDisabled]}
                onPress={() => {
                  void handleReprint(item);
                }}
                disabled={busy}
                accessibilityRole="button"
                accessibilityLabel={t('receipts:reprintA11y', { number: item.receiptNumber })}>
                {busy ? (
                  <ActivityIndicator size="small" color={SoftColors.textInverse} />
                ) : (
                  <Text style={styles.reprintButtonText}>{t('receipts:reprint')}</Text>
                )}
              </Pressable>
            ) : null}
          </View>
        </View>
      );
    },
    [canReprint, formatLocale, handleReprint, openDetail, reprintingId, t]
  );

  if (loading && receipts.length === 0) {
    return (
      <View style={styles.centered}>
        <WaveLoader size={32} color={SoftColors.accent} />
      </View>
    );
  }

  return (
    <>
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
            </View>
          </View>
        </View>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
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
  },
  receiptNumber: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  meta: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  rowRight: {
    alignItems: 'flex-end',
    gap: 6,
  },
  amount: {
    ...SoftTypography.label,
    fontWeight: '700',
    color: SoftColors.textPrimary,
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
});
