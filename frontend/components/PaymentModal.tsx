import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ScrollView,
  Alert,
  TextInput,
  Switch,
  Pressable,
  Platform,
} from 'react-native';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftState, SoftTypography } from '../constants/SoftTheme';
import { formatPrice } from '../utils/formatPrice';
import { useAuth } from '../contexts/AuthContext';
import { useSystem } from '../contexts/SystemContext';
import { usePayment } from '../hooks/usePayment';
import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import {
  POS_LARGE_CASH_WARN_THRESHOLD_EUR,
  POS_TSE_STATUS_FAILURE_WARN_STREAK,
  registerPosTseStatusCheckOutcome,
} from '../constants/posOperatorWarnings';
import {
  paymentService,
  PaymentRequest,
  PaymentItem,
  type VoucherValidateSuccess,
} from '../services/api/paymentService';
import { isPaymentError, getPaymentErrorMessage } from '../features/payment/paymentErrors';
import { cartService } from '../services/api/cartService';
import { customerService, isWalkInCustomerId, type BenefitEligibilityPreviewResponse } from '../services/api/customerService';
import { WALK_IN_CUSTOMER_ID_FALLBACK } from '../constants/walkInCustomer';
import { validateAmount } from '../utils/validation';
import { usePosCashRegisterAssignment } from '../hooks/usePosCashRegisterAssignment';
import { usePosCheckoutUiStore } from '../stores/posCheckoutUiStore';
import { receiptPrinter } from '../services/receiptPrinter';
import { PaymentSuccessQr } from './PaymentSuccessQr';
import { ReceiptSummary, type ReceiptSummaryReceipt } from './ReceiptSummary';
import type { PaymentTseInfo } from '../services/api/paymentService';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import { downloadInvoicePdf, InvoicePdfHttpError } from '../services/api/invoiceService';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import {
  buildPosRegisterGateContext,
  isRegisterGateDecommissioned,
  POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE,
  POS_READINESS_MESSAGE_CODES,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';
import { checkTseStatus } from '../services/api/tseService';
import { useTseHealth } from '../hooks/useTseHealth';
import { WaveLoader } from '../src/components/common/WaveLoader';
import StornoRefundSelection from './StornoRefundSelection';
import { canShowPosStornoRefundButton } from '../utils/posStornoRefundGate';
import { useTimeSyncStatus } from '../hooks/useTimeSyncStatus';
import { POS_TIME_SYNC_ADMIN_CONTACT_MESSAGE_DE } from '../constants/posTimeSyncContact';

/**
 * Map backend blocked reason to short UI text. Fail-safe: unknown codes return neutral fallback;
 * backend message is not used for display (may be English).
 */
function formatBlockedReason(
  t: TFunction,
  b: {
    blockedReasonCode?: string | null;
    message?: string | null;
    requiredMoreQuantity?: number | null;
  }
): string {
  const code = typeof b?.blockedReasonCode === 'string' ? b.blockedReasonCode.trim() : '';
  if (!code) return t('checkout:posFlow.benefit.blockedReasons.fallback');

  if (code === 'QuantityNotReached' && typeof b?.requiredMoreQuantity === 'number' && b.requiredMoreQuantity > 0) {
    return t('checkout:posFlow.benefit.blockedReasons.quantityNotReachedDetail', {
      count: b.requiredMoreQuantity,
    });
  }

  const known: Record<string, string> = {
    DailyLimitReached: 'checkout:posFlow.benefit.blockedReasons.dailyLimit',
    NoEligibleItems: 'checkout:posFlow.benefit.blockedReasons.noEligibleItems',
    QuantityNotReached: 'checkout:posFlow.benefit.blockedReasons.quantityNotReached',
  };
  const key = known[code];
  return key ? t(key) : t('checkout:posFlow.benefit.blockedReasons.fallback');
}

function parseLocaleDecimal(input: string): number {
  const s = input.trim().replace(',', '.');
  if (!s) return NaN;
  return parseFloat(s);
}

function effectiveVoucherRedeemCapFromSnapshot(s: VoucherValidateSuccess): number {
  if (
    typeof s.maxRedeemableAmount === 'number' &&
    Number.isFinite(s.maxRedeemableAmount) &&
    s.maxRedeemableAmount > 0
  ) {
    return Math.min(s.remainingAmount, s.maxRedeemableAmount);
  }
  return s.remainingAmount ?? 0;
}

/** Aligns with PaymentModal `voucherMaxForSale` when the Gutschein toggle is on. */
function computeVoucherMaxForSale(
  totalAmount: number,
  snapshot: VoucherValidateSuccess | null,
  voucherEnabled: boolean
): number {
  if (!voucherEnabled || !snapshot) return 0;
  const cap = effectiveVoucherRedeemCapFromSnapshot(snapshot);
  return Math.max(0, Math.min(totalAmount, cap));
}

/** Cart gross tolerance for voucher + cash coverage (aligned with backend money rules). */
const PAYMENT_COVERAGE_TOLERANCE_EUR = 0.02;

/**
 * Whether applied voucher EUR plus cash tender covers cart gross total (German decimal input).
 * When settlement Restbetrag is ~0, only the voucher portion must cover the cart total.
 */
function computeVoucherPlusCashCoversTotal(input: {
  voucherEnabled: boolean;
  appliedVoucherAmount: number;
  totalCartAmount: number;
  settlementAmountDue: number;
  requiresCashAmount: boolean;
  amountReceivedStr: string;
}): { sumPaid: number; coversTotal: boolean } {
  const v = input.voucherEnabled ? Math.max(0, input.appliedVoucherAmount) : 0;
  const cashParsed = parseLocaleDecimal(input.amountReceivedStr);
  const cashReceived = Number.isFinite(cashParsed) ? Math.max(0, cashParsed) : 0;

  let sumPaid: number;
  if (!input.voucherEnabled) {
    sumPaid = input.requiresCashAmount ? cashReceived : input.totalCartAmount;
  } else if (input.settlementAmountDue <= PAYMENT_COVERAGE_TOLERANCE_EUR) {
    sumPaid = v;
  } else if (input.requiresCashAmount) {
    sumPaid = v + cashReceived;
  } else {
    // Card/transfer: cover Restbetrag without using cash TextInput.
    sumPaid = v + Math.max(0, input.settlementAmountDue);
  }

  return {
    sumPaid,
    coversTotal: sumPaid >= input.totalCartAmount - PAYMENT_COVERAGE_TOLERANCE_EUR,
  };
}

/** ReceiptDTO veya payment response'taki receipt → ReceiptSummary formatı. */
function toSummaryReceipt(receipt: ReceiptDTO | null): ReceiptSummaryReceipt | null {
  if (!receipt?.items?.length) return null;
  const items = receipt.items.map((i) => ({
    name: i.name,
    quantity: i.quantity,
    lineTotalGross: i.lineTotalGross ?? i.totalPrice ?? 0,
    isModifier: i.isModifierLine ?? false,
  }));
  const totals = {
    totalNet: receipt.totals?.totalNet ?? receipt.subtotal ?? 0,
    totalVat: receipt.totals?.totalVat ?? receipt.taxAmount ?? 0,
    totalGross: receipt.totals?.totalGross ?? receipt.grandTotal ?? 0,
  };
  const vatBreakdown = (receipt.taxRates ?? []).map((t) => ({
    rate: t.rate,
    net: t.netAmount,
    vat: t.taxAmount,
    gross: t.grossAmount,
  }));
  return { items, totals, vatBreakdown };
}

// Backend cevabını ReceiptDTO'ya normalize et (PascalCase/camelCase uyumu)
function normalizeReceiptDto(r: any): ReceiptDTO {
  const items = (r.items ?? r.Items ?? []).map((i: any) => ({
    itemId: i.itemId ?? i.ItemId,
    name: i.name ?? i.Name,
    quantity: i.quantity ?? i.Quantity ?? 0,
    unitPrice: i.unitPrice ?? i.UnitPrice ?? 0,
    totalPrice: i.totalPrice ?? i.TotalPrice ?? 0,
    lineTotalNet: i.lineTotalNet ?? i.LineTotalNet,
    lineTotalGross: i.lineTotalGross ?? i.LineTotalGross,
    taxRate: i.taxRate ?? i.TaxRate ?? 0,
    vatRate: i.vatRate ?? i.VatRate,
    vatAmount: i.vatAmount ?? i.VatAmount,
    categoryName: i.categoryName ?? i.CategoryName,
    parentItemId: i.parentItemId ?? i.ParentItemId,
    isModifierLine: i.isModifierLine ?? i.IsModifierLine ?? false,
  }));
  const taxRates = (r.taxRates ?? r.TaxRates ?? []).map((t: any) => ({
    taxType: t.taxType ?? t.TaxType,
    rate: t.rate ?? t.Rate ?? 0,
    vatRate: t.vatRate ?? t.VatRate,
    netAmount: t.netAmount ?? t.NetAmount ?? 0,
    taxAmount: t.taxAmount ?? t.TaxAmount ?? 0,
    grossAmount: t.grossAmount ?? t.GrossAmount ?? 0,
  }));
  return {
    receiptId: r.receiptId ?? r.ReceiptId ?? '',
    receiptNumber: r.receiptNumber ?? r.ReceiptNumber ?? '',
    date: r.date ?? r.Date ?? new Date().toISOString(),
    cashierId: r.cashierId ?? r.CashierId ?? '',
    cashierDisplayName: r.cashierDisplayName ?? r.CashierDisplayName ?? undefined,
    tableNumber: r.tableNumber ?? r.TableNumber,
    company: r.company ?? r.Company ?? { name: '', address: '', taxNumber: '' },
    kassenID: r.kassenID ?? r.KassenID ?? '',
    items,
    taxRates,
    subtotal: r.subtotal ?? r.SubTotal ?? 0,
    taxAmount: r.taxAmount ?? r.TaxAmount ?? 0,
    grandTotal: r.grandTotal ?? r.GrandTotal ?? 0,
    totals: r.totals ?? r.Totals,
    payments: r.payments ?? r.Payments ?? [],
    footerText: r.footerText ?? r.FooterText,
    signature: r.signature ?? r.Signature ?? { algorithm: '', value: '', serialNumber: '', timestamp: '', qrData: '' },
  };
}

// Türkçe Açıklama: Ödeme alma modal'ı - Sepet içeriğini ödeme işlemine dönüştürür
interface PaymentModalProps {
  visible: boolean;
  onClose: () => void;
  /** Called after payment and print; pass tableNumber so caller can clear the paid table. */
  onSuccess: (paymentId: string, tableNumber?: number) => void;
  /** Optional toast sink (tab bar) for server offline-queue messages. */
  onPosToast?: (payload: { type?: 'success' | 'warning' | 'info'; message: string }) => void;
  cartItems: Array<{
    id: string;
    productId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
    taxType?: string | number;
    /** Extra Zutaten – ödeme/fiş için backend'e gönderilir (modifierId zorunlu; name/priceDelta opsiyonel) */
    modifiers?: Array<{ modifierId: string; name?: string; priceDelta?: number }>;
  }>;
  /** Backend'den gelen brüt toplam - FE hesaplama yapmaz */
  grandTotalGross?: number;
  customerId?: string;
  tableNumber?: number;
}

export default function PaymentModal({
  visible,
  onClose,
  onSuccess,
  cartItems,
  grandTotalGross,
  customerId = '00000000-0000-0000-0000-000000000000', // Default Guid formatında
  tableNumber,
  onPosToast,
}: PaymentModalProps) {
  const { t, i18n } = useTranslation(['checkout', 'common', 'invoices', 'settings']);
  const { user } = useAuth();
  const showStornoRefundEntry = canShowPosStornoRefundButton(user);
  const { refetch: refetchTimeSync, timeSyncCritical, timeSyncWarningBand } = useTimeSyncStatus();
  const { isOnline } = useSystem();
  const tseHealth = useTseHealth();
  const tseServerOffline = String(tseHealth.status) === 'Offline';
  const selectedPaymentMethod = usePosCheckoutUiStore((s) => s.selectedPaymentMethodType);
  const setSelectedPaymentMethodType = usePosCheckoutUiStore((s) => s.setSelectedPaymentMethodType);
  const paymentMethodSubmitAttempted = usePosCheckoutUiStore((s) => s.paymentMethodSubmitAttempted);
  const setPaymentMethodSubmitAttempted = usePosCheckoutUiStore((s) => s.setPaymentMethodSubmitAttempted);
  const resetCheckoutPaymentUi = usePosCheckoutUiStore((s) => s.resetCheckoutPaymentUi);
  const [amountReceived, setAmountReceived] = useState<string>('');
  /** Cash (Bar): true when operator entered or preset-selected amount &gt; 0 (drives inline ⚠️ hint). */
  const [isAmountValid, setIsAmountValid] = useState(true);
  const [notes, setNotes] = useState<string>('');
  const [guestCustomerId, setGuestCustomerId] = useState<string>(WALK_IN_CUSTOMER_ID_FALLBACK);
  // State for Purchase Flow
  type PurchaseState = 'input' | 'processing' | 'printing' | 'completed' | 'print_error';
  const [purchaseState, setPurchaseState] = useState<PurchaseState>('input');

  // Store paymentId and TSE/QR bilgisi for success ekranı ve retry
  const [completedPaymentId, setCompletedPaymentId] = useState<string | null>(null);
  const [completedPaymentTse, setCompletedPaymentTse] = useState<PaymentTseInfo | null>(null);
  /** Receipt payload for summary — GET /api/pos/payment/{id}/receipt */
  const [receiptData, setReceiptData] = useState<ReceiptDTO | null>(null);
  const [pdfLoading, setPdfLoading] = useState(false);
  /** Prevents double-submit during async work before purchaseState becomes 'processing'. */
  const [paymentBusy, setPaymentBusy] = useState(false);

  /** RKSV: separate wizard for Storno vs Teilrückerstattung (elevated permissions). */
  const [stornoRefundWizardVisible, setStornoRefundWizardVisible] = useState(false);

  /** Gutschein: code, validate snapshot, redeem amount (must match fiscal total for single-code flow). */
  const [voucherCode, setVoucherCode] = useState('');
  const [voucherEnabled, setVoucherEnabled] = useState(false);
  /** Gutschein: true when code has non-whitespace (inline ⚠️ when false while method is voucher). */
  const [isVoucherCodeValid, setIsVoucherCodeValid] = useState(true);
  const [voucherRedeemAmountStr, setVoucherRedeemAmountStr] = useState('');
  /** Writes `voucherRedeemAmountStr`; effective EUR is derived via `useMemo` below (empty → 0). */
  const setVoucherRedeemAmountEffective = useCallback((eur: number) => {
    const n = Number.isFinite(eur) ? Math.max(0, eur) : 0;
    setVoucherRedeemAmountStr(n > 0 ? n.toFixed(2) : '');
  }, []);
  const [voucherSnapshot, setVoucherSnapshot] = useState<VoucherValidateSuccess | null>(null);
  const [validatedVoucherCode, setValidatedVoucherCode] = useState<string | null>(null);
  const [voucherCheckLoading, setVoucherCheckLoading] = useState(false);
  const [voucherLocalError, setVoucherLocalError] = useState<string | null>(null);
  const voucherValidatedTotalRef = useRef<number | null>(null);
  /** One-shot key so large cash operator warning does not repeat until amount changes. */
  const largeCashWarningAckKeyRef = useRef<string | null>(null);

  /** Eligibility preview: read-only UI info. Only when customer selected (not guest) and cart has items. */
  const [eligibilityPreview, setEligibilityPreview] = useState<BenefitEligibilityPreviewResponse | null>(null);
  const [eligibilityPreviewLoading, setEligibilityPreviewLoading] = useState(false);
  const eligibilityPreviewRequestIdRef = useRef(0);

  const {
    cashRegisterId,
    cashRegisterResolved,
    settingsLoadFailed,
    retryUserSettingsLoad,
    registerPicklist,
    registerListLoading,
    registerListFailureKind,
    registerListEmptyReason,
    refetchRegisterList,
    savingRegisterId,
    hasValidCashRegisterId,
    isRegisterGateBlockingPayment,
    handlePersistCashRegister,
    refreshPosReadiness,
    posReadinessLoading,
    posReadinessError,
    posReadinessNextAction,
    posReadinessMessageCode,
    posReadinessRegisterStatus,
  } = usePosCashRegisterAssignment(visible);

  const registerGateCtx = useMemo(
    () =>
      buildPosRegisterGateContext({
        settingsLoadFailed,
        registerListFailureKind,
        registerListLoading,
        registerPicklistCount: registerPicklist.length,
        registerListEmptyReason,
        readiness: {
          loading: posReadinessLoading,
          error: posReadinessError,
          nextAction: posReadinessNextAction,
          messageCode: posReadinessMessageCode,
          registerStatus: posReadinessRegisterStatus,
        },
      }),
    [
      settingsLoadFailed,
      registerListFailureKind,
      registerListLoading,
      registerPicklist.length,
      registerListEmptyReason,
      posReadinessLoading,
      posReadinessError,
      posReadinessNextAction,
      posReadinessMessageCode,
      posReadinessRegisterStatus,
    ]
  );

  // DEV: TSE Simulation Toggle
  // Default: AÇIK (Bypass) in Development
  const [isTseSimulationEnabled, setIsTseSimulationEnabled] = useState<boolean>(__DEV__);

  const [showStartbelegSaleModal, setShowStartbelegSaleModal] = useState(false);
  const [fiscalTseGateOk, setFiscalTseGateOk] = useState<boolean | null>(null);
  const [tseCheckFailureStreak, setTseCheckFailureStreak] = useState(0);

  useEffect(() => {
    if (!visible) {
      setShowStartbelegSaleModal(false);
      largeCashWarningAckKeyRef.current = null;
      return;
    }
    if (!POS_ENSURE_READY_ON_ENTRY) {
      setShowStartbelegSaleModal(false);
      return;
    }
    if (
      posReadinessNextAction === 'startbeleg_required' ||
      posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.STARTBELEG_REQUIRED
    ) {
      setShowStartbelegSaleModal(true);
    } else {
      setShowStartbelegSaleModal(false);
    }
  }, [visible, posReadinessNextAction, posReadinessMessageCode]);

  useEffect(() => {
    if (!visible) {
      setFiscalTseGateOk(null);
      return;
    }
    const needTse = __DEV__ ? !isTseSimulationEnabled : true;
    if (!needTse) {
      setFiscalTseGateOk(true);
      const streak = registerPosTseStatusCheckOutcome(true);
      setTseCheckFailureStreak(streak);
      return;
    }
    let cancelled = false;
    setFiscalTseGateOk(null);
    void checkTseStatus()
      .then((s) => {
        if (cancelled) return;
        const ok = Boolean(s.isConnected && s.canCreateInvoices);
        setFiscalTseGateOk(ok);
        const streak = registerPosTseStatusCheckOutcome(ok);
        setTseCheckFailureStreak(streak);
      })
      .catch(() => {
        if (cancelled) return;
        setFiscalTseGateOk(false);
        const streak = registerPosTseStatusCheckOutcome(false);
        setTseCheckFailureStreak(streak);
      });
    return () => {
      cancelled = true;
    };
  }, [visible, isTseSimulationEnabled]);

  const {
    methodsLoading,
    error,
    paymentMethods,
    getPaymentMethods,
    validateVoucher,
    processPayment,
    clearError
  } = usePayment();

  const requiresCashAmount = useMemo(() => {
    if (!selectedPaymentMethod) return false;
    const m = paymentMethods.find((x) => x.type === selectedPaymentMethod);
    if (m?.requiresReceivedAmount !== undefined) return m.requiresReceivedAmount;
    return selectedPaymentMethod === 'cash';
  }, [paymentMethods, selectedPaymentMethod]);

  const settlementPaymentMethods = useMemo(() => {
    let list = (paymentMethods ?? []).filter((m) => m.type !== 'voucher');
    if (tseServerOffline) {
      list = list.filter((m) => m.type === 'cash');
    }
    return list;
  }, [paymentMethods, tseServerOffline]);

  const selectedSettlementMethod = useMemo(
    () => settlementPaymentMethods.find((m) => m.type === selectedPaymentMethod) ?? null,
    [settlementPaymentMethods, selectedPaymentMethod]
  );

  /** Mirrors submit guard: POST settlement method must not be catalog-only `voucher` row. */
  const hasValidSettlementMethod = useMemo(
    () =>
      !!selectedPaymentMethod &&
      settlementPaymentMethods.some((m) => m.type === selectedPaymentMethod),
    [selectedPaymentMethod, settlementPaymentMethods]
  );

  useEffect(() => {
    if (!requiresCashAmount) {
      setIsAmountValid(true);
      return;
    }
    const r = parseLocaleDecimal(amountReceived);
    setIsAmountValid(Number.isFinite(r) && r > 0);
  }, [amountReceived, requiresCashAmount]);

  useEffect(() => {
    if (!voucherEnabled) {
      setIsVoucherCodeValid(true);
      return;
    }
    setIsVoucherCodeValid(voucherCode.trim().length > 0);
  }, [voucherEnabled, voucherCode]);

  // Backend line toplamları kullan - FE hesaplama yapmaz (totalPrice = lineGross)
  const calculatedCartItems = useMemo(() => {
    return cartItems.map(item => ({
      ...item,
      lineTotal: item.totalPrice ?? (item.quantity * item.unitPrice)
    }));
  }, [cartItems]);

  const cartLineSumGross = calculatedCartItems.reduce((sum, item) => sum + item.lineTotal, 0);
  /**
   * Prefer backend `grandTotalGross` when > 0. When it is 0, treat as true €0 cart only if line gross is also ~0;
   * otherwise assume missing/default 0 from UI helpers and use line sum (matches legacy `> 0 ? gross : lines`).
   */
  const totalAmount = (() => {
    if (grandTotalGross != null && Number.isFinite(grandTotalGross)) {
      if (grandTotalGross > 0) return grandTotalGross;
      if (grandTotalGross === 0 && cartLineSumGross <= PAYMENT_COVERAGE_TOLERANCE_EUR) return 0;
      if (grandTotalGross === 0 && cartLineSumGross > PAYMENT_COVERAGE_TOLERANCE_EUR) return cartLineSumGross;
    }
    return cartLineSumGross;
  })();

  const resetVoucherUi = () => {
    setVoucherCode('');
    setVoucherRedeemAmountStr('');
    setVoucherSnapshot(null);
    setValidatedVoucherCode(null);
    setVoucherLocalError(null);
    setVoucherCheckLoading(false);
    voucherValidatedTotalRef.current = null;
  };

  useEffect(() => {
    if (!visible || !tseServerOffline) return;
    setVoucherEnabled(false);
    resetVoucherUi();
  }, [visible, tseServerOffline]);

  useEffect(() => {
    if (!visible || !tseServerOffline) return;
    if (selectedPaymentMethod && selectedPaymentMethod !== 'cash') {
      setSelectedPaymentMethodType('cash');
    }
  }, [visible, tseServerOffline, selectedPaymentMethod, setSelectedPaymentMethodType]);

  const voucherRedeemParsed = parseLocaleDecimal(voucherRedeemAmountStr);
  const voucherRequestedAmount = Number.isFinite(voucherRedeemParsed) ? Math.max(0, voucherRedeemParsed) : 0;
  const effectiveVoucherRedeemCap = voucherSnapshot
    ? effectiveVoucherRedeemCapFromSnapshot(voucherSnapshot)
    : 0;
  const voucherMaxForSale = computeVoucherMaxForSale(totalAmount, voucherSnapshot, voucherEnabled);

  /** Clamped EUR for POST / UI; empty redeem field → 0 (NaN-safe). Recalculates on every relevant change—same render as Restbetrag. */
  const voucherRedeemAmountEffective = useMemo(() => {
    if (!voucherEnabled || !voucherSnapshot || validatedVoucherCode?.trim() !== voucherCode.trim()) {
      return 0;
    }
    const cap = computeVoucherMaxForSale(totalAmount, voucherSnapshot, true);
    const trimmed = voucherRedeemAmountStr.trim();
    if (trimmed === '') {
      return 0;
    }
    const parsed = parseLocaleDecimal(voucherRedeemAmountStr);
    const requested = Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
    return Math.min(requested, cap);
  }, [
    voucherEnabled,
    voucherSnapshot,
    validatedVoucherCode,
    voucherCode,
    totalAmount,
    voucherRedeemAmountStr,
  ]);

  const voucherRemainingToPay = Math.max(0, totalAmount - voucherRedeemAmountEffective);
  const settlementAmountDue = voucherEnabled ? voucherRemainingToPay : totalAmount;
  const shouldCollectCashAmount = requiresCashAmount && settlementAmountDue > 0;
  const changeAmount = parseLocaleDecimal(amountReceived) - settlementAmountDue;

  const insets = useSafeAreaInsets();

  // Smart Cash Presets Logic
  const getCashPresets = (total: number): number[] => {
    const presets: number[] = [];
    // Base denominations (Euro)
    const bills = [5, 10, 20, 50, 100, 200, 500];

    for (const bill of bills) {
      if (bill >= total) {
        presets.push(bill);
      }
    }
    // Limit to 4 options
    return presets.slice(0, 4);
  };

  const cashPresets = getCashPresets(settlementAmountDue);

  const mixedCoverage = useMemo(
    () =>
      computeVoucherPlusCashCoversTotal({
        voucherEnabled,
        appliedVoucherAmount: voucherRedeemAmountEffective,
        totalCartAmount: totalAmount,
        settlementAmountDue,
        requiresCashAmount,
        amountReceivedStr: amountReceived,
      }),
    [
      voucherEnabled,
      voucherRedeemAmountEffective,
      totalAmount,
      settlementAmountDue,
      requiresCashAmount,
      amountReceived,
    ]
  );

  const voucherCodeMatchesValidated =
    !!voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim();

  /**
   * Near-zero cart: Prüfen success + matching code is enough (no redeem EUR line). Positive carts: unchanged redeem rules.
   * Full voucher coverage fix: when maxForThisCart is 0 the redeem field stays empty but settlement must still validate if cart total is ~€0.
   */
  const voucherSettlementValid =
    !voucherEnabled ||
    (voucherCodeMatchesValidated &&
      (totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR || // FIX: full voucher coverage
        (Number.isFinite(voucherRedeemParsed) &&
          voucherRedeemParsed > 0 &&
          voucherRedeemAmountEffective > 0)));

  /**
   * Block Zahlen until register gate passes + in-flight payment (paymentBusy / processing).
   * Omit hook `loading`: it can block unrelated actions and caused silent early-return in handlePayment without matching disabled state in edge races.
   */
  const registerHardStopDecommissioned = isRegisterGateDecommissioned(registerGateCtx);
  /** Blocks payment method / cash / voucher controls (register decommissioned or NTP clock critical). */
  const paymentInteractionsLocked = registerHardStopDecommissioned || timeSyncCritical;

  const needFiscalTseForPay = __DEV__ ? !isTseSimulationEnabled : true;
  /** When backend health is Offline, cash-only queue is allowed — do not block on local device probe. */
  const payGateTseBlocked =
    needFiscalTseForPay && !tseServerOffline && fiscalTseGateOk !== true;
  const offlineBlocksVoucher =
    (!isOnline && voucherEnabled) || (tseServerOffline && voucherEnabled);

  const paymentCoverageOk =
    (totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR &&
      voucherEnabled &&
      voucherSettlementValid) || // FIX: full voucher coverage
    mixedCoverage.coversTotal;

  const paySubmitDisabled =
    purchaseState === 'processing' ||
    paymentBusy ||
    methodsLoading ||
    !hasValidSettlementMethod ||
    isRegisterGateBlockingPayment ||
    payGateTseBlocked ||
    offlineBlocksVoucher ||
    !paymentCoverageOk ||
    (voucherEnabled && !voucherSettlementValid) ||
    timeSyncCritical;

  const showPayWorking = purchaseState === 'processing' || paymentBusy;

  const paySubmitBlockedHint =
    paySubmitDisabled && !showPayWorking
      ? timeSyncCritical
        ? 'Systemzeit fehlerhaft — Zahlungen blockiert. Administrator kontaktieren.'
        : methodsLoading
          ? 'Zahlungsarten werden geladen…'
          : !hasValidSettlementMethod
            ? 'Bitte eine Zahlungsart wählen (Schritt 2).'
            : !paymentCoverageOk
              ? 'Deckung fehlt: Gutschein + Barbetrag erreicht nicht den Gesamtbetrag.'
              : voucherEnabled && !voucherSettlementValid
                ? 'Gutschein ungültig — bitte „Prüfen“ und Betrag prüfen.'
                : isRegisterGateBlockingPayment
                  ? registerGateFooterHint(registerGateCtx)
                  : payGateTseBlocked
                    ? 'TSE nicht bereit — fiskalische Zahlung nicht möglich.'
                    : offlineBlocksVoucher
                      ? 'Gutschein ist offline nicht möglich.'
                      : undefined
      : undefined;

  useEffect(() => {
    if (!visible) return;
    debugPosPaymentTrace('modal_open', {
      cartItemCount: cartItems.length,
      totalAmount,
      customerId: customerId?.slice(0, 8) ?? null,
    });
  }, [visible, cartItems.length, totalAmount, customerId]);

  useEffect(() => {
    if (!visible) return;
    void refetchTimeSync();
  }, [visible, refetchTimeSync]);

  useEffect(() => {
    if (!visible) return;
    debugPosPaymentTrace('zahlen_button_disabled_snapshot', {
      paySubmitDisabled,
      purchaseState,
      paymentBusy,
      cashRegisterResolved,
      hasValidCashRegisterId,
      isRegisterGateBlockingPayment,
    });
  }, [
    visible,
    paySubmitDisabled,
    purchaseState,
    paymentBusy,
    cashRegisterResolved,
    hasValidCashRegisterId,
    isRegisterGateBlockingPayment,
  ]);

  useEffect(() => {
    if (!visible) return;
    debugPosPaymentTrace('payment_modal_mounted_while_visible', { platform: Platform.OS });
    return () => {
      debugPosPaymentTrace('payment_modal_cleanup_visible_false_or_unmount', { platform: Platform.OS });
    };
  }, [visible]);

  // Load payment methods and guest customer when modal opens (cash register: usePosCashRegisterAssignment)
  useEffect(() => {
    if (!visible) return;
    getPaymentMethods();
    customerService.getGuestCustomer()
      .then((id) => setGuestCustomerId(id))
      .catch((err) => console.warn('[PaymentModal] Failed to load guest customer:', err));
  }, [visible, getPaymentMethods]);

  useEffect(() => {
    if (!visible) {
      resetCheckoutPaymentUi();
      setVoucherEnabled(false);
    }
  }, [visible, resetCheckoutPaymentUi]);

  useEffect(() => {
    if (selectedPaymentMethod === 'voucher') {
      setSelectedPaymentMethodType(null);
    }
  }, [selectedPaymentMethod, setSelectedPaymentMethodType]);

  /** Cart total changed after voucher validation — require a new check (keep typed code). */
  useEffect(() => {
    if (!visible) return;
    if (!voucherEnabled || !voucherSnapshot || voucherValidatedTotalRef.current == null) return;
    if (Math.abs(voucherValidatedTotalRef.current - totalAmount) > 0.02) {
      setVoucherSnapshot(null);
      setValidatedVoucherCode(null);
      voucherValidatedTotalRef.current = null;
      setVoucherRedeemAmountStr('');
      setVoucherLocalError(t('checkout:posFlow.payment.voucher.totalChangedHint'));
    }
  }, [totalAmount, visible, voucherEnabled, voucherSnapshot, t]);

  // Eligibility preview: only when customer selected (not guest), cart has items, and modal visible. Race-safe.
  const shouldFetchEligibility =
    visible &&
    !!customerId &&
    customerId !== '00000000-0000-0000-0000-000000000000' &&
    !isWalkInCustomerId(customerId) &&
    Array.isArray(cartItems) &&
    cartItems.length > 0;

  const cartSignature = useMemo(
    () =>
      cartItems.map((i) => `${i.productId}:${(i as any).qty ?? i.quantity}`).sort().join(','),
    [cartItems]
  );

  useEffect(() => {
    if (!shouldFetchEligibility) {
      setEligibilityPreview(null);
      setEligibilityPreviewLoading(false);
      return;
    }
    const requestId = ++eligibilityPreviewRequestIdRef.current;
    setEligibilityPreviewLoading(true);
    setEligibilityPreview(null);
    const items = cartItems.map((item) => ({
      productId: item.productId,
      quantity: (item as any).qty ?? item.quantity,
    }));
    customerService
      .getBenefitEligibilityPreview(customerId!, items)
      .then((data) => {
        if (requestId !== eligibilityPreviewRequestIdRef.current) return;
        setEligibilityPreview(data ?? null);
      })
      .catch(() => {
        if (requestId !== eligibilityPreviewRequestIdRef.current) return;
        setEligibilityPreview(null);
      })
      .finally(() => {
        if (requestId !== eligibilityPreviewRequestIdRef.current) return;
        setEligibilityPreviewLoading(false);
      });
  }, [visible, shouldFetchEligibility, customerId, cartSignature]);

  // Ödeme başarılı olunca fiş verisini çek (ReceiptSummary için)
  useEffect(() => {
    if (!completedPaymentId) {
      setReceiptData(null);
      return;
    }
    paymentService.getReceipt(completedPaymentId)
      .then((raw: any) => {
        const r = raw?.data ?? raw?.Value ?? raw;
        if (r && Array.isArray(r.items)) {
          setReceiptData(normalizeReceiptDto(r));
        }
      })
      .catch(err => {
        console.warn('[PaymentModal] Receipt fetch failed:', err);
        setReceiptData(null);
      });
  }, [completedPaymentId]);

  const handleOpenReceiptPdf = async () => {
    if (!completedPaymentId || pdfLoading) return;
    setPdfLoading(true);
    try {
      const blob = await downloadInvoicePdf(completedPaymentId);
      debugPosPaymentTrace('success_flow_pdf_ready', { paymentId: completedPaymentId, bytes: blob.size });
      if (Platform.OS === 'web' && typeof window !== 'undefined') {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener,noreferrer');
        setTimeout(() => URL.revokeObjectURL(url), 120_000);
      } else {
        const fileUri = `${FileSystem.documentDirectory}beleg_${completedPaymentId}.pdf`;
        const base64 = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => {
            const r = reader.result as string;
            resolve(r.includes(',') ? r.split(',')[1] : r);
          };
          reader.onerror = () => reject(new Error('read failed'));
          reader.readAsDataURL(blob);
        });
        await FileSystem.writeAsStringAsync(fileUri, base64, {
          encoding: FileSystem.EncodingType.Base64,
        });
        await Sharing.shareAsync(fileUri, {
          mimeType: 'application/pdf',
          dialogTitle: t('invoices:pdfDialogTitle'),
        });
      }
    } catch (e) {
      console.warn('[PaymentModal] PDF open failed:', e);
      if (e instanceof InvoicePdfHttpError) {
        const prefix = t('checkout:posFlow.payment.pdfErrors.paymentSucceededPrefix');
        if (e.status === 401) {
          Alert.alert(
            t('checkout:posFlow.payment.pdfErrors.title'),
            t('checkout:posFlow.payment.pdfErrors.unauthorized', { prefix })
          );
        } else if (e.status === 403) {
          Alert.alert(
            t('checkout:posFlow.payment.pdfErrors.title'),
            t('checkout:posFlow.payment.pdfErrors.forbidden', { prefix })
          );
        } else if (e.status === 404) {
          Alert.alert(
            t('checkout:posFlow.payment.pdfErrors.title'),
            t('checkout:posFlow.payment.pdfErrors.notFound', { prefix })
          );
        } else {
          Alert.alert(
            t('checkout:posFlow.payment.pdfErrors.title'),
            t('checkout:posFlow.payment.pdfErrors.httpGeneric', { prefix, status: e.status })
          );
        }
      } else {
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.errors.printFailed')
        );
      }
    } finally {
      setPdfLoading(false);
    }
  };

  // Handler for preset buttons
  const handlePresetPress = (amount: number) => {
    setAmountReceived(amount.toString());
  };

  const handleVoucherCheck = async () => {
    const trimmed = voucherCode.trim();
    if (!trimmed) {
      setVoucherLocalError(t('checkout:posFlow.payment.voucher.invalid'));
      return;
    }
    setVoucherCheckLoading(true);
    setVoucherLocalError(null);
    try {
      const r = await validateVoucher(trimmed);
      if (!r.ok) {
        const code = (r.errorCode ?? '').toUpperCase();
        let msg = t('checkout:posFlow.payment.voucher.invalid');
        if (code === 'EXPIRED') msg = t('checkout:posFlow.payment.voucher.expired');
        else if (code === 'CANCELLED') msg = t('checkout:posFlow.payment.voucher.cancelled');
        else if (code === 'REDEEMED') msg = t('checkout:posFlow.payment.voucher.redeemed');
        else if (code === 'NOT_YET_VALID') msg = t('checkout:posFlow.payment.voucher.notYetValid');
        else if (code === 'NOT_FOUND') msg = t('checkout:posFlow.payment.voucher.notFound');
        else if (code === 'NETWORK') msg = t('checkout:posFlow.payment.voucher.networkError');
        else if (r.message) msg = r.message;
        setVoucherLocalError(msg);
        setVoucherSnapshot(null);
        setValidatedVoucherCode(null);
        voucherValidatedTotalRef.current = null;
        return;
      }
      setVoucherSnapshot(r);
      setValidatedVoucherCode(trimmed);
      const maxForThisCart = computeVoucherMaxForSale(totalAmount, r, true);
      setVoucherRedeemAmountEffective(maxForThisCart);
      voucherValidatedTotalRef.current = totalAmount;
    } finally {
      setVoucherCheckLoading(false);
    }
  };

  /** POST /api/pos/payment — exhaustive logs + Debug Error on every guard exit (operator confirmations log only). */
  const executePaymentSubmission = async () => {
    const logPay = (step: string, detail?: Record<string, unknown>) => {
      console.log(`[PaymentModal] ${step}`, detail ?? '');
    };

    if (timeSyncCritical) {
      logPay('Guard exit: system time / NTP critical (fiscal payments blocked)');
      return;
    }

    const submitCoverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled,
      appliedVoucherAmount: voucherRedeemAmountEffective,
      totalCartAmount: totalAmount,
      settlementAmountDue,
      requiresCashAmount,
      amountReceivedStr: amountReceived,
    });

    logPay('Step 1: Validation start', {
      paySubmitDisabled,
      voucherEnabled,
      selectedPaymentMethod,
      totalCartAmount: totalAmount,
      settlementRestbetrag: settlementAmountDue,
      voucherRedeemAmountEffective,
    });

    logPay('Step 2: Settlement method check', {
      selectedPaymentMethod,
      hasValidSettlementMethod,
      settlementCodes: settlementPaymentMethods.map((m) => m.type),
    });

    logPay('Step 3: Calculating total paid (Voucher + cash)', {
      voucherEur: voucherEnabled ? voucherRedeemAmountEffective : 0,
      cashReceivedParsed: parseLocaleDecimal(amountReceived),
      sumPaid: submitCoverage.sumPaid,
      totalCartAmount: totalAmount,
      coversTotal: submitCoverage.coversTotal,
    });

    debugPosPaymentTrace('submit_clicked', {
      authUserPresent: !!user?.id,
      paySubmitDisabled,
      cashRegisterResolved,
      hasValidCashRegisterId,
    });

    if (paymentBusy || purchaseState === 'processing') {
      debugPosPaymentTrace('submit_blocked_busy', { paymentBusy, purchaseState });
      logPay('Guard exit: busy');
      Alert.alert('Debug Error', 'Failed at: paymentBusy or purchaseState processing');
      return;
    }

    if (offlineBlocksVoucher) {
      logPay('Guard exit: offline blocks voucher');
      Alert.alert('Debug Error', 'Failed at: Gutschein offline nicht möglich');
      return;
    }

    if (!selectedPaymentMethod || !settlementPaymentMethods.some((m) => m.type === selectedPaymentMethod)) {
      setPaymentMethodSubmitAttempted(true);
      debugPosPaymentTrace('submit_blocked_no_payment_method', {});
      logPay('Guard exit: no settlement method');
      Alert.alert(
        'Debug Error',
        'Failed at: keine gültige Zahlungsart (Schritt 2; nicht nur „Gutschein“ als Methodentyp)'
      );
      return;
    }

    if (needFiscalTseForPay && payGateTseBlocked) {
      logPay('Guard exit: TSE gate');
      Alert.alert('Debug Error', 'Failed at: TSE nicht bereit');
      return;
    }

    const resolvedTableNumber =
      tableNumber != null && Number.isFinite(Number(tableNumber)) && Number(tableNumber) >= 1
        ? Number(tableNumber)
        : 1;

    if (cartItems.length === 0) {
      debugPosPaymentTrace('submit_blocked_empty_cart', {});
      logPay('Guard exit: empty cart');
      Alert.alert('Debug Error', 'Failed at: Warenkorb leer');
      return;
    }

    if (!submitCoverage.coversTotal) {
      debugPosPaymentTrace('submit_blocked_coverage', {
        sumPaid: submitCoverage.sumPaid,
        totalAmount,
      });
      logPay('Guard exit: coverage');
      Alert.alert(
        'Debug Error',
        `Failed at: Deckung — Gutschein+Bar=${submitCoverage.sumPaid.toFixed(2)} €, Gesamt=${totalAmount.toFixed(2)} €`
      );
      return;
    }

    if (voucherEnabled && !voucherSettlementValid) {
      logPay('Guard exit: voucher settlement invalid');
      Alert.alert('Debug Error', 'Failed at: Gutschein nicht gültig / nicht eingelöst (Prüfen + Betrag)');
      return;
    }

    if (shouldCollectCashAmount) {
      const received = parseLocaleDecimal(amountReceived);
      if (!Number.isFinite(received) || received <= 0) {
        debugPosPaymentTrace('submit_blocked_cash_amount_empty', { received, settlementAmountDue });
        logPay('Guard exit: cash amount empty');
        Alert.alert('Debug Error', 'Failed at: Barbetrag fehlt oder ungültig');
        return;
      }
      if (received + 0.001 < settlementAmountDue) {
        debugPosPaymentTrace('submit_blocked_cash_amount', { received, settlementAmountDue });
        logPay('Guard exit: cash below Restbetrag');
        Alert.alert(
          'Debug Error',
          `Failed at: zu wenig Bargeld (Restbetrag ${settlementAmountDue.toFixed(2)} €, erhalten ${received.toFixed(2)} €)`
        );
        return;
      }
    }

    if (!hasValidCashRegisterId || !cashRegisterId) {
      debugPosPaymentTrace('submit_blocked_missing_cash_register', {
        cashRegisterResolved,
        cashRegisterId: cashRegisterId ?? null,
        registerListFailureKind,
      });
      logPay('Guard exit: cash register');
      Alert.alert(
        'Debug Error',
        `Failed at: Kasse nicht bereit — ${registerGateAlertMessage(registerGateCtx)}`
      );
      return;
    }

    // FIX: full voucher coverage
    const allowZeroTotalWithValidatedVoucher =
      voucherEnabled &&
      voucherSettlementValid &&
      totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR;

    if (!validateAmount(totalAmount) && !allowZeroTotalWithValidatedVoucher) {
      debugPosPaymentTrace('submit_blocked_invalid_amount', { totalAmount });
      logPay('Guard exit: validateAmount totalAmount');
      Alert.alert('Debug Error', `Failed at: ungültiger Gesamtbetrag (${String(totalAmount)})`);
      return;
    }

    // Operator confirmations: German copy; log step only (no duplicate Debug Error alert).
    if (
      selectedPaymentMethod === 'cash' &&
      shouldCollectCashAmount &&
      settlementAmountDue >= POS_LARGE_CASH_WARN_THRESHOLD_EUR
    ) {
      const warnKey = `lc|${settlementAmountDue.toFixed(2)}|${amountReceived.trim()}`;
      if (largeCashWarningAckKeyRef.current !== warnKey) {
        logPay('Defer: large cash operator confirmation', { settlementAmountDue, warnKey });
        Alert.alert(
          t('checkout:posFlow.payment.operatorWarnings.largeCashTitle'),
          t('checkout:posFlow.payment.operatorWarnings.largeCashMessage', {
            threshold: POS_LARGE_CASH_WARN_THRESHOLD_EUR,
          }),
          [
            { text: t('checkout:posFlow.payment.buttons.cancel'), style: 'cancel' },
            {
              text: t('checkout:posFlow.payment.operatorWarnings.largeCashProceed'),
              onPress: () => {
                largeCashWarningAckKeyRef.current = warnKey;
                void executePaymentSubmission();
              },
            },
          ]
        );
        return;
      }
    }

    // REMOVED: voucher full balance confirmation dialog
    // User already confirmed by clicking "Zahlen" after voucher validation
    if (voucherEnabled && voucherSnapshot && (totalAmount - voucherRedeemAmountEffective) <= 0.02) {
      console.log('[PaymentModal] Voucher full coverage - proceeding directly to payment (no dialog)');
      // Continue to API call - do nothing here, just let flow continue
    }

    setPaymentBusy(true);
    try {
      logPay('Step 4: Payload construction (before cart id)');
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(resolvedTableNumber);

        if (!cart || !cart.cartId) {
          throw new Error('Active table cart not found.');
        }
        currentCartId = cart.cartId;
      } catch (cartErr) {
        console.error('Cart fetch failed:', cartErr);
        debugPosPaymentTrace('submit_blocked_cart_fetch', {
          message: cartErr instanceof Error ? cartErr.message : String(cartErr),
        });
        logPay('Guard exit: cart fetch');
        Alert.alert(
          'Debug Error',
          `Failed at: Warenkorb laden — ${cartErr instanceof Error ? cartErr.message : String(cartErr)}`
        );
        return;
      }

      // 3. Determine customer ID (use guest if walk-in)
      const finalCustomerId = customerId && customerId !== '00000000-0000-0000-0000-000000000000'
        ? customerId
        : guestCustomerId;

      // 4. Build payment request: flat items (one PaymentItem per cart line). Phase D: no modifierIds emission; add-ons = product lines only.
      // Guard: flat items only — do not add modifierIds or modifiers (one item per cart line).
      // taxType: paymentService.processPayment normalizes all items before POST / queue
      const paymentItems: PaymentItem[] = cartItems.map((item) => ({
        productId: item.productId,
        quantity: (item as any).qty ?? item.quantity,
        taxType: item.taxType as PaymentItem['taxType'],
      }));

      // NOTE: TSE Logic
      // If Simulation Enabled (Bypass) -> tseRequired: false
      // If Simulation Disabled (Real)  -> tseRequired: true
      // In PROD, always true
      const shouldRequireTse = __DEV__ ? !isTseSimulationEnabled : true;

      // One idempotency key per submit; retries with same key return existing payment
      const idempotencyKey =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `${Date.now()}-${Math.random().toString(36).slice(2, 15)}`;

      const settlementPaymentAmountNumeric = voucherEnabled
        ? settlementAmountDue
        : requiresCashAmount
          ? parseLocaleDecimal(amountReceived)
          : undefined;

      const paymentRequest: PaymentRequest = {
        customerId: finalCustomerId, // Always send valid customer ID (guest or registered)
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod,
          tseRequired: shouldRequireTse,
          amount:
            settlementPaymentAmountNumeric != null && Number.isFinite(settlementPaymentAmountNumeric)
              ? settlementPaymentAmountNumeric
              : undefined,
          ...(voucherEnabled
            ? { voucherRedemptions: [{ code: voucherCode.trim(), amount: voucherRedeemAmountEffective }] }
            : {}),
        },
        tableNumber: resolvedTableNumber,
        totalAmount: totalAmount,
        cashRegisterId,
        notes:
          notes ||
          `Tisch ${resolvedTableNumber} - ${new Date().toLocaleString(
            getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language)
          )}`,
        idempotencyKey
      };

      logPay('Step 4b: Payment payload fields', {
        method: paymentRequest.payment.method,
        settlementAmountOnPayment: paymentRequest.payment.amount,
        voucherRedemptionEur: voucherEnabled ? voucherRedeemAmountEffective : 0,
        totalAmount: paymentRequest.totalAmount,
        voucherRedemptionsCount: voucherEnabled ? 1 : 0,
      });

      // 5. PAYMENT REQUEST (STRICT: error handling)
      setPurchaseState('processing');
      debugPosPaymentTrace('process_payment_called', {
        cashRegisterId: paymentRequest.cashRegisterId,
        itemCount: paymentItems.length,
        method: paymentRequest.payment.method,
        tseRequired: paymentRequest.payment.tseRequired,
        totalAmount: paymentRequest.totalAmount,
        idempotencyKey: paymentRequest.idempotencyKey,
      });

      const DEBUG_PAYLOAD = {
        ...paymentRequest,
        payment: {
          ...paymentRequest.payment,
          voucherRedemptions: paymentRequest.payment.voucherRedemptions?.map((line) => ({
            amount: line.amount,
            code: typeof line.code === 'string' ? `[redacted,len=${line.code.length}]` : line.code,
          })),
        },
      };
      console.log('DEBUG_PAYLOAD:', DEBUG_PAYLOAD);

      logPay('Step 5: Calling processPayment → POST /api/pos/payment', {
        idempotencyKey: paymentRequest.idempotencyKey,
      });

      const response = await processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        debugPosPaymentTrace('payment_non_fiscal_pending_ui', { pendingQueueId: response.pendingQueueId });
        setPurchaseState('input');
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.alerts.fiscalPending')
        );
        return;
      }

      if (response.fiscalStatus === 'SERVER_OFFLINE_QUEUED') {
        debugPosPaymentTrace('payment_server_offline_queued', {
          offlineTransactionId: response.offlineTransactionId ?? null,
        });
        setPurchaseState('input');
        onPosToast?.({
          type: 'success',
          message:
            'Zahlung offline gespeichert – wird automatisch signiert wenn TSE wieder online',
        });
        try {
          await cartService.completeCart(currentCartId, notes || '');
        } catch (completeErr) {
          console.error('[CART] Complete failed (offline queue):', completeErr);
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.errors.completeCartFailed')
          );
        }
        try {
          await cartService.resetCartAfterPayment(currentCartId, 'Payment queued offline (TSE)');
        } catch (resetErr) {
          console.warn('[CART] Reset warning (offline queue):', resetErr);
        }
        const pseudoId = response.paymentId || response.offlineTransactionId || 'offline-queued';
        handleSuccessAndClose(pseudoId);
        return;
      }

      // 6. STRICT: only FISCAL_COMPLETE + paymentId counts as paid sale
      if (
        !response.success ||
        response.fiscalStatus !== 'FISCAL_COMPLETE' ||
        !response.paymentId
      ) {
        debugPosPaymentTrace('submit_blocked_fiscal_incomplete', {
          success: response.success,
          fiscalStatus: response.fiscalStatus,
          paymentId: response.paymentId || null,
        });
        console.error('[PAYMENT] Failed or not fiscally confirmed:', response);
        const errorMsg =
          response.fiscalStatus === 'FAILED'
            ? (response.message || response.error || t('checkout:posFlow.payment.errors.fiscalNotConfirmed'))
            : response.message || response.error || t('checkout:posFlow.payment.errors.failed');
        if (response.fiscalStatus === 'FAILED') {
          const hint = `${response.message || ''} ${response.error || ''}`.toLowerCase();
          if (hint.includes('tse') || hint.includes('signatur') || hint.includes('signature')) {
            const streak = registerPosTseStatusCheckOutcome(false);
            setTseCheckFailureStreak(streak);
          }
        }
        Alert.alert(t('checkout:posFlow.payment.errors.failed'), errorMsg);
        setPurchaseState('input');
        return;
      }

      console.log('[PAYMENT] Success, paymentId:', response.paymentId);
      {
        const streak = registerPosTseStatusCheckOutcome(true);
        setTseCheckFailureStreak(streak);
      }
      setCompletedPaymentId(response.paymentId);
      setCompletedPaymentTse(response.tse ?? null);
      debugPosPaymentTrace('success_flow_qr_ready', {
        paymentId: response.paymentId,
        hasQrPayload: !!response.tse?.qrPayload,
      });

      if (response.invoicePersisted === false) {
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.alerts.invoiceReconcileAttention')
        );
      }

      // 7. CART COMPLETE
      try {
        await cartService.completeCart(currentCartId, notes || '');
        console.log('[CART] Completed:', currentCartId);
      } catch (completeErr) {
        console.error('[CART] Complete failed:', completeErr);
        // ROLLBACK logic omitted for brevity as per previous logic, but strictly we should probably not rollback if payment succeeded but cart failed? 
        // For now, keeping existing flow but ensuring UI handles it.
        // If complete fails, we probably still want to print receipt if payment took money?
        // Let's assume critical failure here means we should notify user.
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.errors.completeCartFailed')
        );
      }

      // 8. CART RESET
      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
        console.log('[CART] Reset complete');
      } catch (resetErr) {
        console.warn('[CART] Reset warning:', resetErr);
        Alert.alert(t('checkout:posFlow.payment.alerts.hintTitle'), t('checkout:posFlow.payment.errors.completeCartFailed'));
      }

      // 9. START PRINTING (QR from GET /api/pos/payment/{id}/qr.png as base64 embed)
      setPurchaseState('printing');
      try {
        await receiptPrinter.print(response.paymentId, {
          isDemoFiscal: response.tse?.isDemoFiscal ?? false,
        });
        setPurchaseState('completed');
      } catch (printErr) {
        console.error('[PRINT] Failed:', printErr);
        setPurchaseState('print_error');
        // User will now see "Retry" or "Skip" buttons
      }

    } catch (err) {
      console.error('Handle Payment Error:', err);
      console.log('[PaymentModal] Step: processPayment or post-submit flow threw', err);
      setPurchaseState('input');
      const message = isPaymentError(err)
        ? getPaymentErrorMessage(err.code)
        : (err instanceof Error ? err.message : t('checkout:posFlow.payment.errors.genericFailed'));
      const title =
        isPaymentError(err) && err.code === 'BENEFIT_DAILY_ALLOWANCE_CONFLICT'
          ? t('checkout:posFlow.payment.alerts.hintTitle')
          : isPaymentError(err) && err.code === 'DEMO_PAYMENT_RESTRICTED'
            ? t('checkout:posFlow.payment.alerts.hintTitle')
            : t('checkout:posFlow.payment.alerts.errorTitle');
      Alert.alert(title, message);
    } finally {
      setPaymentBusy(false);
    }
  };

  const handlePayment = async () => {
    if (timeSyncCritical) {
      return;
    }
    if (timeSyncWarningBand) {
      return new Promise<void>((resolve) => {
        Alert.alert(
          'Zeitabweichung',
          'Zeitabweichung erkannt. Fortfahren trotz möglicher DEP-Probleme?',
          [
            { text: 'Abbrechen', style: 'cancel', onPress: () => resolve() },
            {
              text: 'Fortfahren',
              onPress: () => {
                void executePaymentSubmission();
                resolve();
              },
            },
          ],
          { cancelable: true, onDismiss: () => resolve() }
        );
      });
    }
    await executePaymentSubmission();
  };

  // Helper to finish up: pass tableNumber so layout can clear the paid table
  const handleSuccessAndClose = (paymentId: string) => {
    onSuccess(paymentId, tableNumber);
    handleClose();
  };

  // Retrying print
  const handleRetryPrint = async () => {
    if (!completedPaymentId) return;
    setPurchaseState('printing');
    try {
      await receiptPrinter.print(completedPaymentId, {
        isDemoFiscal: completedPaymentTse?.isDemoFiscal ?? false,
      });
      setPurchaseState('completed');
    } catch (printErr) {
      console.error('[PRINT] Retry Failed:', printErr);
      setPurchaseState('print_error');
    }
  };

  // Skip printing
  const handleSkipPrint = () => {
    if (completedPaymentId) {
      handleSuccessAndClose(completedPaymentId);
    }
  };

  // Modal kapat
  const handleClose = () => {
    clearError();
    resetVoucherUi();
    setVoucherEnabled(false);
    resetCheckoutPaymentUi();
    setAmountReceived('');
    setNotes('');
    setPurchaseState('input');
    setCompletedPaymentId(null);
    setCompletedPaymentTse(null);
    setReceiptData(null);
    setPaymentBusy(false);
    setPdfLoading(false);
    setEligibilityPreview(null);
    setEligibilityPreviewLoading(false);
    onClose();
  };

  return (
    <>
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={handleClose}
      accessibilityLabel={t('checkout:posFlow.payment.title')}
    >
      <View style={styles.overlay} accessibilityViewIsModal>
        {/* Use View (not Pressable) so web does not swallow inner button presses. */}
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('checkout:posFlow.payment.title')}</Text>
            <Pressable
              onPress={handleClose}
              style={({ pressed }) => [styles.closeButton, pressed && SoftState.pressed]}
              hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
              accessibilityLabel={t('common:close')}
              accessibilityRole="button"
            >
              <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
            </Pressable>
          </View>

          <ScrollView style={styles.content} keyboardShouldPersistTaps="handled">
            {/* Step 1: Summe */}
            <View style={styles.section}>
              <Text style={styles.stepLabel}>1</Text>
              <Text style={styles.sectionTitle}>Summe</Text>
              {calculatedCartItems.map((item, index) => (
                <View key={index} style={styles.cartItem}>
                  <Text style={styles.itemName}>{item.productName}</Text>
                  <Text style={styles.itemDetails}>
                    {item.quantity} × {formatPrice(item.unitPrice)} = {formatPrice(item.lineTotal)}
                  </Text>
                </View>
              ))}
              <View style={styles.totalRow}>
                <Text style={styles.totalLabel}>Gesamt</Text>
                <Text style={styles.totalAmount}>{formatPrice(totalAmount)}</Text>
              </View>
            </View>

            {registerHardStopDecommissioned ? (
              <View style={styles.registerHardStopBanner} accessibilityRole="alert">
                <Text style={styles.registerHardStopBannerText}>{POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE}</Text>
              </View>
            ) : null}

            {isRegisterGateBlockingPayment && (
              <View style={styles.registerBanner}>
                <Text style={styles.registerBannerTitle}>{registerGateBannerTitle(registerGateCtx)}</Text>
                {!registerListLoading && !posReadinessLoading ? (
                  <Text style={[styles.registerBannerMuted, { marginBottom: SoftSpacing.xs }]}>
                    {t('settings:registerGate.banner.intro')}
                  </Text>
                ) : null}
                <Text style={styles.registerBannerText}>{registerGateBannerDetail(registerGateCtx)}</Text>
                {registerListLoading || posReadinessLoading ? (
                  <View
                    style={{
                      width: '100%',
                      marginVertical: SoftSpacing.sm,
                      alignItems: 'center',
                      justifyContent: 'center',
                    }}
                  >
                    <WaveLoader size={28} color={SoftColors.accent} />
                  </View>
                ) : registerPicklist.length > 0 ? (
                  <View style={styles.registerChipRow}>
                    {registerPicklist.map((r) => (
                      <Pressable
                        key={r.id}
                        style={({ pressed }) => [
                          styles.registerChip,
                          pressed && SoftState.pressedScale,
                          savingRegisterId === r.id && styles.registerChipDisabled,
                        ]}
                        disabled={!!savingRegisterId}
                        onPress={() => handlePersistCashRegister(r.id)}
                        accessibilityRole="button"
                        accessibilityLabel={`Kasse ${r.registerNumber || r.id} zuweisen`}
                      >
                        <Text style={styles.registerChipText} numberOfLines={1}>
                          {r.registerNumber || r.id.slice(0, 8)}
                        </Text>
                      </Pressable>
                    ))}
                  </View>
                ) : settingsLoadFailed ? (
                  <Pressable
                    onPress={retryUserSettingsLoad}
                    style={styles.retryLink}
                    accessibilityRole="button"
                    accessibilityLabel="Kasseneinstellungen erneut laden"
                  >
                    <Text style={styles.retryLinkText}>Kasseneinstellungen erneut versuchen</Text>
                  </Pressable>
                ) : posReadinessError ? (
                  <Pressable
                    onPress={() => refreshPosReadiness()}
                    style={styles.retryLink}
                    accessibilityRole="button"
                    accessibilityLabel="Kassenbereitschaft erneut laden"
                  >
                    <Text style={styles.retryLinkText}>Kassenbereitschaft erneut versuchen</Text>
                  </Pressable>
                ) : registerListFailureKind === 'network' || registerListFailureKind === 'unknown' ? (
                  <Pressable
                    onPress={refetchRegisterList}
                    style={styles.retryLink}
                    accessibilityRole="button"
                    accessibilityLabel="Kassenliste erneut laden"
                  >
                    <Text style={styles.retryLinkText}>Kassenliste erneut laden</Text>
                  </Pressable>
                ) : null}
              </View>
            )}

            {/* Benefit eligibility preview: read-only info when customer selected (not guest) and cart has items */}
            {shouldFetchEligibility && (
              <View style={styles.benefitPreviewSection}>
                <Text style={styles.benefitPreviewTitle}>Vorteile (Vorschau)</Text>
                {/* Preview-only savings indicator: do not imply final discount; for cashier awareness only */}
                {!eligibilityPreviewLoading && eligibilityPreview && typeof eligibilityPreview.totalDiscountAmount === 'number' && eligibilityPreview.totalDiscountAmount > 0 && (
                  <Text style={styles.savingsIndicatorText}>
                    Möglicher Vorteil: ca. {formatPrice(eligibilityPreview.totalDiscountAmount)}
                  </Text>
                )}
                {eligibilityPreviewLoading ? (
                  <View style={styles.benefitPreviewLoading}>
                    <WaveLoader size={22} color={SoftColors.accent} />
                    <Text style={[styles.benefitPreviewMuted, styles.benefitPreviewLoadingLabel]}>
                      Vorteile werden geladen…
                    </Text>
                  </View>
                ) : eligibilityPreview ? (
                  <>
                    {eligibilityPreview.applicableBenefits.length > 0 && (
                      <View style={styles.benefitList}>
                        {eligibilityPreview.applicableBenefits.map((b, idx) => (
                          <Text key={idx} style={styles.benefitApplicable}>
                            • {b.description} {b.amount < 0 ? formatPrice(Math.abs(b.amount)) : ''}
                          </Text>
                        ))}
                      </View>
                    )}
                    {eligibilityPreview.blockedBenefits.length > 0 && (
                      <View style={styles.benefitList}>
                        {eligibilityPreview.blockedBenefits.map((b, idx) => (
                          <Text key={idx} style={styles.benefitBlocked}>
                            • {formatBlockedReason(t, b)}
                          </Text>
                        ))}
                      </View>
                    )}
                    {(eligibilityPreview.applicableBenefits.length > 0 || eligibilityPreview.blockedBenefits.length > 0) && (
                      <Text style={styles.benefitPreviewDisclaimer}>
                        Vorschau – maßgeblich ist die Abrechnung beim Bezahlen.
                      </Text>
                    )}
                  </>
                ) : null}
              </View>
            )}

            {/* Step 2: Zahlungsart */}
            <View style={styles.section}>
              <Text style={styles.stepLabel}>2</Text>
              <Text style={styles.sectionTitle}>{t('payment:paymentMethod')}</Text>
              <View
                style={[
                  styles.paymentMethodsSectionWrap,
                  paymentMethodSubmitAttempted &&
                    !selectedPaymentMethod &&
                    styles.paymentMethodsSectionWrapInvalid,
                ]}
              >
              <View style={styles.paymentMethodsContainer}>
                {methodsLoading ? (
                  <View style={styles.paymentMethodsLoading}>
                    <WaveLoader color={SoftColors.accent} />
                    <Text style={styles.paymentMethodsLoadingLabel}>{t('common:loading')}</Text>
                  </View>
                ) : settlementPaymentMethods && settlementPaymentMethods.length > 0 ? (
                  settlementPaymentMethods.map((method) => {
                    const isSelected = selectedPaymentMethod === method.type;
                    const methodDisabled = paymentInteractionsLocked;
                    return (
                      <Pressable
                        key={method.id}
                        style={({ pressed }) => [
                          styles.paymentMethod,
                          isSelected && styles.selectedPaymentMethod,
                          methodDisabled && styles.paymentMethodDisabled,
                          pressed && !methodDisabled && SoftState.pressedScale,
                        ]}
                        disabled={methodDisabled}
                        onPress={() => {
                          if (paymentInteractionsLocked) return;
                          if (method.type !== selectedPaymentMethod) {
                            setSelectedPaymentMethodType(method.type);
                          }
                        }}
                        accessibilityRole="button"
                        accessibilityState={{ selected: isSelected, disabled: methodDisabled }}
                        accessibilityLabel={`${method.name}${isSelected ? ', ausgewählt' : ''}`}
                      >
                        <Ionicons
                          name={method.icon as any}
                          size={24}
                          color={isSelected ? SoftColors.accent : SoftColors.textSecondary}
                        />
                        <Text style={[
                          styles.paymentMethodText,
                          isSelected && styles.selectedPaymentMethodText,
                        ]}>
                          {method.name}
                        </Text>
                      </Pressable>
                    );
                  })
                ) : (
                  <View style={styles.errorBlock}>
                    <Text style={styles.errorText}>{t('payment:errors.generalError')}</Text>
                    <Pressable onPress={getPaymentMethods} style={styles.retryLink}>
                      <Text style={styles.retryLinkText}>{t('common:retry')}</Text>
                    </Pressable>
                  </View>
                )}
              </View>
              {paymentMethodSubmitAttempted && !selectedPaymentMethod ? (
                <Text style={styles.paymentMethodRequiredHint} accessibilityRole="alert">
                  {t('checkout:posFlow.payment.errors.paymentMethodRequired')}
                </Text>
              ) : null}
              </View>
            </View>

            {/* Step 3: Nakit – Betrag & Rückgeld */}
            {shouldCollectCashAmount && (
              <View style={styles.section}>
                <Text style={styles.stepLabel}>3</Text>
                <Text style={styles.sectionTitle}>Barzahlung</Text>

                <View style={styles.presetsContainer}>
                  {cashPresets.map((preset) => {
                    const isPresetSelected = amountReceived === preset.toString();
                    return (
                      <Pressable
                        key={preset}
                        style={({ pressed }) => [
                          styles.presetButton,
                          isPresetSelected && styles.presetButtonSelected,
                          pressed && !paymentInteractionsLocked && SoftState.pressedScale,
                          paymentInteractionsLocked && styles.paymentMethodDisabled,
                        ]}
                        disabled={paymentInteractionsLocked}
                        onPress={() => handlePresetPress(preset)}
                        accessibilityRole="button"
                        accessibilityState={{ selected: isPresetSelected }}
                        accessibilityLabel={`${formatPrice(preset)}${isPresetSelected ? ', ausgewählt' : ''}`}
                      >
                        <Text style={[
                          styles.presetButtonText,
                          isPresetSelected && styles.presetButtonTextSelected,
                        ]}>
                          {formatPrice(preset)}
                        </Text>
                      </Pressable>
                    );
                  })}
                </View>

                <View style={styles.inputRow}>
                  <View style={styles.cashLabelWithHint}>
                    <Text style={styles.label}>Erhaltener Betrag</Text>
                    {!isAmountValid ? (
                      <Text style={styles.cashAmountWarnIcon} accessibilityRole="image" accessibilityLabel="⚠">
                        ⚠️
                      </Text>
                    ) : null}
                  </View>
                  <TextInput
                    style={styles.amountInput}
                    value={amountReceived}
                    onChangeText={setAmountReceived}
                    placeholder="0,00"
                    keyboardType="decimal-pad"
                    editable={!paymentInteractionsLocked}
                    accessibilityLabel="Erhaltener Betrag in Euro"
                    accessibilityHint="Mindestens den zu zahlenden Betrag eingeben"
                  />
                </View>
                {parseLocaleDecimal(amountReceived) >= settlementAmountDue && (
                  <View style={styles.changeRow}>
                    <Text style={styles.changeLabel}>Rückgeld</Text>
                    <Text style={styles.changeAmount}>{formatPrice(changeAmount)}</Text>
                  </View>
                )}
              </View>
            )}

            {selectedPaymentMethod && (
              <View style={styles.section}>
                <Text style={styles.stepLabel}>3</Text>
                <View style={styles.paymentMethodsContainer}>
                  <Pressable
                    onPress={() => {
                      if (paymentInteractionsLocked || tseServerOffline) return;
                      if (!isOnline && !voucherEnabled) {
                        Alert.alert('Offline', 'Gutschein ist offline nicht möglich.');
                        return;
                      }
                      if (voucherEnabled) {
                        setVoucherEnabled(false);
                        resetVoucherUi();
                      } else {
                        setVoucherEnabled(true);
                      }
                    }}
                    style={({ pressed }) => [
                      styles.paymentMethod,
                      voucherEnabled && styles.selectedPaymentMethod,
                      (paymentInteractionsLocked || tseServerOffline) && styles.paymentMethodDisabled,
                      pressed && !paymentInteractionsLocked && !tseServerOffline && SoftState.pressedScale,
                    ]}
                    disabled={paymentInteractionsLocked || tseServerOffline}
                    accessibilityRole="button"
                    accessibilityState={{
                      selected: voucherEnabled,
                      disabled: paymentInteractionsLocked || tseServerOffline,
                    }}
                    accessibilityLabel={`Gutschein einlösen${voucherEnabled ? ', ausgewählt' : ''}`}
                  >
                    <Ionicons
                      name="gift-outline"
                      size={24}
                      color={voucherEnabled ? SoftColors.accent : SoftColors.textSecondary}
                    />
                    <Text
                      style={[
                        styles.paymentMethodText,
                        voucherEnabled && styles.selectedPaymentMethodText,
                      ]}
                    >
                      Gutschein einlösen
                    </Text>
                  </Pressable>
                </View>
                {tseServerOffline ? (
                  <Text style={styles.voucherInlineError}>Gutscheine offline nicht verfügbar</Text>
                ) : !isOnline && voucherEnabled ? (
                  <Text style={styles.voucherInlineError}>Gutschein ist offline nicht möglich.</Text>
                ) : null}
                {voucherEnabled ? (
                  <>
                <View style={styles.cashLabelWithHint}>
                  <Text style={styles.voucherFieldLabel}>{t('checkout:posFlow.payment.voucher.codeLabel')}</Text>
                  {!isVoucherCodeValid ? (
                    <Text style={styles.cashAmountWarnIcon} accessibilityRole="image" accessibilityLabel="⚠">
                      ⚠️
                    </Text>
                  ) : null}
                </View>
                <TextInput
                  style={styles.voucherCodeInput}
                  value={voucherCode}
                  onChangeText={(v) => {
                    setVoucherCode(v);
                    setVoucherLocalError(null);
                    const nextTrim = v.trim();
                    if (
                      voucherSnapshot &&
                      validatedVoucherCode != null &&
                      nextTrim !== validatedVoucherCode.trim()
                    ) {
                      setVoucherSnapshot(null);
                      setValidatedVoucherCode(null);
                      voucherValidatedTotalRef.current = null;
                      setVoucherRedeemAmountStr('');
                    }
                  }}
                  placeholder={t('checkout:posFlow.payment.voucher.codeLabel')}
                  autoCapitalize="characters"
                  autoCorrect={false}
                  editable={!voucherCheckLoading && isOnline && !paymentInteractionsLocked}
                  accessibilityLabel={t('checkout:posFlow.payment.voucher.codeLabel')}
                />
                <Pressable
                  onPress={handleVoucherCheck}
                  disabled={voucherCheckLoading || !isOnline || paymentInteractionsLocked}
                  style={({ pressed }) => [
                    styles.voucherCheckButton,
                    (voucherCheckLoading || !isOnline || paymentInteractionsLocked) && styles.voucherCheckButtonDisabled,
                    pressed && !voucherCheckLoading && isOnline && !paymentInteractionsLocked && SoftState.pressedScale,
                  ]}
                  accessibilityRole="button"
                  accessibilityLabel={t('checkout:posFlow.payment.voucher.checkButton')}
                >
                  {voucherCheckLoading ? (
                    <View style={styles.voucherCheckButtonInner}>
                      <WaveLoader size={18} color={SoftColors.accent} />
                      <Text style={styles.voucherCheckButtonText}>{t('checkout:posFlow.payment.voucher.checking')}</Text>
                    </View>
                  ) : (
                    <Text style={styles.voucherCheckButtonText}>{t('checkout:posFlow.payment.voucher.checkButton')}</Text>
                  )}
                </Pressable>
                {voucherSnapshot ? (
                  <View style={styles.voucherInfoBlock}>
                    <Text style={styles.voucherInfoLine}>
                      {t('checkout:posFlow.payment.voucher.balanceLabel')}: {formatPrice(voucherSnapshot.remainingAmount)}
                    </Text>
                    <Text style={styles.voucherInfoLine}>
                      {t('checkout:posFlow.payment.voucher.maxUsableLabel')}:{' '}
                      {formatPrice(effectiveVoucherRedeemCap)}
                    </Text>
                    <Text style={styles.voucherMuted}>
                      {t('checkout:posFlow.payment.voucher.maskedHint', { masked: voucherSnapshot.maskedCode })}
                    </Text>
                    {(() => {
                      const exp = new Date(voucherSnapshot.expiresAtUtc);
                      if (Number.isNaN(exp.getTime())) return null;
                      return (
                        <Text style={styles.voucherMuted}>
                          {t('checkout:posFlow.payment.voucher.expiresHint', {
                            date: exp.toLocaleDateString(
                              getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language),
                              { day: '2-digit', month: '2-digit', year: 'numeric' }
                            ),
                          })}
                        </Text>
                      );
                    })()}
                  </View>
                ) : null}
                <View style={styles.inputRow}>
                  <Text style={styles.label}>{t('checkout:posFlow.payment.voucher.redeemAmountLabel')}</Text>
                  <TextInput
                    style={styles.amountInput}
                    value={voucherRedeemAmountStr}
                    onChangeText={setVoucherRedeemAmountStr}
                    placeholder={t('checkout:posFlow.payment.placeholderAmount')}
                    keyboardType="decimal-pad"
                    editable={!!voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim()}
                    accessibilityLabel={t('checkout:posFlow.payment.voucher.redeemAmountLabel')}
                  />
                </View>
                {voucherSnapshot && Number.isFinite(voucherRedeemParsed) ? (
                  <View style={styles.inputRow}>
                    <Text style={styles.label}>Restbetrag</Text>
                    <Text style={styles.voucherInfoLine}>{formatPrice(voucherRemainingToPay)}</Text>
                  </View>
                ) : null}
                {voucherLocalError ? <Text style={styles.voucherInlineError}>{voucherLocalError}</Text> : null}
                {voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim() ? (
                  <Text style={styles.voucherMuted}>{t('checkout:posFlow.payment.voucher.changeCodeHint')}</Text>
                ) : null}
                  </>
                ) : null}
              </View>
            )}

            <View style={styles.section}>
              <Text style={styles.sectionTitle}>Notizen</Text>
              <TextInput
                style={styles.notesInput}
                value={notes}
                onChangeText={setNotes}
                placeholder="Optionale Notizen…"
                multiline
                numberOfLines={2}
                accessibilityLabel="Optionale Notizen zur Zahlung"
              />
            </View>

            {/* Hata Mesajı */}
            {error && (
              <View style={styles.errorContainer}>
                <Text style={styles.errorText}>{error}</Text>
              </View>
            )}
            {/* DEV: TSE Simulation Toggle */}
            {__DEV__ && (
              <View style={styles.section}>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', backgroundColor: '#fff3e0', padding: 10, borderRadius: 8 }}>
                  <View>
                    <Text style={{ fontWeight: 'bold', color: '#e65100' }}>TSE Simülasyonu</Text>
                    <Text style={{ fontSize: 12, color: '#f57c00' }}>{isTseSimulationEnabled ? 'AÇIK (Bypass)' : 'KAPALI (Gerçek TSE)'}</Text>
                  </View>
                  <Switch
                    value={isTseSimulationEnabled}
                    onValueChange={setIsTseSimulationEnabled}
                    trackColor={{ false: "#767577", true: "#f57c00" }}
                    thumbColor={isTseSimulationEnabled ? "#ffb74d" : "#f4f3f4"}
                  />
                </View>
              </View>
            )}

          </ScrollView>

          {purchaseState === 'input' || purchaseState === 'processing' ? (
            <View style={[styles.footer, { paddingBottom: Math.max(SoftSpacing.md, insets.bottom) }]}>
              {timeSyncCritical ? (
                <View style={styles.timeSyncCriticalBanner} accessibilityRole="alert">
                  <Text style={styles.timeSyncCriticalText}>
                    SYSTEMZEIT FEHLERHAFT – Zahlungen blockiert! Bitte Admin kontaktieren
                  </Text>
                  <Pressable
                    onPress={() =>
                      Alert.alert('Administrator kontaktieren', POS_TIME_SYNC_ADMIN_CONTACT_MESSAGE_DE)
                    }
                    style={({ pressed }) => [styles.timeSyncAdminButton, pressed && SoftState.pressed]}
                    accessibilityRole="button"
                    accessibilityLabel="Administrator kontaktieren"
                  >
                    <Text style={styles.timeSyncAdminButtonText}>Admin kontaktieren</Text>
                  </Pressable>
                </View>
              ) : null}
              {needFiscalTseForPay && tseCheckFailureStreak >= POS_TSE_STATUS_FAILURE_WARN_STREAK ? (
                <View style={styles.operatorWarnBanner} accessibilityRole="alert">
                  <Text style={styles.operatorWarnBannerText}>
                    {t('checkout:posFlow.payment.operatorWarnings.tseUnstableBanner')}
                  </Text>
                </View>
              ) : null}
              <View style={styles.footerButtonRow}>
                <Pressable
                  onPress={handleClose}
                  style={({ pressed }) => [
                    styles.cancelButton,
                    pressed && SoftState.pressed,
                  ]}
                  disabled={showPayWorking}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  accessibilityLabel="Abbrechen"
                  accessibilityRole="button"
                >
                  <Text style={styles.cancelButtonText}>Abbrechen</Text>
                </Pressable>
                {showStornoRefundEntry ? (
                  <Pressable
                    onPress={() => setStornoRefundWizardVisible(true)}
                    style={({ pressed }) => [
                      styles.stornoRefundSideBtn,
                      pressed && SoftState.pressed,
                      showPayWorking && styles.payButtonDisabled,
                    ]}
                    disabled={showPayWorking}
                    hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                    accessibilityLabel={t('checkout:posFlow.stornoRefund.paymentModalEntry')}
                    accessibilityRole="button"
                  >
                    <Text style={styles.stornoRefundSideBtnText} numberOfLines={2}>
                      {t('checkout:posFlow.stornoRefund.paymentModalEntry')}
                    </Text>
                  </Pressable>
                ) : null}
                <Pressable
                  onPress={handlePayment}
                  style={({ pressed }) => [
                    styles.payButton,
                    showStornoRefundEntry && styles.payButtonWhenStornoPresent,
                    paySubmitDisabled && styles.payButtonDisabled,
                    pressed && !paySubmitDisabled && SoftState.pressedScale,
                  ]}
                  disabled={paySubmitDisabled}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  accessibilityLabel={`Zahlen ${formatPrice(settlementAmountDue)}`}
                  accessibilityHint={paySubmitBlockedHint}
                  accessibilityRole="button"
                  accessibilityState={{
                    disabled: paySubmitDisabled,
                  }}
                >
                  {showPayWorking ? (
                    <View style={styles.payButtonContent}>
                      <WaveLoader size={18} color={SoftColors.textInverse} />
                      <Text style={styles.payButtonText}>Wird verarbeitet…</Text>
                    </View>
                  ) : (
                    <Text style={styles.payButtonText}>
                      {voucherEnabled && voucherSettlementValid && paymentCoverageOk && settlementAmountDue <= 0.01
                        ? t('checkout:posFlow.payment.voucher.payCta', { amount: formatPrice(totalAmount) })
                        : settlementAmountDue > 0.01 && selectedSettlementMethod
                          ? `${formatPrice(settlementAmountDue)} ${selectedSettlementMethod.name.toLowerCase()} zahlen`
                          : `${formatPrice(totalAmount)} zahlen`}
                    </Text>
                  )}
                </Pressable>
              </View>
              {!cashRegisterResolved ? (
                <Text style={styles.footerBlockedHint}>Kasseneinstellungen werden geladen…</Text>
              ) : !hasValidCashRegisterId ? (
                <Text style={styles.footerBlockedHint}>
                  {registerGateFooterHint(registerGateCtx)}
                </Text>
              ) : paySubmitBlockedHint ? (
                <Text style={styles.footerBlockedHint}>{paySubmitBlockedHint}</Text>
              ) : null}
            </View>
          ) : (
            <View style={[styles.footerSecondary, { paddingBottom: Math.max(SoftSpacing.md, insets.bottom) }]}>
              {purchaseState === 'printing' && (
                <View style={styles.statusBlock}>
                  <WaveLoader size={36} color={SoftColors.accent} />
                  <Text style={styles.statusText}>Beleg wird gedruckt…</Text>
                </View>
              )}

              {purchaseState === 'completed' && (
                <View style={styles.statusBlock}>
                  <Ionicons name="checkmark-circle" size={48} color={SoftColors.success} />
                  <Text style={styles.successTitle}>Zahlung erfolgreich</Text>
                  {(() => {
                    // Receipt: GET /api/pos/payment/{id}/receipt
                    const summaryReceipt = toSummaryReceipt(receiptData ?? null);
                    return summaryReceipt ? (
                      <View style={[styles.receiptPreview, { marginTop: SoftSpacing.sm, maxHeight: 320 }]}>
                        <ReceiptSummary receipt={summaryReceipt} mode="cashier" />
                      </View>
                    ) : null;
                  })()}
                  <PaymentSuccessQr
                    tse={completedPaymentTse}
                    paymentId={completedPaymentId}
                    fetchServerPng
                    size={160}
                  />
                  <View style={styles.successActionsRow}>
                    <Pressable
                      onPress={handleOpenReceiptPdf}
                      disabled={pdfLoading || !completedPaymentId}
                      style={({ pressed }) => [
                        styles.successSecondaryBtn,
                        pressed && SoftState.pressed,
                        (pdfLoading || !completedPaymentId) && styles.payButtonDisabled,
                      ]}
                      accessibilityRole="button"
                      accessibilityLabel="Beleg-PDF"
                    >
                      {pdfLoading ? (
                        <WaveLoader size={20} color={SoftColors.accent} />
                      ) : (
                        <Text style={styles.successSecondaryBtnText}>Beleg-PDF</Text>
                      )}
                    </Pressable>
                    <Pressable
                      onPress={() => completedPaymentId && handleSuccessAndClose(completedPaymentId)}
                      style={({ pressed }) => [styles.successPrimaryBtn, pressed && SoftState.pressedScale]}
                      accessibilityRole="button"
                      accessibilityLabel="Fertig"
                    >
                      <Text style={styles.payButtonText}>Fertig</Text>
                    </Pressable>
                  </View>
                </View>
              )}

              {purchaseState === 'print_error' && (
                <View style={styles.printErrorBlock}>
                  <Text style={styles.printErrorTitle}>Beleg konnte nicht gedruckt werden</Text>
                  {(() => {
                    const summaryReceipt = toSummaryReceipt(receiptData ?? null);
                    return summaryReceipt ? (
                      <View style={styles.receiptPreview}>
                        <ReceiptSummary receipt={summaryReceipt} mode="cashier" />
                      </View>
                    ) : null;
                  })()}
                  <PaymentSuccessQr
                    tse={completedPaymentTse}
                    paymentId={completedPaymentId}
                    fetchServerPng
                    size={140}
                  />
                  <View style={styles.printErrorActions}>
                    <Pressable
                      onPress={handleOpenReceiptPdf}
                      disabled={pdfLoading || !completedPaymentId}
                      style={[styles.printErrorBtnSecondary, pdfLoading && styles.payButtonDisabled]}
                    >
                      {pdfLoading ? (
                        <WaveLoader size={20} color={SoftColors.accent} />
                      ) : (
                        <Text style={styles.printErrorBtnSecondaryText}>Beleg-PDF</Text>
                      )}
                    </Pressable>
                    <Pressable onPress={handleSkipPrint} style={styles.printErrorBtnSecondary}>
                      <Text style={styles.printErrorBtnSecondaryText}>Überspringen</Text>
                    </Pressable>
                    <Pressable onPress={handleRetryPrint} style={styles.payButton}>
                      <Text style={styles.payButtonText}>Erneut drucken</Text>
                    </Pressable>
                  </View>
                </View>
              )}
            </View>
          )}
        </View>
      </View>
    </Modal>

    <Modal
      visible={visible && showStartbelegSaleModal}
      transparent
      animationType="fade"
      onRequestClose={() => setShowStartbelegSaleModal(false)}
    >
      <View style={styles.startbelegBlockBackdrop}>
        <View style={styles.startbelegBlockCard}>
          <Text style={styles.startbelegBlockTitle}>Startbeleg erforderlich</Text>
          <Text style={styles.startbelegBlockBody}>
            Für diese Registrierkasse fehlt der fiskalische Startbeleg (RKSV). Erstellen Sie zuerst den Startbeleg, danach
            sind Verkäufe möglich.
          </Text>
          <Pressable
            onPress={() => setShowStartbelegSaleModal(false)}
            style={({ pressed }) => [styles.startbelegBlockBtn, pressed && SoftState.pressedScale]}
            accessibilityRole="button"
            accessibilityLabel="Verstanden"
          >
            <Text style={styles.startbelegBlockBtnText}>Verstanden</Text>
          </Pressable>
        </View>
      </View>
    </Modal>

    <StornoRefundSelection
      visible={stornoRefundWizardVisible}
      onClose={() => setStornoRefundWizardVisible(false)}
      cashRegisterId={cashRegisterId ?? '00000000-0000-0000-0000-000000000000'}
      tableNumber={tableNumber ?? 0}
      onSuccess={() => {
        onPosToast?.({
          type: 'success',
          message: t('checkout:posFlow.stornoRefund.alerts.successTitle'),
        });
      }}
    />
    </>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'flex-end',
    // Web: tab bar / custom tab buttons use overflow:visible and can sit above a low stacking context;
    // fixed + high z-index keeps the payment sheet and Zahlen hit-target above the tab UI.
    ...(Platform.OS === 'web'
      ? ({
          position: 'fixed' as const,
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 2147483646,
        } as const)
      : {}),
  },
  modal: {
    backgroundColor: SoftColors.bgCard,
    borderTopLeftRadius: SoftRadius.xl,
    borderTopRightRadius: SoftRadius.xl,
    maxHeight: '90%',
    ...(Platform.OS === 'web' ? ({ zIndex: 2147483647 } as const) : {}),
    ...SoftShadows.lg,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: SoftSpacing.md,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
  },
  closeButton: {
    padding: SoftSpacing.sm,
    minWidth: 44,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  content: {
    padding: SoftSpacing.md,
  },
  section: {
    marginBottom: SoftSpacing.lg,
  },
  stepLabel: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginBottom: SoftSpacing.xs,
  },
  sectionTitle: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.borderLight,
  },
  itemName: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
    flex: 1,
  },
  itemDetails: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: SoftSpacing.sm,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
    marginTop: SoftSpacing.xs,
  },
  totalLabel: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
  },
  totalAmount: {
    ...SoftTypography.priceTotal,
    color: SoftColors.accent,
  },
  benefitPreviewSection: {
    marginBottom: SoftSpacing.lg,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.md,
  },
  benefitPreviewTitle: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.xs,
    fontWeight: '600',
  },
  savingsIndicatorText: {
    ...SoftTypography.caption,
    color: SoftColors.accent,
    marginBottom: SoftSpacing.xs,
  },
  benefitPreviewMuted: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textMuted,
  },
  benefitList: {
    marginTop: SoftSpacing.xs,
  },
  benefitApplicable: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
    marginBottom: 2,
  },
  benefitBlocked: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textMuted,
    marginBottom: 2,
  },
  benefitPreviewDisclaimer: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.xs,
    fontStyle: 'italic',
  },
  benefitPreviewLoading: {
    width: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.xs,
  },
  benefitPreviewLoadingLabel: {
    marginTop: SoftSpacing.sm,
    textAlign: 'center',
  },
  paymentMethodsSectionWrap: {
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.xs,
  },
  paymentMethodsSectionWrapInvalid: {
    borderWidth: 2,
    borderColor: SoftColors.error,
  },
  paymentMethodRequiredHint: {
    ...SoftTypography.caption,
    color: SoftColors.error,
    marginTop: SoftSpacing.sm,
  },
  paymentMethodsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  paymentMethodsLoading: {
    width: '100%',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.md,
  },
  paymentMethodsLoadingLabel: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    marginTop: SoftSpacing.sm,
  },
  paymentMethod: {
    alignItems: 'center',
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    borderWidth: 2,
    borderColor: SoftColors.borderLight,
    minWidth: 80,
  },
  selectedPaymentMethod: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accentLight,
  },
  paymentMethodText: {
    marginTop: SoftSpacing.xs,
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
  },
  selectedPaymentMethodText: {
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  paymentMethodDisabled: {
    opacity: 0.45,
  },
  registerHardStopBanner: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.md,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.errorBg,
    borderWidth: 1,
    borderColor: 'rgba(220, 38, 38, 0.35)',
  },
  registerHardStopBannerText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.error,
    textAlign: 'center',
  },
  startbelegBlockBackdrop: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.lg,
  },
  startbelegBlockCard: {
    width: '100%',
    maxWidth: 420,
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    padding: SoftSpacing.lg,
    ...SoftShadows.md,
  },
  startbelegBlockTitle: {
    ...SoftTypography.h2,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
    textAlign: 'center',
  },
  startbelegBlockBody: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.lg,
    textAlign: 'center',
  },
  startbelegBlockBtn: {
    alignSelf: 'center',
    minWidth: 200,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
  },
  startbelegBlockBtnText: {
    ...SoftTypography.label,
    fontWeight: '600',
    color: SoftColors.textInverse,
  },
  inputRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  cashLabelWithHint: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    flexShrink: 1,
  },
  cashAmountWarnIcon: {
    fontSize: 14,
    lineHeight: 18,
  },
  label: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
  },
  amountInput: {
    borderWidth: 2,
    borderColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    ...SoftTypography.priceTotal,
    color: SoftColors.textPrimary,
    textAlign: 'right',
    minWidth: 120,
  },
  changeRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: SoftSpacing.sm,
    marginTop: SoftSpacing.xs,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
  },
  changeLabel: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
  },
  changeAmount: {
    ...SoftTypography.priceTotal,
    color: SoftColors.success,
  },
  notesInput: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    ...SoftTypography.bodySmall,
    minHeight: 64,
    textAlignVertical: 'top',
  },
  errorContainer: {
    backgroundColor: SoftColors.errorBg,
    padding: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    marginTop: SoftSpacing.sm,
  },
  errorText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.error,
  },
  errorBlock: {
    width: '100%',
    alignItems: 'center',
  },
  retryLink: {
    marginTop: SoftSpacing.sm,
    padding: SoftSpacing.sm,
  },
  retryLinkText: {
    ...SoftTypography.label,
    color: SoftColors.accent,
  },
  footer: {
    flexDirection: 'column',
    gap: SoftSpacing.sm,
    padding: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgCard,
  },
  timeSyncCriticalBanner: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: '#b71c1c',
    borderWidth: 1,
    borderColor: '#7f0000',
    gap: SoftSpacing.sm,
  },
  timeSyncCriticalText: {
    ...SoftTypography.caption,
    color: SoftColors.textInverse,
    textAlign: 'center',
    fontWeight: '700',
  },
  timeSyncAdminButton: {
    alignSelf: 'center',
    backgroundColor: SoftColors.textInverse,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.lg,
    borderRadius: SoftRadius.md,
  },
  timeSyncAdminButtonText: {
    ...SoftTypography.label,
    color: '#b71c1c',
    fontWeight: '700',
  },
  operatorWarnBanner: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: 'rgba(255, 193, 7, 0.22)',
    borderWidth: 1,
    borderColor: 'rgba(245, 124, 0, 0.45)',
  },
  operatorWarnBannerText: {
    ...SoftTypography.caption,
    color: SoftColors.textPrimary,
    textAlign: 'center',
  },
  footerButtonRow: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    alignItems: 'stretch',
  },
  footerBlockedHint: {
    ...SoftTypography.caption,
    color: SoftColors.error,
    textAlign: 'center',
    paddingHorizontal: SoftSpacing.sm,
  },
  cancelButton: {
    flex: 1,
    minHeight: 48,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.border,
    backgroundColor: SoftColors.bgSecondary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cancelButtonText: {
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  stornoRefundSideBtn: {
    flex: 1.15,
    minHeight: 48,
    paddingVertical: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    borderWidth: 1.5,
    borderColor: '#c62828',
    backgroundColor: SoftColors.bgCard,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stornoRefundSideBtnText: {
    ...SoftTypography.caption,
    fontWeight: '700',
    color: '#c62828',
    textAlign: 'center',
  },
  payButton: {
    flex: 2,
    minHeight: 48,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
    justifyContent: 'center',
    ...SoftShadows.sm,
  },
  payButtonWhenStornoPresent: {
    flex: 1.55,
  },
  payButtonDisabled: {
    // Darker than textMuted so white label stays readable (disabled Zahlen was washing out on web).
    backgroundColor: SoftColors.textSecondary,
    opacity: 0.85,
  },
  payButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.sm,
  },
  payButtonText: {
    ...SoftTypography.body,
    fontWeight: '700',
    color: SoftColors.textInverse,
  },
  loadingText: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    paddingVertical: SoftSpacing.sm,
  },
  presetsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  presetButton: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.md,
    borderWidth: 2,
    borderColor: SoftColors.borderLight,
    minWidth: 72,
    alignItems: 'center',
    flex: 1,
  },
  presetButtonSelected: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accentLight,
  },
  presetButtonText: {
    ...SoftTypography.priceSmall,
    color: SoftColors.textSecondary,
  },
  presetButtonTextSelected: {
    color: SoftColors.accentDark,
    fontWeight: '700',
  },
  voucherFieldLabel: {
    ...SoftTypography.label,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  voucherCodeInput: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.sm,
  },
  voucherCheckButton: {
    alignSelf: 'flex-start',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    borderWidth: 2,
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accentLight,
    marginBottom: SoftSpacing.sm,
  },
  voucherCheckButtonDisabled: {
    opacity: 0.6,
  },
  voucherCheckButtonInner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.sm,
  },
  voucherCheckButtonText: {
    ...SoftTypography.label,
    color: SoftColors.accentDark,
    fontWeight: '600',
  },
  voucherInfoBlock: {
    marginBottom: SoftSpacing.sm,
    gap: 4,
  },
  voucherInfoLine: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textPrimary,
  },
  voucherMuted: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  voucherInlineError: {
    ...SoftTypography.bodySmall,
    color: SoftColors.error,
    marginTop: SoftSpacing.xs,
  },
  footerSecondary: {
    flexDirection: 'column',
    alignItems: 'center',
    padding: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgCard,
  },
  statusBlock: {
    alignItems: 'center',
    justifyContent: 'center',
    padding: SoftSpacing.lg,
    width: '100%',
  },
  statusText: {
    marginTop: SoftSpacing.sm,
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
  },
  successTitle: {
    marginTop: SoftSpacing.sm,
    ...SoftTypography.h3,
    color: SoftColors.success,
  },
  successActionsRow: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    alignItems: 'stretch',
    marginTop: SoftSpacing.md,
    width: '100%',
    paddingHorizontal: SoftSpacing.xs,
  },
  successSecondaryBtn: {
    flex: 1,
    minHeight: 48,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    borderWidth: 1,
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.bgCard,
    alignItems: 'center',
    justifyContent: 'center',
  },
  successSecondaryBtnText: {
    ...SoftTypography.body,
    fontWeight: '600',
    color: SoftColors.accent,
  },
  successPrimaryBtn: {
    flex: 1,
    minHeight: 48,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
    alignItems: 'center',
    justifyContent: 'center',
    ...SoftShadows.sm,
  },
  printErrorBlock: {
    width: '100%',
  },
  printErrorTitle: {
    textAlign: 'center',
    ...SoftTypography.body,
    color: SoftColors.error,
    marginBottom: SoftSpacing.sm,
    fontWeight: '600',
  },
  receiptPreview: {
    width: '100%',
    marginBottom: SoftSpacing.sm,
    maxHeight: 280,
  },
  printErrorActions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
    justifyContent: 'center',
    marginTop: SoftSpacing.sm,
  },
  printErrorBtnSecondary: {
    flex: 1,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.errorBg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  printErrorBtnSecondaryText: {
    ...SoftTypography.body,
    color: SoftColors.error,
  },
  registerBanner: {
    marginHorizontal: SoftSpacing.md,
    marginBottom: SoftSpacing.md,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.warningBg,
    borderWidth: 1,
    borderColor: 'rgba(234, 179, 8, 0.35)',
  },
  registerBannerTitle: {
    ...SoftTypography.label,
    fontWeight: '600',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  registerBannerText: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.sm,
  },
  registerBannerMuted: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    fontStyle: 'italic',
  },
  registerChipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  registerChip: {
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.full,
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.borderLight,
    maxWidth: '100%',
  },
  registerChipDisabled: {
    opacity: 0.6,
  },
  registerChipText: {
    ...SoftTypography.label,
    color: SoftColors.accent,
  },
}); 
