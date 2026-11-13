/**
 * RKSV: Separate explicit flows for full receipt cancellation (Storno) vs partial refund (Teilrückerstattung).
 */

import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  SoftColors,
  SoftRadius,
  SoftShadows,
  SoftSpacing,
  SoftState,
  SoftTypography,
} from '../constants/SoftTheme';
import { getPaymentErrorDisplayMessage, isPaymentError } from '../features/payment/paymentErrors';
import { paymentService, type PaymentRequest } from '../services/api/paymentService';
import {
  fetchPaymentRowForPos,
  fetchReceiptDtoByPayment,
  searchReceiptsByReceiptNumber,
  type ParsedPaymentRow,
} from '../services/api/receiptListService';
import type { ReceiptDTO, ReceiptItemDTO } from '../types/ReceiptDTO';
import { formatPrice } from '../utils/formatPrice';

export type StornoRefundSelectionProps = {
  visible: boolean;
  onClose: () => void;
  cashRegisterId: string;
  tableNumber: number;
  /** Called after successful fiscal POST (payment id from server). */
  onSuccess?: (paymentId: string) => void;
};

type FlowStep = 'menu' | 'storno' | 'refund';

type ApiStornoReason = 'FalscherBetrag' | 'KundeStorniert' | 'TechnischerFehler' | 'Anderes';

const STORNO_REASON_VALUES: ApiStornoReason[] = [
  'FalscherBetrag',
  'KundeStorniert',
  'TechnischerFehler',
  'Anderes',
];

function parseLocaleDecimal(input: string): number {
  const s = input.trim().replace(',', '.');
  if (!s) return NaN;
  return parseFloat(s);
}

function lineGross(i: ReceiptItemDTO): number {
  return i.lineTotalGross ?? i.totalPrice ?? 0;
}

export default function StornoRefundSelection({
  visible,
  onClose,
  cashRegisterId,
  tableNumber,
  onSuccess,
}: StornoRefundSelectionProps) {
  const { t } = useTranslation(['checkout', 'common']);
  const insets = useSafeAreaInsets();

  const [step, setStep] = useState<FlowStep>('menu');
  const [receiptInput, setReceiptInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const [resolvedPayment, setResolvedPayment] = useState<ParsedPaymentRow | null>(null);

  const [stornoReason, setStornoReason] = useState<ApiStornoReason>('KundeStorniert');
  const [stornoConfirm, setStornoConfirm] = useState(false);

  const [refundReceipt, setRefundReceipt] = useState<ReceiptDTO | null>(null);
  const [selectedLineIdx, setSelectedLineIdx] = useState<Set<number>>(() => new Set());
  const [refundAmountStr, setRefundAmountStr] = useState('');

  const resetAll = useCallback(() => {
    setStep('menu');
    setReceiptInput('');
    setLoading(false);
    setLocalError(null);
    setResolvedPayment(null);
    setStornoReason('KundeStorniert');
    setStornoConfirm(false);
    setRefundReceipt(null);
    setSelectedLineIdx(new Set());
    setRefundAmountStr('');
  }, []);

  const handleClose = useCallback(() => {
    resetAll();
    onClose();
  }, [onClose, resetAll]);

  const resolveReceiptToPayment = useCallback(async (): Promise<ParsedPaymentRow | null> => {
    setLocalError(null);
    const trimmed = receiptInput.trim();
    if (!trimmed) {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.receiptRequired'));
      return null;
    }
    if (!cashRegisterId || cashRegisterId === '00000000-0000-0000-0000-000000000000') {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.noCashRegister'));
      return null;
    }

    setLoading(true);
    try {
      const rows = await searchReceiptsByReceiptNumber({
        receiptNumber: trimmed,
        cashRegisterId,
        pageSize: 20,
      });
      const exact = rows.filter(
        (r) => r.receiptNumber.trim().toLowerCase() === trimmed.toLowerCase()
      );
      if (exact.length === 0) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.receiptNotFound'));
        return null;
      }
      const saleRow =
        exact.find((r) => !r.rksvSpecialReceiptKind) ??
        exact.find((r) => r.grandTotal > 0) ??
        exact[0];
      if (saleRow.rksvSpecialReceiptKind) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.specialReceipt'));
        return null;
      }

      const pay = await fetchPaymentRowForPos(saleRow.paymentId);
      if (!pay) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.paymentLoadFailed'));
        return null;
      }
      if (pay.isStorno || pay.isRefund) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.notOriginalSale'));
        return null;
      }
      if (pay.cashRegisterId.toLowerCase() !== cashRegisterId.toLowerCase()) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.registerMismatch'));
        return null;
      }

      setResolvedPayment(pay);
      return pay;
    } catch {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.lookupFailed'));
      return null;
    } finally {
      setLoading(false);
    }
  }, [cashRegisterId, receiptInput, t]);

  const loadRefundReceiptLines = useCallback(async () => {
    const pay = await resolveReceiptToPayment();
    if (!pay) return;
    setLoading(true);
    try {
      const dto = await fetchReceiptDtoByPayment(pay.id);
      if (!dto?.items?.length) {
        setLocalError(t('checkout:posFlow.stornoRefund.errors.receiptLinesMissing'));
        setRefundReceipt(null);
        return;
      }
      setRefundReceipt(dto);
      const allIdx = new Set<number>();
      dto.items.forEach((_, i) => allIdx.add(i));
      setSelectedLineIdx(allIdx);
      const mx = dto.grandTotal ?? 0;
      setRefundAmountStr(mx > 0 ? mx.toFixed(2) : '');
    } finally {
      setLoading(false);
    }
  }, [resolveReceiptToPayment, t]);

  const toggleLine = useCallback((idx: number) => {
    setSelectedLineIdx((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) next.delete(idx);
      else next.add(idx);
      return next;
    });
  }, []);

  const selectedSum = useMemo(() => {
    if (!refundReceipt?.items?.length) return 0;
    let s = 0;
    refundReceipt.items.forEach((it, idx) => {
      if (selectedLineIdx.has(idx)) s += lineGross(it);
    });
    return Math.round(s * 100) / 100;
  }, [refundReceipt, selectedLineIdx]);

  const maxRefund = useMemo(() => {
    const g = refundReceipt?.grandTotal ?? resolvedPayment?.totalAmount ?? 0;
    return Math.max(0, g);
  }, [refundReceipt, resolvedPayment]);

  const submitStorno = useCallback(async () => {
    const pay = resolvedPayment ?? (await resolveReceiptToPayment());
    if (!pay) return;
    if (!stornoConfirm) {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.confirmRequired'));
      return;
    }

    const req: PaymentRequest = {
      customerId: pay.customerId,
      items: [],
      payment: { method: 'cash', tseRequired: true },
      tableNumber,
      totalAmount: pay.totalAmount,
      cashRegisterId,
      isStorno: true,
      isRefund: false,
      originalReceiptNumber: pay.receiptNumber,
      stornoReason,
      notes: t('checkout:posFlow.stornoRefund.storno.auditNote'),
    };

    setLoading(true);
    setLocalError(null);
    try {
      const res = await paymentService.processPayment(req);
      if (!res.success || !res.paymentId) {
        const msg =
          res.message || res.error || t('checkout:posFlow.stornoRefund.errors.submitFailed');
        setLocalError(msg);
        return;
      }
      Alert.alert(
        t('checkout:posFlow.stornoRefund.alerts.successTitle'),
        t('checkout:posFlow.stornoRefund.alerts.stornoSuccessBody'),
        [
          {
            text: t('common:ok'),
            onPress: () => {
              onSuccess?.(res.paymentId);
              handleClose();
            },
          },
        ]
      );
    } catch (e: unknown) {
      const msg = isPaymentError(e)
        ? getPaymentErrorDisplayMessage(e)
        : e instanceof Error
          ? e.message
          : t('checkout:posFlow.stornoRefund.errors.submitFailed');
      setLocalError(msg);
    } finally {
      setLoading(false);
    }
  }, [
    cashRegisterId,
    handleClose,
    onSuccess,
    resolveReceiptToPayment,
    resolvedPayment,
    stornoConfirm,
    stornoReason,
    t,
    tableNumber,
  ]);

  const submitRefund = useCallback(async () => {
    let pay = resolvedPayment;
    if (!pay) {
      pay = (await resolveReceiptToPayment()) ?? null;
    }
    if (!pay) return;

    const amt = parseLocaleDecimal(refundAmountStr);
    if (!Number.isFinite(amt) || amt < 0.01) {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.invalidRefundAmount'));
      return;
    }
    if (amt > maxRefund + 0.01) {
      setLocalError(
        t('checkout:posFlow.stornoRefund.errors.refundExceedsMax', { max: formatPrice(maxRefund) })
      );
      return;
    }
    if (selectedLineIdx.size === 0) {
      setLocalError(t('checkout:posFlow.stornoRefund.errors.selectOneLine'));
      return;
    }

    const req: PaymentRequest = {
      customerId: pay.customerId,
      items: [],
      payment: { method: 'cash', tseRequired: true },
      tableNumber,
      totalAmount: Math.round(amt * 100) / 100,
      cashRegisterId,
      isStorno: false,
      isRefund: true,
      originalReceiptNumber: pay.receiptNumber,
      notes: t('checkout:posFlow.stornoRefund.refund.auditNote'),
    };

    setLoading(true);
    setLocalError(null);
    try {
      const res = await paymentService.processPayment(req);
      if (!res.success || !res.paymentId) {
        const msg =
          res.message || res.error || t('checkout:posFlow.stornoRefund.errors.submitFailed');
        setLocalError(msg);
        return;
      }
      const refundMessage =
        typeof res.message === 'string' && res.message.trim().length > 0
          ? res.message.trim()
          : t('checkout:posFlow.stornoRefund.alerts.refundSuccessBody');
      const refundReceiptNumber =
        res.tse?.receiptNumber ||
        ((res as unknown as { receiptNumber?: string }).receiptNumber ?? '');
      const successBody =
        typeof refundReceiptNumber === 'string' && refundReceiptNumber.trim().length > 0
          ? `${refundMessage}\n${t('checkout:posFlow.stornoRefund.fields.receiptNumber')}: ${refundReceiptNumber.trim()}`
          : refundMessage;
      Alert.alert('Erstattung erfolgreich', successBody, [
        {
          text: 'Zurück zur Kasse',
          onPress: () => {
            // Parent onSuccess callback can route back to payment/cart flow.
            onSuccess?.(res.paymentId);
            handleClose();
          },
        },
      ]);
    } catch (e: unknown) {
      const msg = isPaymentError(e)
        ? getPaymentErrorDisplayMessage(e)
        : e instanceof Error
          ? e.message
          : t('checkout:posFlow.stornoRefund.errors.submitFailed');
      setLocalError(msg);
    } finally {
      setLoading(false);
    }
  }, [
    cashRegisterId,
    handleClose,
    maxRefund,
    onSuccess,
    refundAmountStr,
    resolveReceiptToPayment,
    resolvedPayment,
    selectedLineIdx.size,
    t,
    tableNumber,
  ]);

  const menu = (
    <View style={styles.menuColumn}>
      <Text style={styles.menuIntro}>{t('checkout:posFlow.stornoRefund.menu.intro')}</Text>

      <Pressable
        onPress={() => {
          setStep('storno');
        }}
        style={({ pressed }) => [styles.cardRed, pressed && SoftState.pressed]}
        accessibilityRole="button">
        <View style={styles.cardHeaderRow}>
          <Ionicons name="warning" size={28} color="#b71c1c" />
          <Text style={styles.cardTitleRed}>
            {t('checkout:posFlow.stornoRefund.menu.optionFullStorno')}
          </Text>
        </View>
        <Text style={styles.cardHint}>
          {t('checkout:posFlow.stornoRefund.menu.hintFullStorno')}
        </Text>
      </Pressable>

      <Pressable
        onPress={() => {
          setStep('refund');
        }}
        style={({ pressed }) => [styles.cardYellow, pressed && SoftState.pressed]}
        accessibilityRole="button">
        <View style={styles.cardHeaderRow}>
          <Ionicons name="return-down-back" size={26} color="#f57f17" />
          <Text style={styles.cardTitleYellow}>
            {t('checkout:posFlow.stornoRefund.menu.optionRefund')}
          </Text>
        </View>
        <Text style={styles.cardHint}>{t('checkout:posFlow.stornoRefund.menu.hintRefund')}</Text>
      </Pressable>

      <Pressable
        onPress={handleClose}
        style={({ pressed }) => [styles.cardNeutral, pressed && SoftState.pressed]}
        accessibilityRole="button">
        <Text style={styles.cardTitleNeutral}>
          {t('checkout:posFlow.stornoRefund.menu.optionBack')}
        </Text>
      </Pressable>
    </View>
  );

  const stornoPanel = (
    <View>
      <Text style={styles.sectionTitle}>{t('checkout:posFlow.stornoRefund.storno.title')}</Text>
      <Text style={styles.label}>{t('checkout:posFlow.stornoRefund.fields.receiptNumber')}</Text>
      <TextInput
        style={styles.input}
        value={receiptInput}
        onChangeText={setReceiptInput}
        placeholder={t('checkout:posFlow.stornoRefund.placeholders.receiptNumber')}
        autoCapitalize="characters"
        editable={!loading}
      />
      <Pressable
        onPress={async () => {
          const p = await resolveReceiptToPayment();
          if (p)
            Alert.alert(
              t('checkout:posFlow.stornoRefund.alerts.loadedTitle'),
              t('checkout:posFlow.stornoRefund.alerts.loadedBody', {
                amount: formatPrice(p.totalAmount),
              })
            );
        }}
        style={({ pressed }) => [styles.secondaryBtn, pressed && SoftState.pressed]}
        disabled={loading}>
        <Text style={styles.secondaryBtnText}>
          {t('checkout:posFlow.stornoRefund.actions.loadReceipt')}
        </Text>
      </Pressable>

      {resolvedPayment ? (
        <Text style={styles.meta}>
          {t('checkout:posFlow.stornoRefund.storno.originalTotal', {
            amount: formatPrice(resolvedPayment.totalAmount),
          })}
        </Text>
      ) : null}

      <Text style={[styles.label, { marginTop: SoftSpacing.md }]}>
        {t('checkout:posFlow.stornoRefund.fields.stornoReason')}
      </Text>
      {STORNO_REASON_VALUES.map((v) => (
        <Pressable
          key={v}
          onPress={() => {
            setStornoReason(v);
          }}
          style={styles.reasonRow}
          accessibilityRole="radio"
          accessibilityState={{ selected: stornoReason === v }}>
          <Ionicons
            name={stornoReason === v ? 'radio-button-on' : 'radio-button-off'}
            size={22}
            color={SoftColors.accent}
          />
          <Text style={styles.reasonLabel}>{t(`checkout:posFlow.stornoRefund.reasons.${v}`)}</Text>
        </Pressable>
      ))}

      <View style={styles.warnBox}>
        <Ionicons name="alert-circle" size={22} color="#b71c1c" />
        <Text style={styles.warnText}>
          {t('checkout:posFlow.stornoRefund.storno.irreversibleWarning')}
        </Text>
      </View>

      <Pressable
        onPress={() => {
          setStornoConfirm(!stornoConfirm);
        }}
        style={styles.checkboxRow}
        accessibilityRole="checkbox"
        accessibilityState={{ checked: stornoConfirm }}>
        <Ionicons
          name={stornoConfirm ? 'checkbox' : 'square-outline'}
          size={24}
          color={SoftColors.accent}
        />
        <Text style={styles.checkboxLabel}>
          {t('checkout:posFlow.stornoRefund.storno.confirmCheckbox')}
        </Text>
      </Pressable>

      <View style={styles.rowButtons}>
        <Pressable
          onPress={() => {
            resetAll();
          }}
          style={styles.secondaryGhost}>
          <Text style={styles.secondaryGhostText}>
            {t('checkout:posFlow.stornoRefund.actions.backToMenu')}
          </Text>
        </Pressable>
        <Pressable
          onPress={submitStorno}
          style={({ pressed }) => [
            styles.dangerBtn,
            pressed && SoftState.pressed,
            loading && styles.btnDisabled,
          ]}
          disabled={loading}>
          <Text style={styles.dangerBtnText}>
            {t('checkout:posFlow.stornoRefund.actions.executeStorno')}
          </Text>
        </Pressable>
      </View>
    </View>
  );

  const refundPanel = (
    <View>
      <Text style={styles.sectionTitle}>{t('checkout:posFlow.stornoRefund.refund.title')}</Text>
      <Text style={styles.note}>{t('checkout:posFlow.stornoRefund.refund.note')}</Text>

      <Text style={styles.label}>{t('checkout:posFlow.stornoRefund.fields.receiptNumber')}</Text>
      <TextInput
        style={styles.input}
        value={receiptInput}
        onChangeText={setReceiptInput}
        placeholder={t('checkout:posFlow.stornoRefund.placeholders.receiptNumber')}
        autoCapitalize="characters"
        editable={!loading}
      />
      <Pressable
        onPress={loadRefundReceiptLines}
        style={({ pressed }) => [styles.secondaryBtn, pressed && SoftState.pressed]}
        disabled={loading}>
        <Text style={styles.secondaryBtnText}>
          {t('checkout:posFlow.stornoRefund.actions.loadReceiptLines')}
        </Text>
      </Pressable>

      {refundReceipt?.items?.length ? (
        <>
          <Text style={[styles.label, { marginTop: SoftSpacing.md }]}>
            {t('checkout:posFlow.stornoRefund.refund.selectLines')}
          </Text>
          <Text style={styles.metaSmall}>
            {t('checkout:posFlow.stornoRefund.refund.selectedSum', {
              amount: formatPrice(selectedSum),
            })}
          </Text>
          {refundReceipt.items.map((it, idx) => (
            <Pressable
              key={`${it.name}-${idx}`}
              onPress={() => {
                toggleLine(idx);
              }}
              style={styles.lineRow}>
              <Ionicons
                name={selectedLineIdx.has(idx) ? 'checkbox' : 'square-outline'}
                size={22}
                color={SoftColors.accent}
              />
              <View style={{ flex: 1 }}>
                <Text style={styles.lineName}>{it.name}</Text>
                <Text style={styles.lineMeta}>
                  × {it.quantity} · {formatPrice(lineGross(it))}
                </Text>
              </View>
            </Pressable>
          ))}

          <Text style={[styles.label, { marginTop: SoftSpacing.md }]}>
            {t('checkout:posFlow.stornoRefund.fields.refundAmount')}
          </Text>
          <TextInput
            style={styles.input}
            value={refundAmountStr}
            onChangeText={setRefundAmountStr}
            keyboardType="decimal-pad"
            placeholder={t('checkout:posFlow.stornoRefund.placeholders.amount')}
          />
          <Text style={styles.metaSmall}>
            {t('checkout:posFlow.stornoRefund.refund.maxHint', { max: formatPrice(maxRefund) })}
          </Text>
        </>
      ) : null}

      <View style={styles.rowButtons}>
        <Pressable
          onPress={() => {
            resetAll();
          }}
          style={styles.secondaryGhost}>
          <Text style={styles.secondaryGhostText}>
            {t('checkout:posFlow.stornoRefund.actions.backToMenu')}
          </Text>
        </Pressable>
        <Pressable
          onPress={submitRefund}
          style={({ pressed }) => [
            styles.accentBtn,
            pressed && SoftState.pressed,
            loading && styles.btnDisabled,
          ]}
          disabled={loading}>
          <Text style={styles.payButtonText}>
            {t('checkout:posFlow.stornoRefund.actions.executeRefund')}
          </Text>
        </Pressable>
      </View>
    </View>
  );

  return (
    <Modal visible={visible} animationType="slide" transparent={false} onRequestClose={handleClose}>
      <KeyboardAvoidingView
        style={[styles.container, { paddingTop: insets.top, paddingBottom: insets.bottom }]}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <View style={styles.header}>
          <Pressable
            onPress={() => {
              step === 'menu' ? handleClose() : resetAll();
            }}
            hitSlop={12}>
            <Ionicons name="close" size={28} color={SoftColors.textPrimary} />
          </Pressable>
          <Text style={styles.headerTitle}>{t('checkout:posFlow.stornoRefund.headerTitle')}</Text>
          <View style={{ width: 28 }} />
        </View>

        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled">
          {step === 'menu' && menu}
          {step === 'storno' && stornoPanel}
          {step === 'refund' && refundPanel}

          {localError ? (
            <View style={styles.errorBox}>
              <Text style={styles.errorText}>{localError}</Text>
            </View>
          ) : null}
          {loading ? (
            <Text style={styles.loadingText}>{t('checkout:posFlow.stornoRefund.loading')}</Text>
          ) : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgCard,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  headerTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  scrollContent: {
    padding: SoftSpacing.md,
    paddingBottom: SoftSpacing.xl,
  },
  menuIntro: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
  },
  menuColumn: {
    gap: SoftSpacing.md,
  },
  cardRed: {
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.md,
    backgroundColor: '#ffebee',
    borderWidth: 2,
    borderColor: '#e57373',
    ...SoftShadows.sm,
  },
  cardYellow: {
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.md,
    backgroundColor: '#fff8e1',
    borderWidth: 2,
    borderColor: '#ffca28',
    ...SoftShadows.sm,
  },
  cardNeutral: {
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    backgroundColor: SoftColors.bgSecondary,
    borderWidth: 1,
    borderColor: SoftColors.border,
    alignItems: 'center',
  },
  cardHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.xs,
  },
  cardTitleRed: {
    ...SoftTypography.h3,
    color: '#b71c1c',
    flex: 1,
  },
  cardTitleYellow: {
    ...SoftTypography.h3,
    color: '#f57f17',
    flex: 1,
  },
  cardTitleNeutral: {
    ...SoftTypography.body,
    fontWeight: '600',
    color: SoftColors.textPrimary,
  },
  cardHint: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
  },
  sectionTitle: {
    ...SoftTypography.h2,
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
  },
  label: {
    ...SoftTypography.bodySmall,
    fontWeight: '600',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  input: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    fontSize: 16,
    backgroundColor: SoftColors.bgSecondary,
    marginBottom: SoftSpacing.sm,
  },
  secondaryBtn: {
    alignSelf: 'flex-start',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgSecondary,
    borderWidth: 1,
    borderColor: SoftColors.border,
    marginBottom: SoftSpacing.md,
  },
  secondaryBtnText: {
    ...SoftTypography.body,
    color: SoftColors.accent,
    fontWeight: '600',
  },
  meta: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  metaSmall: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.sm,
  },
  reasonRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
  },
  reasonLabel: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  warnBox: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    backgroundColor: '#fff3e0',
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    marginTop: SoftSpacing.md,
    borderWidth: 1,
    borderColor: '#ffb74d',
  },
  warnText: {
    ...SoftTypography.bodySmall,
    color: '#bf360c',
    flex: 1,
  },
  checkboxRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.md,
  },
  checkboxLabel: {
    ...SoftTypography.bodySmall,
    flex: 1,
    color: SoftColors.textPrimary,
  },
  rowButtons: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    marginTop: SoftSpacing.lg,
    flexWrap: 'wrap',
  },
  secondaryGhost: {
    padding: SoftSpacing.sm,
  },
  secondaryGhostText: {
    ...SoftTypography.body,
    color: SoftColors.accent,
  },
  dangerBtn: {
    flex: 1,
    minHeight: 48,
    backgroundColor: '#c62828',
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: SoftSpacing.sm,
  },
  dangerBtnText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: '#fff',
  },
  accentBtn: {
    flex: 1,
    minHeight: 48,
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: SoftSpacing.sm,
    ...SoftShadows.sm,
  },
  payButtonText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
  btnDisabled: {
    opacity: 0.55,
  },
  note: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
  },
  lineRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  lineName: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  lineMeta: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
  },
  errorBox: {
    marginTop: SoftSpacing.md,
    padding: SoftSpacing.sm,
    backgroundColor: '#ffebee',
    borderRadius: SoftRadius.md,
  },
  errorText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.error,
  },
  loadingText: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.sm,
    textAlign: 'center',
  },
});
