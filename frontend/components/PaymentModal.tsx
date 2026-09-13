import { Ionicons } from '@expo/vector-icons';
import type { TFunction } from 'i18next';
import React, { useState, useEffect, useMemo, useRef, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ScrollView,
  Alert,
  TextInput,
  Pressable,
  Platform,
  Switch,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  paymentService,
  PaymentRequest,
  PaymentItem,
  type VoucherValidateSuccess,
  resolveGatewayChargeAmount,
} from '../services/api/paymentService';
import {
  isPaymentError,
  getPaymentErrorDisplayMessage,
  getPaymentResponseFailureMessage,
} from '../features/payment/paymentErrors';
import { cartService } from '../services/api/cartService';
import { getPreorderByReceipt } from '../services/api/preorderService';
import {
  customerService,
  isWalkInCustomerId,
  type BenefitEligibilityPreviewResponse,
} from '../services/api/customerService';
import { WALK_IN_CUSTOMER_ID_FALLBACK } from '../constants/walkInCustomer';
import { validateAmount } from '../utils/validation';
import {
  PAYMENT_COVERAGE_TOLERANCE_EUR,
  computeVoucherPlusCashCoversTotal,
  resolveOptionalCashTender,
} from '../utils/posPaymentCoverage';
import { usePosCashRegisterAssignment } from '../hooks/usePosCashRegisterAssignment';
import {
  DEFAULT_POS_PAYMENT_METHOD,
  posCheckoutUiActions,
  selectPaymentMethodSubmitAttempted,
  selectSelectedPaymentMethodType,
  usePosCheckoutUiStore,
} from '../stores/posCheckoutUiStore';
import {
  onlinePaymentStoreActions,
  selectOnlinePaymentPhase,
  useOnlinePaymentStore,
} from '../stores/onlinePaymentStore';
import {
  isHostedOnlinePaymentMethod,
  isOnlinePaymentDisabledOffline,
  mergeHostedOnlinePaymentMethods,
  type HostedOnlinePaymentCode,
} from '../services/payment/onlinePaymentMethods';
import {
  OnlinePaymentFlowError,
  runHostedOnlinePayment,
} from '../services/payment/onlinePaymentFlow';
import { logger } from '../lib/logger';
import { receiptPrinter } from '../services/receiptPrinter';
import {
  POS_RECEIPT_REPRINT_REASONS,
  reprintReceipt,
} from '../services/api/receiptService';
import { VoucherScanner } from './VoucherScanner';
import { PaymentSuccessQr } from './PaymentSuccessQr';
import CardPaymentModal from './CardPaymentModal';
import { ReceiptSummary, type ReceiptSummaryReceipt } from './ReceiptSummary';
import type { PaymentTseInfo } from '../services/api/paymentService';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import { normalizeReceiptDto, resolveReceiptNetAmount } from '../utils/normalizeReceiptDto';
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
import { useTseHealth } from '../hooks/useTseHealth';
import { shouldRequireTseForPosPayment } from '../utils/shouldRequireTseForPosPayment';
import { TseStatusIndicator } from './TseStatusIndicator';
import { WaveLoader } from '../src/components/common/WaveLoader';
import StornoRefundSelection from './StornoRefundSelection';
import {
  SoftColors,
  SoftRadius,
  SoftShadows,
  SoftSpacing,
  SoftState,
  SoftTypography,
} from '../constants/SoftTheme';
import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import {
  POS_LARGE_CASH_WARN_THRESHOLD_EUR,
  POS_TSE_STATUS_FAILURE_WARN_STREAK,
  registerPosTseStatusCheckOutcome,
} from '../constants/posOperatorWarnings';
import { usePosPermissions } from '../hooks/usePosPermissions';
import { canShowPosStornoRefundButton } from '../utils/posStornoRefundGate';
import { POS_TIME_SYNC_ADMIN_CONTACT_MESSAGE_DE } from '../constants/posTimeSyncContact';
import { useAuth } from '../contexts/AuthContext';
import { useSystem } from '../contexts/SystemContext';
import { useLicenseStatus } from '../hooks/useLicenseStatus';
import { useMaintenance } from '../contexts/MaintenanceContext';
import { usePayment } from '../hooks/usePayment';
import { useTimeSyncStatus } from '../hooks/useTimeSyncStatus';
import { checkLicenseBeforePayment } from '../utils/checkLicenseBeforePayment';
import { formatUserDate, formatUserDateTime } from '../utils/dateFormatter';
import { isPrintCancelled } from '../utils/expoPrintShare';
import { formatPrice } from '../utils/formatPrice';
import {
  areLicenseChecksBypassedInDevelopment,
  isLicenseExpiredForCriticalActions,
  isTrialLikeLicenseStatus,
} from '../utils/licenseCriticalActionGuard';

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

  if (
    code === 'QuantityNotReached' &&
    typeof b?.requiredMoreQuantity === 'number' &&
    b.requiredMoreQuantity > 0
  ) {
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


/** ReceiptDTO veya payment response'taki receipt ? ReceiptSummary format?. */
function toSummaryReceipt(receipt: ReceiptDTO | null): ReceiptSummaryReceipt | null {
  if (!receipt?.items?.length) return null;
  const items = receipt.items.map((i) => ({
    name: i.name,
    quantity: i.quantity,
    lineTotalGross: i.lineTotalGross ?? i.totalPrice ?? 0,
    isModifier: i.isModifierLine ?? false,
  }));
  const totals = {
    totalNet: resolveReceiptNetAmount({
      netTotal: receipt.netTotal,
      totalNet: receipt.totals?.totalNet,
      subtotal: receipt.subtotal,
      grandTotal: receipt.grandTotal,
      taxAmount: receipt.taxAmount ?? receipt.totals?.totalVat,
    }),
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

// Backend cevab?n? ReceiptDTO'ya normalize et (PascalCase/camelCase uyumu)
function normalizeReceiptDtoFromApi(r: unknown): ReceiptDTO {
  return normalizeReceiptDto(r);
}

// T�rk�e A�?klama: �deme alma modal'? - Sepet i�eri?ini �deme i?lemine d�n�?t�r�r
interface PaymentModalProps {
  visible: boolean;
  onClose: () => void;
  /** Called after payment and print; pass tableNumber so caller can clear the paid table. */
  onSuccess: (paymentId: string, tableNumber?: number) => void | Promise<void>;
  /** Optional toast sink (tab bar) for server offline-queue messages. */
  onPosToast?: (payload: { type?: 'success' | 'warning' | 'info'; message: string }) => void;
  cartItems: {
    id: string;
    productId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
    taxType?: string | number;
    /** Extra Zutaten � �deme/fi? i�in backend'e g�nderilir (modifierId zorunlu; name/priceDelta opsiyonel) */
    modifiers?: { modifierId: string; name?: string; priceDelta?: number }[];
  }[];
  /** Backend'den gelen br�t toplam - FE hesaplama yapmaz */
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
  customerId = '00000000-0000-0000-0000-000000000000', // Default Guid format?nda
  tableNumber,
  onPosToast,
}: PaymentModalProps) {
  const { t, i18n } = useTranslation(['checkout', 'common', 'invoices', 'settings', 'system', 'receipts']);
  const { t: tLicense } = useTranslation('license');
  const { user } = useAuth();
  const { canMakePayment, canReprintReceipt } = usePosPermissions();
  const { status: licenseSnapshot } = useLicenseStatus();
  const { isBlocking: maintenanceBlocksPayment } = useMaintenance();
  const showStornoRefundEntry = canShowPosStornoRefundButton(user);
  const { refetch: refetchTimeSync, timeSyncCritical, timeSyncWarningBand } = useTimeSyncStatus();
  const { isOnline } = useSystem();
  const tseHealth = useTseHealth();
  const tseServerOffline = String(tseHealth.status) === 'Offline';
  const shouldRequireTse = shouldRequireTseForPosPayment(tseHealth.requiresFiscalSignature);
  const selectedPaymentMethod = usePosCheckoutUiStore(selectSelectedPaymentMethodType);
  const onlinePaymentPhase = useOnlinePaymentStore(selectOnlinePaymentPhase);
  const paymentMethodSubmitAttempted = usePosCheckoutUiStore(selectPaymentMethodSubmitAttempted);
  const { setSelectedPaymentMethodType, setPaymentMethodSubmitAttempted, resetCheckoutPaymentUi } =
    posCheckoutUiActions;
  const [amountReceived, setAmountReceived] = useState<string>('');
  /** Cash (Bar): false only when a typed tender is present and below the rest amount. */
  const [isAmountValid, setIsAmountValid] = useState(true);
  const [notes, setNotes] = useState<string>('');
  const [isPreorder, setIsPreorder] = useState(false);
  const [preorderRemainingText, setPreorderRemainingText] = useState('');
  const [balanceQuery, setBalanceQuery] = useState('');
  const [balanceOrder, setBalanceOrder] = useState<{
    id: string;
    preorderNumber?: string | null;
    remainingAmount?: number;
  } | null>(null);
  const [balanceLookupError, setBalanceLookupError] = useState<string | null>(null);
  const [guestCustomerId, setGuestCustomerId] = useState<string>(WALK_IN_CUSTOMER_ID_FALLBACK);
  // State for Purchase Flow
  type PurchaseState = 'input' | 'processing' | 'printing' | 'completed' | 'print_error';
  const [purchaseState, setPurchaseState] = useState<PurchaseState>('input');

  // Store paymentId and TSE/QR bilgisi for success ekran? ve retry
  const [completedPaymentId, setCompletedPaymentId] = useState<string | null>(null);
  const [completedPaymentTse, setCompletedPaymentTse] = useState<PaymentTseInfo | null>(null);
  /** Receipt payload for summary � GET /api/pos/payment/{id}/receipt */
  const [receiptData, setReceiptData] = useState<ReceiptDTO | null>(null);
  /** Prevents double-submit during async work before purchaseState becomes 'processing'. */
  const [cardSimVisible, setCardSimVisible] = useState(false);
  const [cardPaymentIntentId, setCardPaymentIntentId] = useState<string | undefined>();
  const [paymentBusy, setPaymentBusy] = useState(false);
  const onlinePaymentAbortRef = useRef<AbortController | null>(null);

  /** RKSV: separate wizard for Storno vs Teilr�ckerstattung (elevated permissions). */
  const [stornoRefundWizardVisible, setStornoRefundWizardVisible] = useState(false);

  /** Gutschein: code, validate snapshot, redeem amount (must match fiscal total for single-code flow). */
  const [voucherCode, setVoucherCode] = useState('');
  const [voucherScannerVisible, setVoucherScannerVisible] = useState(false);
  const [voucherEnabled, setVoucherEnabled] = useState(false);
  /** Gutschein: true when code has non-whitespace (inline ?? when false while method is voucher). */
  const [isVoucherCodeValid, setIsVoucherCodeValid] = useState(true);
  const [voucherRedeemAmountStr, setVoucherRedeemAmountStr] = useState('');
  /** Writes `voucherRedeemAmountStr`; effective EUR is derived via `useMemo` below (empty ? 0). */
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
  const [eligibilityPreview, setEligibilityPreview] =
    useState<BenefitEligibilityPreviewResponse | null>(null);
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
    const needTse = shouldRequireTse;
    if (!needTse) {
      setFiscalTseGateOk(true);
      const streak = registerPosTseStatusCheckOutcome(true);
      setTseCheckFailureStreak(streak);
      return;
    }
    if (tseHealth.loading && !tseHealth.lastCheck) {
      setFiscalTseGateOk(null);
      return;
    }
    const indicator = String(tseHealth.indicatorStatus);
    const ok = indicator === 'Active' || indicator === 'Degraded';
    setFiscalTseGateOk(ok);
    const streak = registerPosTseStatusCheckOutcome(ok);
    setTseCheckFailureStreak(streak);
  }, [visible, shouldRequireTse, tseHealth.indicatorStatus, tseHealth.loading, tseHealth.lastCheck]);

  const {
    methodsLoading,
    error,
    paymentMethods,
    getPaymentMethods,
    validateVoucher,
    processPayment,
    clearError,
  } = usePayment(cashRegisterId);

  const requiresCashAmount = useMemo(() => {
    if (!selectedPaymentMethod) return false;
    const m = paymentMethods.find((x) => x.type === selectedPaymentMethod);
    if (m?.requiresReceivedAmount !== undefined) return m.requiresReceivedAmount;
    return selectedPaymentMethod === 'cash';
  }, [paymentMethods, selectedPaymentMethod]);

  const settlementPaymentMethods = useMemo(() => {
    let list = mergeHostedOnlinePaymentMethods(paymentMethods ?? []).filter(
      (m) => m.type !== 'voucher'
    );
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
    if (!voucherEnabled) {
      setIsVoucherCodeValid(true);
      return;
    }
    setIsVoucherCodeValid(voucherCode.trim().length > 0);
  }, [voucherEnabled, voucherCode]);

  // Backend line toplamlar? kullan - FE hesaplama yapmaz (totalPrice = lineGross)
  const calculatedCartItems = useMemo(() => {
    return cartItems.map((item) => ({
      ...item,
      lineTotal: item.totalPrice ?? item.quantity * item.unitPrice,
    }));
  }, [cartItems]);

  const cartLineSumGross = calculatedCartItems.reduce((sum, item) => sum + item.lineTotal, 0);
  /**
   * Prefer backend `grandTotalGross` when > 0. When it is 0, treat as true �0 cart only if line gross is also ~0;
   * otherwise assume missing/default 0 from UI helpers and use line sum (matches legacy `> 0 ? gross : lines`).
   */
  const totalAmount = (() => {
    if (grandTotalGross != null && Number.isFinite(grandTotalGross)) {
      if (grandTotalGross > 0) return grandTotalGross;
      if (grandTotalGross === 0 && cartLineSumGross <= PAYMENT_COVERAGE_TOLERANCE_EUR) return 0;
      if (grandTotalGross === 0 && cartLineSumGross > PAYMENT_COVERAGE_TOLERANCE_EUR)
        return cartLineSumGross;
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
  const voucherRequestedAmount = Number.isFinite(voucherRedeemParsed)
    ? Math.max(0, voucherRedeemParsed)
    : 0;
  const effectiveVoucherRedeemCap = voucherSnapshot
    ? effectiveVoucherRedeemCapFromSnapshot(voucherSnapshot)
    : 0;
  const voucherMaxForSale = computeVoucherMaxForSale(totalAmount, voucherSnapshot, voucherEnabled);

  /** Clamped EUR for POST / UI; empty redeem field ? 0 (NaN-safe). Recalculates on every relevant change�same render as Restbetrag. */
  const voucherRedeemAmountEffective = useMemo(() => {
    if (
      !voucherEnabled ||
      !voucherSnapshot ||
      validatedVoucherCode?.trim() !== voucherCode.trim()
    ) {
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
  const gatewayChargeAmount = resolveGatewayChargeAmount({
    cartTotal: totalAmount,
    remainderAfterVoucher: voucherEnabled ? settlementAmountDue : undefined,
  });
  const shouldCollectCashAmount = requiresCashAmount && settlementAmountDue > 0;
  const cashTender = resolveOptionalCashTender(amountReceived, settlementAmountDue);
  const changeAmount = cashTender.fieldEmpty ? null : cashTender.parsed - settlementAmountDue;
  const showCashChange = changeAmount != null && Number.isFinite(changeAmount) && changeAmount >= 0;

  useEffect(() => {
    if (!requiresCashAmount) {
      setIsAmountValid(true);
      return;
    }
    if (cashTender.fieldEmpty) {
      setIsAmountValid(true);
      return;
    }
    setIsAmountValid(!cashTender.isInsufficient);
  }, [requiresCashAmount, cashTender.fieldEmpty, cashTender.isInsufficient]);

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
   * Near-zero cart: Pr�fen success + matching code is enough (no redeem EUR line). Positive carts: unchanged redeem rules.
   * Full voucher coverage fix: when maxForThisCart is 0 the redeem field stays empty but settlement must still validate if cart total is ~�0.
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

  const needFiscalTseForPay = shouldRequireTse;
  /** When backend health is Offline, cash-only queue is allowed � do not block on local device probe. */
  const payGateTseBlocked = needFiscalTseForPay && !tseServerOffline && fiscalTseGateOk !== true;
  const offlineBlocksVoucher =
    (!isOnline && voucherEnabled) || (tseServerOffline && voucherEnabled);
  const offlineBlocksOnlinePayment = isOnlinePaymentDisabledOffline(
    selectedPaymentMethod,
    isOnline
  );

  const paymentCoverageOk =
    (totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR && voucherEnabled && voucherSettlementValid) || // FIX: full voucher coverage
    mixedCoverage.coversTotal;

  const showPayTrialWarningIcon = useMemo(() => {
    if (areLicenseChecksBypassedInDevelopment()) return false;
    if (!licenseSnapshot) return false;
    return (
      isTrialLikeLicenseStatus(licenseSnapshot) &&
      !isLicenseExpiredForCriticalActions(licenseSnapshot)
    );
  }, [licenseSnapshot]);

  const licenseBlocksPaymentUi = Boolean(
    !areLicenseChecksBypassedInDevelopment() &&
    licenseSnapshot &&
    isLicenseExpiredForCriticalActions(licenseSnapshot)
  );

  const paySubmitDisabled =
    !canMakePayment ||
    purchaseState === 'processing' ||
    paymentBusy ||
    methodsLoading ||
    !hasValidSettlementMethod ||
    isRegisterGateBlockingPayment ||
    payGateTseBlocked ||
    offlineBlocksVoucher ||
    !paymentCoverageOk ||
    (voucherEnabled && !voucherSettlementValid) ||
    timeSyncCritical ||
    licenseBlocksPaymentUi ||
    maintenanceBlocksPayment ||
    offlineBlocksOnlinePayment;

  const showPayWorking = purchaseState === 'processing' || paymentBusy;

  const paySubmitBlockedHint =
    paySubmitDisabled && !showPayWorking
      ? !canMakePayment
        ? t('checkout:posFlow.payment.blockedHints.noPaymentPermission')
        : maintenanceBlocksPayment
          ? t('system:maintenanceNotice.paymentBlocked')
          : licenseBlocksPaymentUi
            ? tLicense('criticalGuard.expiredBody')
            : timeSyncCritical
              ? t('checkout:posFlow.payment.blockedHints.timeSyncCritical')
              : methodsLoading
                ? t('checkout:posFlow.payment.blockedHints.methodsLoading')
                : !hasValidSettlementMethod
                  ? t('checkout:posFlow.payment.blockedHints.selectMethod')
                  : !paymentCoverageOk
                    ? t('checkout:posFlow.payment.blockedHints.coverageMissing')
                    : voucherEnabled && !voucherSettlementValid
                      ? t('checkout:posFlow.payment.blockedHints.voucherInvalid')
                      : isRegisterGateBlockingPayment
                        ? registerGateFooterHint(registerGateCtx)
                        : payGateTseBlocked
                          ? t('checkout:posFlow.payment.blockedHints.tseNotReady')
                          : offlineBlocksVoucher
                            ? t('checkout:posFlow.payment.blockedHints.voucherOffline')
                            : offlineBlocksOnlinePayment
                              ? t('checkout:posFlow.payment.blockedHints.onlinePaymentOffline')
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
      canMakePayment,
      purchaseState,
      paymentBusy,
      cashRegisterResolved,
      hasValidCashRegisterId,
      isRegisterGateBlockingPayment,
    });
  }, [
    visible,
    paySubmitDisabled,
    canMakePayment,
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
      debugPosPaymentTrace('payment_modal_cleanup_visible_false_or_unmount', {
        platform: Platform.OS,
      });
    };
  }, [visible]);

  // Load payment methods and guest customer when modal opens (cash register: usePosCashRegisterAssignment)
  useEffect(() => {
    if (!visible) return;
    if (cashRegisterId?.trim()) {
      getPaymentMethods();
    }
    customerService
      .getGuestCustomer()
      .then((id) => {
        setGuestCustomerId(id);
      })
      .catch((err) => {
        console.warn('[PaymentModal] Failed to load guest customer:', err);
      });
  }, [visible, cashRegisterId, getPaymentMethods]);

  useEffect(() => {
    if (!visible) {
      resetCheckoutPaymentUi();
      setVoucherEnabled(false);
    }
  }, [visible, resetCheckoutPaymentUi]);

  useEffect(() => {
    if (!visible || settlementPaymentMethods.length === 0) return;
    const selectedIsAvailable =
      !!selectedPaymentMethod &&
      settlementPaymentMethods.some((m) => m.type === selectedPaymentMethod);
    if (selectedIsAvailable) return;
    const cash = settlementPaymentMethods.find((m) => m.type === DEFAULT_POS_PAYMENT_METHOD);
    const catalogDefault = settlementPaymentMethods.find((m) => m.isDefault);
    setSelectedPaymentMethodType(
      cash?.type ?? catalogDefault?.type ?? settlementPaymentMethods[0].type
    );
  }, [visible, settlementPaymentMethods, selectedPaymentMethod, setSelectedPaymentMethodType]);

  useEffect(() => {
    if (selectedPaymentMethod === 'voucher') {
      setSelectedPaymentMethodType(null);
    }
  }, [selectedPaymentMethod, setSelectedPaymentMethodType]);

  /** Cart total changed after voucher validation � require a new check (keep typed code). */
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
      cartItems
        .map((i) => `${i.productId}:${(i as any).qty ?? i.quantity}`)
        .sort()
        .join(','),
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
      .getBenefitEligibilityPreview(customerId, items)
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

  // �deme ba?ar?l? olunca fi? verisini �ek (ReceiptSummary i�in)
  useEffect(() => {
    if (!completedPaymentId) {
      setReceiptData(null);
      return;
    }
    paymentService
      .getReceipt(completedPaymentId)
      .then((raw: any) => {
        const r = raw?.data ?? raw?.Value ?? raw;
        if (r && Array.isArray(r.items)) {
          setReceiptData(normalizeReceiptDtoFromApi(r));
        }
      })
      .catch((err) => {
        console.warn('[PaymentModal] Receipt fetch failed:', err);
        setReceiptData(null);
      });
  }, [completedPaymentId]);

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

  /** POST /api/pos/payment � exhaustive logs + Debug Error on every guard exit (operator confirmations log only). */
  const executePaymentSubmission = async (confirmedCardIntentId?: string) => {
    const logPay = (step: string, detail?: Record<string, unknown>) => {
      debugPosPaymentTrace(step, detail);
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
      Alert.alert('Debug Error', 'Failed at: Gutschein offline nicht m�glich');
      return;
    }

    if (offlineBlocksOnlinePayment) {
      logPay('Guard exit: offline blocks online payment');
      Alert.alert(
        t('checkout:posFlow.payment.onlinePayment.offlineAlertTitle'),
        t('checkout:posFlow.payment.onlinePayment.offlineDisabled')
      );
      return;
    }

    if (
      !selectedPaymentMethod ||
      !settlementPaymentMethods.some((m) => m.type === selectedPaymentMethod)
    ) {
      setPaymentMethodSubmitAttempted(true);
      debugPosPaymentTrace('submit_blocked_no_payment_method', {});
      logPay('Guard exit: no settlement method');
      Alert.alert(
        'Debug Error',
        'Failed at: keine g�ltige Zahlungsart (Schritt 2; nicht nur �Gutschein� als Methodentyp)'
      );
      return;
    }

    if (needFiscalTseForPay && payGateTseBlocked) {
      logPay('Guard exit: TSE gate');
      Alert.alert(
        t('checkout:posFlow.payment.alerts.errorTitle'),
        resolveTseGateMessage()
      );
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
        `Failed at: Deckung � Gutschein+Bar=${submitCoverage.sumPaid.toFixed(2)} �, Gesamt=${totalAmount.toFixed(2)} �`
      );
      return;
    }

    if (voucherEnabled && !voucherSettlementValid) {
      logPay('Guard exit: voucher settlement invalid');
      Alert.alert(
        'Debug Error',
        'Failed at: Gutschein nicht g�ltig / nicht eingel�st (Pr�fen + Betrag)'
      );
      return;
    }

    if (shouldCollectCashAmount && !cashTender.fieldEmpty && cashTender.isInsufficient) {
      debugPosPaymentTrace('submit_blocked_cash_amount', {
        received: cashTender.parsed,
        settlementAmountDue,
      });
      logPay('Guard exit: cash below Restbetrag');
      Alert.alert(
        t('checkout:posFlow.payment.cash.belowTotalTitle'),
        t('checkout:posFlow.payment.cash.belowTotalMessage', {
          amount: formatPrice(settlementAmountDue),
        })
      );
      return;
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
        `Failed at: Kasse nicht bereit � ${registerGateAlertMessage(registerGateCtx)}`
      );
      return;
    }

    // FIX: full voucher coverage
    const allowZeroTotalWithValidatedVoucher =
      voucherEnabled && voucherSettlementValid && totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR;

    if (!validateAmount(totalAmount) && !allowZeroTotalWithValidatedVoucher) {
      debugPosPaymentTrace('submit_blocked_invalid_amount', { totalAmount });
      logPay('Guard exit: validateAmount totalAmount');
      Alert.alert('Debug Error', `Failed at: ung�ltiger Gesamtbetrag (${String(totalAmount)})`);
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
    if (voucherEnabled && voucherSnapshot && totalAmount - voucherRedeemAmountEffective <= 0.02) {
      debugPosPaymentTrace('voucher_full_coverage_no_dialog', {
        totalAmount,
        voucherRedeemAmountEffective,
      });
    }

    const effectiveCardIntentId = confirmedCardIntentId ?? cardPaymentIntentId;
    const hostedOnlineSelected = isHostedOnlinePaymentMethod(selectedPaymentMethod);
    if (hostedOnlineSelected && !isOnline) {
      Alert.alert(
        t('checkout:posFlow.payment.onlinePayment.offlineAlertTitle'),
        t('checkout:posFlow.payment.onlinePayment.offlineDisabled')
      );
      return;
    }
    if (!hostedOnlineSelected && selectedPaymentMethod === 'card' && !effectiveCardIntentId) {
      setCardSimVisible(true);
      return;
    }

    setPaymentBusy(true);
    try {
      logPay('Step 4: Payload construction (before cart id)');
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(resolvedTableNumber);

        if (!cart?.cartId) {
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
          `Failed at: Warenkorb laden � ${cartErr instanceof Error ? cartErr.message : String(cartErr)}`
        );
        return;
      }

      // 3. Determine customer ID (use guest if walk-in)
      const finalCustomerId =
        customerId && customerId !== '00000000-0000-0000-0000-000000000000'
          ? customerId
          : guestCustomerId;

      let hostedOnlinePaymentId: string | undefined;
      if (hostedOnlineSelected && isHostedOnlinePaymentMethod(selectedPaymentMethod)) {
        const methodCode = selectedPaymentMethod as HostedOnlinePaymentCode;
        logger.info('online_payment.modal_flow_start', { method: methodCode });
        onlinePaymentAbortRef.current?.abort();
        const abort = new AbortController();
        onlinePaymentAbortRef.current = abort;
        try {
          const gateway = await runHostedOnlinePayment({
            method: methodCode,
            cashRegisterId,
            amount: gatewayChargeAmount,
            customerId: finalCustomerId,
            tableNumber: resolvedTableNumber,
            signal: abort.signal,
          });
          hostedOnlinePaymentId = gateway.onlinePaymentId;
        } catch (gatewayErr) {
          if (gatewayErr instanceof Error && gatewayErr.message === 'ONLINE_PAYMENT_ABORTED') {
            logger.info('online_payment.modal_flow_aborted', { method: methodCode });
            setPurchaseState('input');
            return;
          }
          const code =
            gatewayErr instanceof OnlinePaymentFlowError
              ? gatewayErr.code
              : 'ONLINE_PAYMENT_FAILED';
          logger.warn('online_payment.modal_flow_failed', { method: methodCode, errorCode: code });
          const message =
            code === 'ONLINE_PAYMENT_CANCELLED'
              ? t('checkout:posFlow.payment.onlinePayment.cancelled')
              : code === 'ONLINE_PAYMENT_TIMEOUT'
                ? t('checkout:posFlow.payment.onlinePayment.timeout')
                : code === 'HOSTED_PAGE_OPEN_FAILED'
                  ? t('checkout:posFlow.payment.onlinePayment.openFailed')
                  : t('checkout:posFlow.payment.onlinePayment.failed');
          Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), message);
          setPurchaseState('input');
          onlinePaymentStoreActions.reset();
          return;
        }
      }

      // 4. Build payment request: flat items (one PaymentItem per cart line). Phase D: no modifierIds emission; add-ons = product lines only.
      // Guard: flat items only � do not add modifierIds or modifiers (one item per cart line).
      // taxType: paymentService.processPayment normalizes all items before POST / queue
      const paymentItems: PaymentItem[] = cartItems.map((item) => ({
        productId: item.productId,
        quantity: (item as any).qty ?? item.quantity,
        taxType: item.taxType as PaymentItem['taxType'],
      }));

      // One idempotency key per submit; retries with same key return existing payment
      const idempotencyKey =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `${Date.now()}-${Math.random().toString(36).slice(2, 15)}`;

      const settlementPaymentAmountNumeric = voucherEnabled
        ? settlementAmountDue
        : requiresCashAmount
          ? cashTender.effectiveTender
          : undefined;

      const paymentRequest: PaymentRequest = {
        customerId: finalCustomerId, // Always send valid customer ID (guest or registered)
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod,
          tseRequired: shouldRequireTse,
          amount:
            settlementPaymentAmountNumeric != null &&
            Number.isFinite(settlementPaymentAmountNumeric)
              ? settlementPaymentAmountNumeric
              : undefined,
          ...(voucherEnabled
            ? {
                voucherRedemptions: [
                  { code: voucherCode.trim(), amount: voucherRedeemAmountEffective },
                ],
              }
            : {}),
          ...(effectiveCardIntentId ? { cardPaymentIntentId: effectiveCardIntentId } : {}),
          ...(hostedOnlinePaymentId ? { onlinePaymentId: hostedOnlinePaymentId } : {}),
        },
        tableNumber: resolvedTableNumber,
        totalAmount,
        cashRegisterId,
        notes: notes || `Tisch ${resolvedTableNumber} - ${formatUserDateTime(new Date())}`,
        idempotencyKey,
        isPreorder,
        preorderCustomerNotes: isPreorder ? notes || undefined : undefined,
        preorderRemainingAmount: isPreorder
          ? Number(preorderRemainingText.replace(',', '.')) || 0
          : undefined,
        preorderBalanceOrderId:
          !isPreorder && balanceOrder?.id ? balanceOrder.id : undefined,
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

      logPay('Step 5: Calling processPayment ? POST /api/pos/payment', {
        idempotencyKey: paymentRequest.idempotencyKey,
        tseIndicator: tseHealth.indicatorStatus,
        tseEnvironment: tseHealth.environment,
      });

      const response = await processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        debugPosPaymentTrace('payment_non_fiscal_pending_ui', {
          pendingQueueId: response.pendingQueueId,
        });
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
          message: t('checkout:posFlow.payment.offlineQueued'),
        });
        try {
          await cartService.resetCartAfterPayment(currentCartId, 'Payment queued offline (TSE)');
        } catch (resetErr) {
          console.warn('[CART] Reset warning (offline queue):', resetErr);
        }
        const pseudoId = response.paymentId || response.offlineTransactionId || 'offline-queued';
        await Promise.resolve(onSuccess(pseudoId, resolvedTableNumber));
        handleSuccessAndClose(pseudoId);
        return;
      }

      // 6. STRICT: only FISCAL_COMPLETE + paymentId counts as paid sale
      if (!response.success || response.fiscalStatus !== 'FISCAL_COMPLETE' || !response.paymentId) {
        debugPosPaymentTrace('submit_blocked_fiscal_incomplete', {
          success: response.success,
          fiscalStatus: response.fiscalStatus,
          paymentId: response.paymentId || null,
        });
        console.error('[PAYMENT] Failed or not fiscally confirmed:', response);
        const errorMsg = getPaymentResponseFailureMessage(response);
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

      debugPosPaymentTrace('payment_success', {
        paymentId: response.paymentId,
        isDemoFiscal: response.tse?.isDemoFiscal ?? null,
        hasQrPayload: Boolean(response.tse?.qrPayload),
      });
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

      // 7�8. Cart lifecycle: reset-after-payment marks the cart completed, clears lines, and opens a fresh cart.
      // (Skip separate /complete � POST /payment can succeed while the persisted cart has no rows, which would make /complete fail with "empty cart".)
      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
        debugPosPaymentTrace('cart_reset_complete', {});
      } catch (resetErr) {
        console.warn('[CART] Reset warning:', resetErr);
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.errors.completeCartFailed')
        );
      }

      // Clear POS cart as soon as payment + cart lifecycle APIs finished (do not wait for print / Fertig).
      await Promise.resolve(onSuccess(response.paymentId, resolvedTableNumber));

      // 9. START PRINTING (QR from GET /api/pos/payment/{id}/qr.png as base64 embed)
      setPurchaseState('printing');
      try {
        await receiptPrinter.print(response.paymentId);
        setPurchaseState('completed');
      } catch (printErr) {
        // iOS: closing print preview without printing � not a printer fault
        if (isPrintCancelled(printErr)) {
          setPurchaseState('completed');
        } else {
          console.error('[PRINT] Failed:', printErr);
          setPurchaseState('print_error');
          // User will now see "Retry" or "Skip" buttons
        }
      }
    } catch (err) {
      console.error('Handle Payment Error:', err);
      debugPosPaymentTrace('process_payment_or_post_submit_threw', {
        message: err instanceof Error ? err.message : 'unknown',
      });
      setPurchaseState('input');
      const message = getPaymentErrorDisplayMessage(err);
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

  const resolveTseGateMessage = () => {
    const indicator = String(tseHealth.indicatorStatus);
    const message = (tseHealth.message ?? tseHealth.lastErrorMessageSafe ?? '').toLowerCase();
    if (message.includes('disabled') || message.includes('deaktiviert')) {
      return t('checkout:posFlow.payment.tseGate.disabled');
    }
    if (
      indicator === 'Inactive' &&
      (message.includes('initial') || message.includes('no tse') || !tseHealth.scuId)
    ) {
      return t('checkout:posFlow.payment.tseGate.notInitialized');
    }
    return t('checkout:posFlow.payment.tseGate.unavailable');
  };

  const handlePayment = async () => {
    if (!canMakePayment) {
      Alert.alert(
        t('checkout:posFlow.payment.alerts.paymentNotPossibleTitle'),
        t('checkout:posFlow.payment.blockedHints.noPaymentPermission')
      );
      return;
    }
    if (timeSyncCritical) {
      return;
    }
    if (maintenanceBlocksPayment) {
      Alert.alert(
        t('system:maintenanceNotice.modalTitle'),
        t('system:maintenanceNotice.paymentBlocked')
      );
      return;
    }
    const licenseOk = await checkLicenseBeforePayment(tLicense);
    if (!licenseOk) {
      return;
    }
    if (needFiscalTseForPay && !tseServerOffline) {
      try {
        const latest = await tseHealth.refresh();
        const indicator = String(latest?.status ?? tseHealth.indicatorStatus);
        const ok = indicator === 'Active' || indicator === 'Degraded';
        setFiscalTseGateOk(ok);
        registerPosTseStatusCheckOutcome(ok);
        debugPosPaymentTrace('tse_status_before_payment', {
          indicator,
          environment: latest?.environment ?? tseHealth.environment,
          scuId: latest?.scuId ?? tseHealth.scuId,
        });
        if (!ok) {
          Alert.alert(
            t('checkout:posFlow.payment.alerts.errorTitle'),
            t('checkout:posFlow.payment.tseGate.unavailable')
          );
          return;
        }
      } catch {
        setFiscalTseGateOk(false);
        registerPosTseStatusCheckOutcome(false);
        Alert.alert(
          t('checkout:posFlow.payment.alerts.errorTitle'),
          t('checkout:posFlow.payment.tseGate.unavailable')
        );
        return;
      }
    }
    if (timeSyncWarningBand) {
      await new Promise<void>((resolve) => {
        Alert.alert(
          'Zeitabweichung',
          'Zeitabweichung erkannt. Fortfahren trotz m�glicher DEP-Probleme?',
          [
            {
              text: 'Abbrechen',
              style: 'cancel',
              onPress: () => {
                resolve();
              },
            },
            {
              text: 'Fortfahren',
              onPress: () => {
                void executePaymentSubmission();
                resolve();
              },
            },
          ],
          {
            cancelable: true,
            onDismiss: () => {
              resolve();
            },
          }
        );
      });
      return;
    }
    await executePaymentSubmission();
  };

  // Close after success UI; cart is already cleared via onSuccess right after payment settlement.
  const handleSuccessAndClose = (_paymentId: string) => {
    handleClose();
  };

  // Retrying print
  const handleRetryPrint = async () => {
    if (!completedPaymentId) return;
    setPurchaseState('printing');
    try {
      if (canReprintReceipt && cashRegisterId) {
        try {
          await reprintReceipt({
            receiptId: completedPaymentId,
            cashRegisterId,
            reasonCode: POS_RECEIPT_REPRINT_REASONS.PRINTER_FAILURE,
          });
        } catch {
          // Audit/reprint API must not block the local printer retry.
        }
      }
      await receiptPrinter.print(completedPaymentId);
      setPurchaseState('completed');
    } catch (printErr) {
      if (isPrintCancelled(printErr)) {
        setPurchaseState('completed');
      } else {
        console.error('[PRINT] Retry Failed:', printErr);
        setPurchaseState('print_error');
      }
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
    onlinePaymentAbortRef.current?.abort();
    onlinePaymentAbortRef.current = null;
    onlinePaymentStoreActions.reset();
    clearError();
    resetVoucherUi();
    setVoucherEnabled(false);
    resetCheckoutPaymentUi();
    setAmountReceived('');
    setNotes('');
    setIsPreorder(false);
    setPurchaseState('input');
    setCompletedPaymentId(null);
    setCompletedPaymentTse(null);
    setReceiptData(null);
    setPaymentBusy(false);
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
        accessibilityLabel={t('checkout:posFlow.payment.title')}>
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
                accessibilityRole="button">
                <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
              </Pressable>
            </View>

            <ScrollView style={styles.content} keyboardShouldPersistTaps="handled">
              {/* Step 1: Summe */}
              <View style={styles.section}>
                <Text style={styles.stepLabel}>1</Text>
                <Text style={styles.sectionTitle}>{t('checkout:posFlow.payment.steps.sum')}</Text>
                {calculatedCartItems.map((item, index) => (
                  <View key={index} style={styles.cartItem}>
                    <Text style={styles.itemName}>{item.productName}</Text>
                    <Text style={styles.itemDetails}>
                      {item.quantity} � {formatPrice(item.unitPrice)} ={' '}
                      {formatPrice(item.lineTotal)}
                    </Text>
                  </View>
                ))}
                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>{t('checkout:posFlow.payment.totalLabel')}</Text>
                  <Text style={styles.totalAmount}>{formatPrice(totalAmount)}</Text>
                </View>
              </View>

              {registerHardStopDecommissioned ? (
                <View style={styles.registerHardStopBanner} accessibilityRole="alert">
                  <Text style={styles.registerHardStopBannerText}>
                    {POS_DECOMMISSIONED_SALES_BLOCK_MESSAGE_DE}
                  </Text>
                </View>
              ) : null}

              {isRegisterGateBlockingPayment && (
                <View style={styles.registerBanner}>
                  <Text style={styles.registerBannerTitle}>
                    {registerGateBannerTitle(registerGateCtx)}
                  </Text>
                  {!registerListLoading && !posReadinessLoading ? (
                    <Text style={[styles.registerBannerMuted, { marginBottom: SoftSpacing.xs }]}>
                      {t('settings:registerGate.banner.intro')}
                    </Text>
                  ) : null}
                  <Text style={styles.registerBannerText}>
                    {registerGateBannerDetail(registerGateCtx)}
                  </Text>
                  {registerListLoading || posReadinessLoading ? (
                    <View
                      style={{
                        width: '100%',
                        marginVertical: SoftSpacing.sm,
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}>
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
                          accessibilityLabel={t('checkout:posFlow.payment.registerAssignA11y', {
                            register: r.registerNumber || r.id,
                          })}>
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
                      accessibilityLabel={t('checkout:posFlow.payment.retryActions.retrySettings')}>
                      <Text style={styles.retryLinkText}>
                        {t('checkout:posFlow.payment.retryActions.retrySettings')}
                      </Text>
                    </Pressable>
                  ) : posReadinessError ? (
                    <Pressable
                      onPress={() => {
                        refreshPosReadiness();
                      }}
                      style={styles.retryLink}
                      accessibilityRole="button"
                      accessibilityLabel={t(
                        'checkout:posFlow.payment.retryActions.retryReadiness'
                      )}>
                      <Text style={styles.retryLinkText}>
                        {t('checkout:posFlow.payment.retryActions.retryReadiness')}
                      </Text>
                    </Pressable>
                  ) : registerListFailureKind === 'network' ||
                    registerListFailureKind === 'unknown' ? (
                    <Pressable
                      onPress={refetchRegisterList}
                      style={styles.retryLink}
                      accessibilityRole="button"
                      accessibilityLabel={t(
                        'checkout:posFlow.payment.retryActions.reloadRegisterList'
                      )}>
                      <Text style={styles.retryLinkText}>
                        {t('checkout:posFlow.payment.retryActions.reloadRegisterList')}
                      </Text>
                    </Pressable>
                  ) : null}
                </View>
              )}

              {/* Benefit eligibility preview: read-only info when customer selected (not guest) and cart has items */}
              {shouldFetchEligibility && (
                <View style={styles.benefitPreviewSection}>
                  <Text style={styles.benefitPreviewTitle}>
                    {t('checkout:posFlow.payment.benefits.previewTitle')}
                  </Text>
                  {/* Preview-only savings indicator: do not imply final discount; for cashier awareness only */}
                  {!eligibilityPreviewLoading &&
                    eligibilityPreview &&
                    typeof eligibilityPreview.totalDiscountAmount === 'number' &&
                    eligibilityPreview.totalDiscountAmount > 0 && (
                      <Text style={styles.savingsIndicatorText}>
                        {t('checkout:posFlow.payment.benefits.possibleSavings', {
                          amount: formatPrice(eligibilityPreview.totalDiscountAmount),
                        })}
                      </Text>
                    )}
                  {eligibilityPreviewLoading ? (
                    <View style={styles.benefitPreviewLoading}>
                      <WaveLoader size={22} color={SoftColors.accent} />
                      <Text style={[styles.benefitPreviewMuted, styles.benefitPreviewLoadingLabel]}>
                        {t('checkout:posFlow.payment.benefits.loading')}
                      </Text>
                    </View>
                  ) : eligibilityPreview ? (
                    <>
                      {eligibilityPreview.applicableBenefits.length > 0 && (
                        <View style={styles.benefitList}>
                          {eligibilityPreview.applicableBenefits.map((b, idx) => (
                            <Text key={idx} style={styles.benefitApplicable}>
                              � {b.description}{' '}
                              {b.amount < 0 ? formatPrice(Math.abs(b.amount)) : ''}
                            </Text>
                          ))}
                        </View>
                      )}
                      {eligibilityPreview.blockedBenefits.length > 0 && (
                        <View style={styles.benefitList}>
                          {eligibilityPreview.blockedBenefits.map((b, idx) => (
                            <Text key={idx} style={styles.benefitBlocked}>
                              � {formatBlockedReason(t, b)}
                            </Text>
                          ))}
                        </View>
                      )}
                      {(eligibilityPreview.applicableBenefits.length > 0 ||
                        eligibilityPreview.blockedBenefits.length > 0) && (
                        <Text style={styles.benefitPreviewDisclaimer}>
                          {t('checkout:posFlow.payment.benefits.disclaimer')}
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
                  ]}>
                  <View style={styles.paymentMethodsContainer}>
                    {methodsLoading || !cashRegisterResolved ? (
                      <View style={styles.paymentMethodsLoading}>
                        <WaveLoader color={SoftColors.accent} />
                        <Text style={styles.paymentMethodsLoadingLabel}>{t('common:loading')}</Text>
                      </View>
                    ) : settlementPaymentMethods && settlementPaymentMethods.length > 0 ? (
                      settlementPaymentMethods.map((method) => {
                        const isSelected = selectedPaymentMethod === method.type;
                        const onlineOfflineDisabled = isOnlinePaymentDisabledOffline(
                          method.type,
                          isOnline
                        );
                        const methodDisabled =
                          paymentInteractionsLocked || onlineOfflineDisabled;
                        const methodLabel =
                          method.type === 'credit_card'
                            ? t('checkout:posFlow.payment.onlinePayment.kreditkarte')
                            : method.type === 'paypal'
                              ? t('checkout:posFlow.payment.onlinePayment.paypal')
                              : method.name;
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
                              if (onlineOfflineDisabled) {
                                Alert.alert(
                                  t('checkout:posFlow.payment.onlinePayment.offlineAlertTitle'),
                                  t('checkout:posFlow.payment.onlinePayment.offlineDisabled')
                                );
                                return;
                              }
                              if (method.type !== selectedPaymentMethod) {
                                setSelectedPaymentMethodType(method.type);
                              }
                            }}
                            accessibilityRole="button"
                            accessibilityState={{ selected: isSelected, disabled: methodDisabled }}
                            accessibilityLabel={`${methodLabel}${isSelected ? t('checkout:posFlow.payment.methodSelectedA11ySuffix') : ''}${onlineOfflineDisabled ? t('checkout:posFlow.payment.onlinePayment.offlineA11ySuffix') : ''}`}>
                            <Ionicons
                              name={method.icon as any}
                              size={24}
                              color={isSelected ? SoftColors.accent : SoftColors.textSecondary}
                            />
                            <Text
                              style={[
                                styles.paymentMethodText,
                                isSelected && styles.selectedPaymentMethodText,
                              ]}>
                              {methodLabel}
                            </Text>
                          </Pressable>
                        );
                      })
                    ) : isRegisterGateBlockingPayment || !hasValidCashRegisterId ? (
                      <Text style={styles.paymentMethodsGateHint}>
                        {registerGateFooterHint(registerGateCtx)}
                      </Text>
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

              {/* Step 3: Nakit � Betrag & R�ckgeld */}
              {shouldCollectCashAmount && (
                <View style={styles.section}>
                  <Text style={styles.stepLabel}>3</Text>
                  <Text style={styles.sectionTitle}>
                    {t('checkout:posFlow.payment.steps.cash')}
                  </Text>

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
                          onPress={() => {
                            handlePresetPress(preset);
                          }}
                          accessibilityRole="button"
                          accessibilityState={{ selected: isPresetSelected }}
                          accessibilityLabel={`${formatPrice(preset)}${isPresetSelected ? ', ausgew�hlt' : ''}`}>
                          <Text
                            style={[
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
                      <Text style={styles.label}>
                        {t('checkout:posFlow.payment.cash.receivedLabel')}
                      </Text>
                      {!isAmountValid ? (
                        <Text
                          style={styles.cashAmountWarnIcon}
                          accessibilityRole="image"
                          accessibilityLabel="!">
                          !
                        </Text>
                      ) : null}
                    </View>
                    <TextInput
                      style={styles.amountInput}
                      value={amountReceived}
                      onChangeText={setAmountReceived}
                      placeholder={t('checkout:posFlow.payment.cash.placeholder')}
                      keyboardType="decimal-pad"
                      editable={!paymentInteractionsLocked}
                      accessibilityLabel={t('checkout:posFlow.payment.cash.receivedA11y')}
                      accessibilityHint={t('checkout:posFlow.payment.cash.optionalHint')}
                    />
                  </View>
                  <Text style={styles.cashOptionalHint}>
                    {t('checkout:posFlow.payment.cash.optionalHint')}
                  </Text>
                  {!cashTender.fieldEmpty && cashTender.isInsufficient ? (
                    <Text style={styles.cashBelowTotalHint} accessibilityRole="alert">
                      {t('checkout:posFlow.payment.cash.belowTotalMessage', {
                        amount: formatPrice(settlementAmountDue),
                      })}
                    </Text>
                  ) : null}
                  {showCashChange && changeAmount != null ? (
                    <View style={styles.changeRow}>
                      <Text style={styles.changeLabel}>
                        {t('checkout:posFlow.payment.cash.changeLabel')}
                      </Text>
                      <Text style={styles.changeAmount}>{formatPrice(changeAmount)}</Text>
                    </View>
                  ) : null}
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
                          Alert.alert(
                            t('checkout:posFlow.payment.voucherToggle.offlineAlertTitle'),
                            t('checkout:posFlow.payment.voucherToggle.offlineNotPossible')
                          );
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
                        (paymentInteractionsLocked || tseServerOffline) &&
                          styles.paymentMethodDisabled,
                        pressed &&
                          !paymentInteractionsLocked &&
                          !tseServerOffline &&
                          SoftState.pressedScale,
                      ]}
                      disabled={paymentInteractionsLocked || tseServerOffline}
                      accessibilityRole="button"
                      accessibilityState={{
                        selected: voucherEnabled,
                        disabled: paymentInteractionsLocked || tseServerOffline,
                      }}
                      accessibilityLabel={`${t('checkout:posFlow.payment.voucherToggle.redeem')}${voucherEnabled ? t('checkout:posFlow.payment.methodSelectedA11ySuffix') : ''}`}>
                      <Ionicons
                        name="gift-outline"
                        size={24}
                        color={voucherEnabled ? SoftColors.accent : SoftColors.textSecondary}
                      />
                      <Text
                        style={[
                          styles.paymentMethodText,
                          voucherEnabled && styles.selectedPaymentMethodText,
                        ]}>
                        {t('checkout:posFlow.payment.voucherToggle.redeem')}
                      </Text>
                    </Pressable>
                  </View>
                  {tseServerOffline ? (
                    <Text style={styles.voucherInlineError}>
                      {t('checkout:posFlow.payment.voucherToggle.offlineUnavailable')}
                    </Text>
                  ) : !isOnline && voucherEnabled ? (
                    <Text style={styles.voucherInlineError}>
                      {t('checkout:posFlow.payment.voucherToggle.offlineNotPossible')}
                    </Text>
                  ) : null}
                  {voucherEnabled ? (
                    <>
                      <View style={styles.cashLabelWithHint}>
                        <Text style={styles.voucherFieldLabel}>
                          {t('checkout:posFlow.payment.voucher.codeLabel')}
                        </Text>
                        {!isVoucherCodeValid ? (
                          <Text
                            style={styles.cashAmountWarnIcon}
                            accessibilityRole="image"
                            accessibilityLabel="?">
                            ??
                          </Text>
                        ) : null}
                      </View>
                      <View style={styles.voucherCodeRow}>
                        <TextInput
                          style={[styles.voucherCodeInput, styles.voucherCodeInputFlex]}
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
                          onPress={() => {
                            setVoucherScannerVisible(true);
                          }}
                          disabled={!isOnline || paymentInteractionsLocked}
                          style={({ pressed }) => [
                            styles.voucherScanBtn,
                            (!isOnline || paymentInteractionsLocked) &&
                              styles.voucherCheckButtonDisabled,
                            pressed && styles.voucherScanBtnPressed,
                          ]}
                          accessibilityRole="button"
                          accessibilityLabel={t('checkout:posFlow.payment.voucherToggle.scanA11y')}>
                          <Ionicons name="scan-outline" size={22} color={SoftColors.accent} />
                        </Pressable>
                      </View>
                      <Pressable
                        onPress={handleVoucherCheck}
                        disabled={voucherCheckLoading || !isOnline || paymentInteractionsLocked}
                        style={({ pressed }) => [
                          styles.voucherCheckButton,
                          (voucherCheckLoading || !isOnline || paymentInteractionsLocked) &&
                            styles.voucherCheckButtonDisabled,
                          pressed &&
                            !voucherCheckLoading &&
                            isOnline &&
                            !paymentInteractionsLocked &&
                            SoftState.pressedScale,
                        ]}
                        accessibilityRole="button"
                        accessibilityLabel={t('checkout:posFlow.payment.voucher.checkButton')}>
                        {voucherCheckLoading ? (
                          <View style={styles.voucherCheckButtonInner}>
                            <WaveLoader size={18} color={SoftColors.accent} />
                            <Text style={styles.voucherCheckButtonText}>
                              {t('checkout:posFlow.payment.voucher.checking')}
                            </Text>
                          </View>
                        ) : (
                          <Text style={styles.voucherCheckButtonText}>
                            {t('checkout:posFlow.payment.voucher.checkButton')}
                          </Text>
                        )}
                      </Pressable>
                      {voucherSnapshot ? (
                        <View style={styles.voucherInfoBlock}>
                          <Text style={styles.voucherInfoLine}>
                            {t('checkout:posFlow.payment.voucher.balanceLabel')}:{' '}
                            {formatPrice(voucherSnapshot.remainingAmount)}
                          </Text>
                          <Text style={styles.voucherInfoLine}>
                            {t('checkout:posFlow.payment.voucher.maxUsableLabel')}:{' '}
                            {formatPrice(effectiveVoucherRedeemCap)}
                          </Text>
                          <Text style={styles.voucherMuted}>
                            {t('checkout:posFlow.payment.voucher.maskedHint', {
                              masked: voucherSnapshot.maskedCode,
                            })}
                          </Text>
                          {(() => {
                            const exp = new Date(voucherSnapshot.expiresAtUtc);
                            if (Number.isNaN(exp.getTime())) return null;
                            return (
                              <Text style={styles.voucherMuted}>
                                {t('checkout:posFlow.payment.voucher.expiresHint', {
                                  date: formatUserDate(exp),
                                })}
                              </Text>
                            );
                          })()}
                        </View>
                      ) : null}
                      <View style={styles.inputRow}>
                        <Text style={styles.label}>
                          {t('checkout:posFlow.payment.voucher.redeemAmountLabel')}
                        </Text>
                        <TextInput
                          style={styles.amountInput}
                          value={voucherRedeemAmountStr}
                          onChangeText={setVoucherRedeemAmountStr}
                          placeholder={t('checkout:posFlow.payment.placeholderAmount')}
                          keyboardType="decimal-pad"
                          editable={
                            !!voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim()
                          }
                          accessibilityLabel={t(
                            'checkout:posFlow.payment.voucher.redeemAmountLabel'
                          )}
                        />
                      </View>
                      {voucherSnapshot && Number.isFinite(voucherRedeemParsed) ? (
                        <View style={styles.inputRow}>
                          <Text style={styles.label}>
                            {t('checkout:posFlow.payment.remainingAmount')}
                          </Text>
                          <Text style={styles.voucherInfoLine}>
                            {formatPrice(voucherRemainingToPay)}
                          </Text>
                        </View>
                      ) : null}
                      {voucherLocalError ? (
                        <Text style={styles.voucherInlineError}>{voucherLocalError}</Text>
                      ) : null}
                      {voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim() ? (
                        <Text style={styles.voucherMuted}>
                          {t('checkout:posFlow.payment.voucher.changeCodeHint')}
                        </Text>
                      ) : null}
                    </>
                  ) : null}
                </View>
              )}

              <View style={styles.section}>
                <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <View style={{ flex: 1, paddingRight: 12 }}>
                    <Text style={styles.sectionTitle}>
                      {t('checkout:posFlow.payment.preorder.checkbox')}
                    </Text>
                    <Text style={styles.voucherMuted}>
                      {t('checkout:posFlow.payment.preorder.hint')}
                    </Text>
                  </View>
                  <Switch
                    value={isPreorder}
                    onValueChange={(value) => {
                      setIsPreorder(value);
                      if (value) {
                        setBalanceOrder(null);
                        setBalanceQuery('');
                        setBalanceLookupError(null);
                      }
                    }}
                    accessibilityLabel={t('checkout:posFlow.payment.preorder.checkbox')}
                  />
                </View>
                {isPreorder ? (
                  <View>
                    <Text style={styles.voucherMuted}>
                      {t('checkout:posFlow.payment.preorder.remainingLabel')}
                    </Text>
                    <TextInput
                      style={styles.notesInput}
                      value={preorderRemainingText}
                      onChangeText={setPreorderRemainingText}
                      keyboardType="decimal-pad"
                      placeholder="0,00"
                      accessibilityLabel={t('checkout:posFlow.payment.preorder.remainingLabel')}
                    />
                    <Text style={styles.voucherMuted}>
                      {t('checkout:posFlow.payment.preorder.remainingHint')}
                    </Text>
                  </View>
                ) : (
                  <View>
                    <Text style={styles.voucherMuted}>
                      {t('checkout:posFlow.payment.preorder.balanceLabel')}
                    </Text>
                    <TextInput
                      style={styles.notesInput}
                      value={balanceQuery}
                      onChangeText={(value) => {
                        setBalanceQuery(value);
                        setBalanceOrder(null);
                        setBalanceLookupError(null);
                      }}
                      onBlur={() => {
                        const key = balanceQuery.trim();
                        if (!key) return;
                        void (async () => {
                          try {
                            const found = await getPreorderByReceipt(key);
                            if ((found.remainingAmount ?? 0) <= 0.01) {
                              setBalanceLookupError(t('checkout:posFlow.payment.preorder.balanceZero'));
                              return;
                            }
                            setBalanceOrder(found);
                          } catch {
                            setBalanceLookupError(t('checkout:posFlow.payment.preorder.balanceNotFound'));
                          }
                        })();
                      }}
                      placeholder={t('checkout:posFlow.payment.preorder.balancePlaceholder')}
                      autoCapitalize="characters"
                    />
                    {balanceOrder ? (
                      <Text style={styles.voucherMuted}>
                        {t('checkout:posFlow.payment.preorder.balanceFound', {
                          number: balanceOrder.preorderNumber ?? '',
                          amount: formatPrice(balanceOrder.remainingAmount ?? 0),
                        })}
                      </Text>
                    ) : null}
                    {balanceLookupError ? (
                      <Text style={styles.voucherInlineError}>{balanceLookupError}</Text>
                    ) : null}
                  </View>
                )}
              </View>

              <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('checkout:posFlow.payment.notes.title')}</Text>
                <TextInput
                  style={styles.notesInput}
                  value={notes}
                  onChangeText={setNotes}
                  placeholder={t('checkout:posFlow.payment.notes.placeholder')}
                  multiline
                  numberOfLines={2}
                  accessibilityLabel={t('checkout:posFlow.payment.notes.a11y')}
                />
              </View>

              {/* Hata Mesaj? */}
              {error && (
                <View style={styles.errorContainer}>
                  <Text style={styles.errorText}>{error}</Text>
                </View>
              )}
              {needFiscalTseForPay ? (
                <View style={styles.section} accessibilityRole="summary">
                  <TseStatusIndicator />
                  {payGateTseBlocked ? (
                    <Text style={styles.paymentMethodRequiredHint} accessibilityRole="alert">
                      {resolveTseGateMessage()}
                    </Text>
                  ) : null}
                </View>
              ) : null}
            </ScrollView>

            {purchaseState === 'input' || purchaseState === 'processing' ? (
              <View
                style={[styles.footer, { paddingBottom: Math.max(SoftSpacing.md, insets.bottom) }]}>
                {timeSyncCritical ? (
                  <View style={styles.timeSyncCriticalBanner} accessibilityRole="alert">
                    <Text style={styles.timeSyncCriticalText}>
                      {t('checkout:posFlow.payment.timeSync.banner')}
                    </Text>
                    <Pressable
                      onPress={() => {
                        Alert.alert(
                          t('checkout:posFlow.payment.timeSync.contactAdminA11y'),
                          POS_TIME_SYNC_ADMIN_CONTACT_MESSAGE_DE
                        );
                      }}
                      style={({ pressed }) => [
                        styles.timeSyncAdminButton,
                        pressed && SoftState.pressed,
                      ]}
                      accessibilityRole="button"
                      accessibilityLabel={t('checkout:posFlow.payment.timeSync.contactAdminA11y')}>
                      <Text style={styles.timeSyncAdminButtonText}>
                        {t('checkout:posFlow.payment.timeSync.contactAdmin')}
                      </Text>
                    </Pressable>
                  </View>
                ) : null}
                {needFiscalTseForPay &&
                tseCheckFailureStreak >= POS_TSE_STATUS_FAILURE_WARN_STREAK ? (
                  <View style={styles.operatorWarnBanner} accessibilityRole="alert">
                    <Text style={styles.operatorWarnBannerText}>
                      {t('checkout:posFlow.payment.operatorWarnings.tseUnstableBanner')}
                    </Text>
                  </View>
                ) : null}
                <View style={styles.footerButtonRow}>
                  <Pressable
                    onPress={handleClose}
                    style={({ pressed }) => [styles.cancelButton, pressed && SoftState.pressed]}
                    disabled={showPayWorking}
                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                    accessibilityLabel={t('common:cancel')}
                    accessibilityRole="button">
                    <Text style={styles.cancelButtonText}>
                      {t('checkout:posFlow.payment.buttons.cancel')}
                    </Text>
                  </Pressable>
                  {showStornoRefundEntry ? (
                    <Pressable
                      onPress={() => {
                        setStornoRefundWizardVisible(true);
                      }}
                      style={({ pressed }) => [
                        styles.stornoRefundSideBtn,
                        pressed && SoftState.pressed,
                        showPayWorking && styles.payButtonDisabled,
                      ]}
                      disabled={showPayWorking}
                      hitSlop={{ top: 8, bottom: 8, left: 8, right: 8 }}
                      accessibilityLabel={t('checkout:posFlow.stornoRefund.paymentModalEntry')}
                      accessibilityRole="button">
                      <Text style={styles.stornoRefundSideBtnText} numberOfLines={2}>
                        {t('checkout:posFlow.stornoRefund.paymentModalEntry')}
                      </Text>
                    </Pressable>
                  ) : null}
                  {canMakePayment ? (
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
                      accessibilityLabel={t('checkout:posFlow.payment.footer.payA11y', {
                        amount: formatPrice(settlementAmountDue),
                      })}
                      accessibilityHint={paySubmitBlockedHint}
                      accessibilityRole="button"
                      accessibilityState={{
                        disabled: paySubmitDisabled,
                      }}>
                      {showPayWorking ? (
                        <View style={styles.payButtonContent}>
                          <WaveLoader size={18} color={SoftColors.textInverse} />
                          <Text style={styles.payButtonText}>
                            {onlinePaymentPhase === 'awaiting_action'
                              ? t('checkout:posFlow.payment.onlinePayment.awaitingGateway')
                              : t('checkout:posFlow.payment.footer.processing')}
                          </Text>
                        </View>
                      ) : (
                        <View style={styles.payButtonContent}>
                          {showPayTrialWarningIcon ? (
                            <Ionicons
                              name="warning"
                              size={18}
                              color="#FFB300"
                              accessibilityLabel={t(
                                'checkout:posFlow.payment.footer.licenseHintA11y'
                              )}
                            />
                          ) : null}
                          <Text style={styles.payButtonText} numberOfLines={2}>
                            {voucherEnabled &&
                            voucherSettlementValid &&
                            paymentCoverageOk &&
                            settlementAmountDue <= 0.01
                              ? t('checkout:posFlow.payment.voucher.payCta', {
                                  amount: formatPrice(totalAmount),
                                })
                              : settlementAmountDue > 0.01 && selectedSettlementMethod
                                ? t('checkout:posFlow.payment.footer.payMethodSuffix', {
                                    amount: formatPrice(settlementAmountDue),
                                    method: selectedSettlementMethod.name.toLowerCase(),
                                  })
                                : t('checkout:posFlow.payment.footer.payGeneric', {
                                    amount: formatPrice(totalAmount),
                                  })}
                          </Text>
                        </View>
                      )}
                    </Pressable>
                  ) : (
                    <View
                      style={styles.payPermissionBanner}
                      accessibilityRole="alert"
                      accessibilityLiveRegion="polite">
                      <Text style={styles.payPermissionBannerText}>
                        {t('checkout:posFlow.payment.blockedHints.noPaymentPermission')}
                      </Text>
                    </View>
                  )}
                </View>
                {canMakePayment && !cashRegisterResolved ? (
                  <Text style={styles.footerBlockedHint}>
                    {t('checkout:posFlow.payment.footer.registerSettingsLoading')}
                  </Text>
                ) : canMakePayment && !hasValidCashRegisterId ? (
                  <Text style={styles.footerBlockedHint}>
                    {registerGateFooterHint(registerGateCtx)}
                  </Text>
                ) : canMakePayment && paySubmitBlockedHint ? (
                  <Text style={styles.footerBlockedHint}>{paySubmitBlockedHint}</Text>
                ) : null}
              </View>
            ) : (
              <View
                style={[
                  styles.footerSecondary,
                  { paddingBottom: Math.max(SoftSpacing.md, insets.bottom) },
                ]}>
                {purchaseState === 'printing' && (
                  <View style={styles.statusBlock}>
                    <WaveLoader size={36} color={SoftColors.accent} />
                    <Text style={styles.statusText}>
                      {t('checkout:posFlow.payment.print.printing')}
                    </Text>
                  </View>
                )}

                {purchaseState === 'completed' && (
                  <View style={styles.statusBlock}>
                    <Ionicons name="checkmark-circle" size={48} color={SoftColors.success} />
                    <Text style={styles.successTitle}>
                      {t('checkout:posFlow.payment.success.title')}
                    </Text>
                    {(() => {
                      // Receipt: GET /api/pos/payment/{id}/receipt
                      const summaryReceipt = toSummaryReceipt(receiptData ?? null);
                      return summaryReceipt ? (
                        <View
                          style={[
                            styles.receiptPreview,
                            { marginTop: SoftSpacing.sm, maxHeight: 320 },
                          ]}>
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
                        onPress={() =>
                          completedPaymentId && handleSuccessAndClose(completedPaymentId)
                        }
                        style={({ pressed }) => [
                          styles.successPrimaryBtn,
                          pressed && SoftState.pressedScale,
                        ]}
                        accessibilityRole="button"
                        accessibilityLabel={t('checkout:posFlow.payment.success.doneA11y')}>
                        <Text style={styles.payButtonText}>
                          {t('checkout:posFlow.payment.success.done')}
                        </Text>
                      </Pressable>
                    </View>
                  </View>
                )}

                {purchaseState === 'print_error' && (
                  <View style={styles.printErrorBlock}>
                    <Text style={styles.printErrorTitle}>
                      {t('checkout:posFlow.payment.print.failedTitle')}
                    </Text>
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
                      <Pressable onPress={handleSkipPrint} style={styles.printErrorBtnSecondary}>
                        <Text style={styles.printErrorBtnSecondaryText}>
                          {t('checkout:posFlow.payment.print.skip')}
                        </Text>
                      </Pressable>
                      <Pressable onPress={handleRetryPrint} style={styles.payButton}>
                        <Text style={styles.payButtonText}>
                          {canReprintReceipt
                            ? t('receipts:reprint')
                            : t('checkout:posFlow.payment.print.retry')}
                        </Text>
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
        onRequestClose={() => {
          setShowStartbelegSaleModal(false);
        }}>
        <View style={styles.startbelegBlockBackdrop}>
          <View style={styles.startbelegBlockCard}>
            <Text style={styles.startbelegBlockTitle}>
              {t('checkout:posFlow.payment.startbelegBlock.title')}
            </Text>
            <Text style={styles.startbelegBlockBody}>
              {t('checkout:posFlow.payment.startbelegBlock.body')}
            </Text>
            <Pressable
              onPress={() => {
                setShowStartbelegSaleModal(false);
              }}
              style={({ pressed }) => [
                styles.startbelegBlockBtn,
                pressed && SoftState.pressedScale,
              ]}
              accessibilityRole="button"
              accessibilityLabel={t('checkout:posFlow.payment.startbelegBlock.understoodA11y')}>
              <Text style={styles.startbelegBlockBtnText}>
                {t('checkout:posFlow.payment.startbelegBlock.understood')}
              </Text>
            </Pressable>
          </View>
        </View>
      </Modal>

      <VoucherScanner
        visible={voucherScannerVisible}
        onClose={() => {
          setVoucherScannerVisible(false);
        }}
        onVoucherValidated={(code, snapshot) => {
          setVoucherCode(code);
          setVoucherLocalError(null);
          setVoucherSnapshot(snapshot);
          setValidatedVoucherCode(code);
          const maxForThisCart = computeVoucherMaxForSale(totalAmount, snapshot, true);
          setVoucherRedeemAmountEffective(maxForThisCart);
          voucherValidatedTotalRef.current = totalAmount;
          setVoucherEnabled(true);
        }}
      />

      <StornoRefundSelection
        visible={stornoRefundWizardVisible}
        onClose={() => {
          setStornoRefundWizardVisible(false);
        }}
        cashRegisterId={cashRegisterId ?? '00000000-0000-0000-0000-000000000000'}
        tableNumber={tableNumber ?? 0}
        onSuccess={() => {
          onPosToast?.({
            type: 'success',
            message: t('checkout:posFlow.stornoRefund.alerts.successTitle'),
          });
        }}
      />
      <CardPaymentModal
        visible={cardSimVisible}
        amount={gatewayChargeAmount}
        cashRegisterId={cashRegisterId ?? ''}
        onClose={() => {
          setCardSimVisible(false);
        }}
        onSuccess={(intentId) => {
          setCardPaymentIntentId(intentId);
          setCardSimVisible(false);
          void executePaymentSubmission(intentId);
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
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          zIndex: 2147483646,
        } as unknown as ViewStyle)
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
  paymentMethodsGateHint: {
    ...SoftTypography.bodySmall,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    paddingHorizontal: SoftSpacing.sm,
    width: '100%',
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
  cashOptionalHint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: 4,
  },
  cashBelowTotalHint: {
    ...SoftTypography.caption,
    color: SoftColors.error,
    marginTop: 4,
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
  payPermissionBanner: {
    flex: 2,
    minHeight: 48,
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.errorBg,
    borderWidth: 1,
    borderColor: 'rgba(220, 38, 38, 0.35)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  payPermissionBannerText: {
    ...SoftTypography.caption,
    fontWeight: '700',
    color: SoftColors.error,
    textAlign: 'center',
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
  voucherCodeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  voucherCodeInput: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    ...SoftTypography.body,
    color: SoftColors.textPrimary,
  },
  voucherCodeInputFlex: {
    flex: 1,
  },
  voucherScanBtn: {
    borderWidth: 1,
    borderColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.sm,
    justifyContent: 'center',
    alignItems: 'center',
  },
  voucherScanBtnPressed: {
    opacity: 0.85,
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
