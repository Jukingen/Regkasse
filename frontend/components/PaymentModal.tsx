import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useMemo, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator,
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
import { usePayment } from '../hooks/usePayment';
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
import { receiptPrinter } from '../services/receiptPrinter';
import { PaymentSuccessQr } from './PaymentSuccessQr';
import { ReceiptSummary, type ReceiptSummaryReceipt } from './ReceiptSummary';
import type { PaymentTseInfo } from '../services/api/paymentService';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import { downloadInvoicePdf, InvoicePdfHttpError } from '../services/api/invoiceService';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import {
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

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
  tableNumber
}: PaymentModalProps) {
  const { t, i18n } = useTranslation(['checkout', 'common', 'invoices', 'settings']);
  const { user } = useAuth();
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<string>('cash');
  const [amountReceived, setAmountReceived] = useState<string>('');
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
  /** When POST response has no qrPayload, show GET /pos/payment/{id}/qr.png as data URL. */
  const [qrPngFallback, setQrPngFallback] = useState<string | null>(null);
  /** True while fetching qr.png when qrPayload is missing (user-visible; avoids silent empty QR). */
  const [qrPngLoading, setQrPngLoading] = useState(false);
  /** True after qr.png fetch completed without a usable image and no qrPayload. */
  const [qrPngFetchFailed, setQrPngFetchFailed] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);
  /** Prevents double-submit during async work before purchaseState becomes 'processing'. */
  const [paymentBusy, setPaymentBusy] = useState(false);

  /** Gutschein: code, validate snapshot, redeem amount (must match fiscal total for single-code flow). */
  const [voucherCode, setVoucherCode] = useState('');
  const [voucherRedeemAmountStr, setVoucherRedeemAmountStr] = useState('');
  const [voucherSnapshot, setVoucherSnapshot] = useState<VoucherValidateSuccess | null>(null);
  const [validatedVoucherCode, setValidatedVoucherCode] = useState<string | null>(null);
  const [voucherCheckLoading, setVoucherCheckLoading] = useState(false);
  const [voucherLocalError, setVoucherLocalError] = useState<string | null>(null);
  const voucherValidatedTotalRef = useRef<number | null>(null);

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
    ]
  );

  // DEV: TSE Simulation Toggle
  // Default: AÇIK (Bypass) in Development
  const [isTseSimulationEnabled, setIsTseSimulationEnabled] = useState<boolean>(__DEV__);

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
    const m = paymentMethods.find((x) => x.type === selectedPaymentMethod);
    if (m?.requiresReceivedAmount !== undefined) return m.requiresReceivedAmount;
    return selectedPaymentMethod === 'cash';
  }, [paymentMethods, selectedPaymentMethod]);

  // Backend line toplamları kullan - FE hesaplama yapmaz (totalPrice = lineGross)
  const calculatedCartItems = useMemo(() => {
    return cartItems.map(item => ({
      ...item,
      lineTotal: item.totalPrice ?? (item.quantity * item.unitPrice)
    }));
  }, [cartItems]);

  const totalAmount = grandTotalGross ?? calculatedCartItems.reduce((sum, item) => sum + item.lineTotal, 0);

  const resetVoucherUi = () => {
    setVoucherCode('');
    setVoucherRedeemAmountStr('');
    setVoucherSnapshot(null);
    setValidatedVoucherCode(null);
    setVoucherLocalError(null);
    setVoucherCheckLoading(false);
    voucherValidatedTotalRef.current = null;
  };

  const changeAmount = parseFloat(amountReceived) - totalAmount;

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

  const cashPresets = getCashPresets(totalAmount);

  /**
   * Block Zahlen until register gate passes + in-flight payment (paymentBusy / processing).
   * Omit hook `loading`: it can block unrelated actions and caused silent early-return in handlePayment without matching disabled state in edge races.
   */
  const voucherReady =
    selectedPaymentMethod !== 'voucher' ||
    (!!voucherSnapshot &&
      !!validatedVoucherCode &&
      validatedVoucherCode.trim() === voucherCode.trim() &&
      voucherSnapshot.remainingAmount + 0.009 >= totalAmount &&
      voucherValidatedTotalRef.current != null &&
      Math.abs(voucherValidatedTotalRef.current - totalAmount) <= 0.02);

  const voucherRedeemParsed = parseLocaleDecimal(voucherRedeemAmountStr);
  const voucherAmountMatchesTotal =
    selectedPaymentMethod !== 'voucher' ||
    (Number.isFinite(voucherRedeemParsed) &&
      Math.abs(voucherRedeemParsed - totalAmount) <= 0.02 &&
      (!voucherSnapshot || voucherRedeemParsed <= voucherSnapshot.remainingAmount + 0.02));

  const paySubmitDisabled =
    purchaseState === 'processing' ||
    paymentBusy ||
    isRegisterGateBlockingPayment ||
    (selectedPaymentMethod === 'voucher' && (!voucherReady || !voucherAmountMatchesTotal));

  const showPayWorking = purchaseState === 'processing' || paymentBusy;

  const zahlenBlockedByRegisterHint =
    paySubmitDisabled && !showPayWorking && isRegisterGateBlockingPayment
      ? registerGateFooterHint(registerGateCtx)
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
    if (!visible || !paymentMethods?.length) return;
    const hasSelection = paymentMethods.some((m) => m.type === selectedPaymentMethod);
    if (hasSelection) return;
    const def = paymentMethods.find((m) => m.isDefault) ?? paymentMethods[0];
    if (def) setSelectedPaymentMethod(def.type);
  }, [visible, paymentMethods, selectedPaymentMethod]);

  /** Cart total changed after voucher validation — require a new check (keep typed code). */
  useEffect(() => {
    if (!visible) return;
    if (selectedPaymentMethod !== 'voucher' || !voucherSnapshot || voucherValidatedTotalRef.current == null) return;
    if (Math.abs(voucherValidatedTotalRef.current - totalAmount) > 0.02) {
      setVoucherSnapshot(null);
      setValidatedVoucherCode(null);
      voucherValidatedTotalRef.current = null;
      setVoucherRedeemAmountStr('');
      setVoucherLocalError(t('checkout:posFlow.payment.voucher.totalChangedHint'));
    }
  }, [totalAmount, visible, selectedPaymentMethod, voucherSnapshot, t]);

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

  useEffect(() => {
    const showQrStates: Array<'completed' | 'print_error'> = ['completed', 'print_error'];
    if (!completedPaymentId || !showQrStates.includes(purchaseState as 'completed' | 'print_error')) {
      setQrPngFallback(null);
      setQrPngLoading(false);
      setQrPngFetchFailed(false);
      return;
    }
    if (completedPaymentTse?.qrPayload) {
      setQrPngFallback(null);
      setQrPngLoading(false);
      setQrPngFetchFailed(false);
      return;
    }
    let cancelled = false;
    setQrPngFallback(null);
    setQrPngFetchFailed(false);
    setQrPngLoading(true);
    paymentService
      .getQrPngAsBase64(completedPaymentId)
      .then((url) => {
        if (cancelled) return;
        if (url) {
          setQrPngFallback(url);
          setQrPngFetchFailed(false);
          debugPosPaymentTrace('success_flow_qr_ready', { source: 'qr_png_fallback' });
        } else {
          setQrPngFetchFailed(true);
        }
      })
      .catch(() => {
        if (!cancelled) setQrPngFetchFailed(true);
      })
      .finally(() => {
        if (!cancelled) setQrPngLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [completedPaymentId, completedPaymentTse?.qrPayload, purchaseState]);

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
      const r = await validateVoucher(trimmed, totalAmount > 0 ? totalAmount : undefined);
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
      if (r.remainingAmount + 0.009 < totalAmount) {
        setVoucherLocalError(t('checkout:posFlow.payment.voucher.insufficient'));
        setVoucherSnapshot(null);
        setValidatedVoucherCode(null);
        voucherValidatedTotalRef.current = null;
        return;
      }
      setVoucherSnapshot(r);
      setValidatedVoucherCode(trimmed);
      setVoucherRedeemAmountStr(totalAmount.toFixed(2));
      voucherValidatedTotalRef.current = totalAmount;
    } finally {
      setVoucherCheckLoading(false);
    }
  };

  // Ödeme işlemi
  const handlePayment = async () => {
    debugPosPaymentTrace('submit_clicked', {
      authUserPresent: !!user?.id,
      paySubmitDisabled,
      cashRegisterResolved,
      hasValidCashRegisterId,
    });
    // Intentionally no Alert: button shows "Wird verarbeitet…" and is non-interactive while busy.
    if (paymentBusy || purchaseState === 'processing') {
      debugPosPaymentTrace('submit_blocked_busy', { paymentBusy, purchaseState });
      return;
    }
    setPaymentBusy(true);
    try {
      // 1. Validasyonlar — Tischnummer: layout usually passes activeTableId; default 1 if missing (guest POS)
      const resolvedTableNumber =
        tableNumber != null && Number.isFinite(Number(tableNumber)) && Number(tableNumber) >= 1
          ? Number(tableNumber)
          : 1;

      if (cartItems.length === 0) {
        debugPosPaymentTrace('submit_blocked_empty_cart', {});
        Alert.alert(t('checkout:posFlow.payment.alerts.hintTitle'), t('checkout:posFlow.payment.errors.emptyCart'));
        return;
      }

      if (requiresCashAmount) {
        const received = parseFloat(amountReceived);
        if (isNaN(received) || received < totalAmount) {
          debugPosPaymentTrace('submit_blocked_cash_amount', { received, totalAmount });
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.errors.insufficientAmount')
          );
          return;
        }
      }

      if (selectedPaymentMethod === 'voucher') {
        if (!voucherSnapshot || validatedVoucherCode?.trim() !== voucherCode.trim()) {
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.voucher.invalid')
          );
          return;
        }
        if (voucherSnapshot.remainingAmount + 0.009 < totalAmount) {
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.voucher.insufficient')
          );
          return;
        }
        const vr = parseLocaleDecimal(voucherRedeemAmountStr);
        if (!Number.isFinite(vr) || Math.abs(vr - totalAmount) > 0.02) {
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.voucher.amountMustMatchTotal')
          );
          return;
        }
        if (vr > voucherSnapshot.remainingAmount + 0.02) {
          Alert.alert(
            t('checkout:posFlow.payment.alerts.hintTitle'),
            t('checkout:posFlow.payment.voucher.insufficient')
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
        Alert.alert(t('checkout:posFlow.payment.alerts.paymentNotPossibleTitle'), registerGateAlertMessage(registerGateCtx));
        return;
      }

      if (!validateAmount(totalAmount)) {
        debugPosPaymentTrace('submit_blocked_invalid_amount', { totalAmount });
        Alert.alert(t('checkout:posFlow.payment.alerts.hintTitle'), t('checkout:posFlow.payment.errors.invalidAmountDetailed'));
        return;
      }

      // 2. Aktif sepet ID'sini al (Source of Truth for Cart ID)
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(resolvedTableNumber);

        // Eğer backend cart boş geliyorsa ama frontend doluysa, frontend items kullanmaya devam et
        // Ancak cartId'ye ihtiyacımız var complete için.
        if (!cart || !cart.cartId) {
          throw new Error('Aktif masa sepeti bulunamadı.');
        }
        currentCartId = cart.cartId;
      } catch (cartErr) {
        console.error('Cart fetch failed:', cartErr);
        debugPosPaymentTrace('submit_blocked_cart_fetch', {
          message: cartErr instanceof Error ? cartErr.message : String(cartErr),
        });
        Alert.alert(
          t('checkout:posFlow.payment.alerts.errorTitle'),
          t('checkout:posFlow.payment.errors.cartLoadFailedRefresh')
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

      const paymentRequest: PaymentRequest = {
        customerId: finalCustomerId, // Always send valid customer ID (guest or registered)
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod,
          tseRequired: shouldRequireTse,
          amount: requiresCashAmount ? parseFloat(amountReceived) : undefined,
          ...(selectedPaymentMethod === 'voucher' ? { voucherCode: voucherCode.trim() } : {}),
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
      if (__DEV__) {
        const p = paymentRequest.payment;
        const hasVoucherSecret =
          !!(typeof p.voucherCode === 'string' && p.voucherCode.trim()) ||
          (Array.isArray(p.voucherRedemptions) && p.voucherRedemptions.length > 0);
        console.log('[PAYMENT] Request summary:', {
          method: p.method,
          itemCount: paymentItems.length,
          totalAmount: paymentRequest.totalAmount,
          tableNumber: paymentRequest.tableNumber,
          cashRegisterId: paymentRequest.cashRegisterId,
          idempotencyKey: paymentRequest.idempotencyKey,
          hasVoucherSecret,
          voucherRedemptionLineCount: p.voucherRedemptions?.length ?? 0,
        });
      }
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
        Alert.alert(t('checkout:posFlow.payment.errors.failed'), errorMsg);
        setPurchaseState('input');
        return;
      }

      console.log('[PAYMENT] Success, paymentId:', response.paymentId);
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
    setSelectedPaymentMethod('cash');
    setAmountReceived('');
    setNotes('');
    setPurchaseState('input');
    setCompletedPaymentId(null);
    setCompletedPaymentTse(null);
    setReceiptData(null);
    setQrPngFallback(null);
    setQrPngLoading(false);
    setQrPngFetchFailed(false);
    setPaymentBusy(false);
    setPdfLoading(false);
    setEligibilityPreview(null);
    setEligibilityPreviewLoading(false);
    onClose();
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={handleClose}
      accessibilityRole="dialog"
      accessibilityLabel={t('checkout:posFlow.payment.title')}
    >
      <View style={styles.overlay} accessibilityViewIsModal>
        {/* Use View (not Pressable) so web does not swallow inner button presses. */}
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('checkout:posFlow.payment.title')}</Text>
            <Pressable
              onPress={handleClose}
              style={({ pressed, focused }) => [styles.closeButton, pressed && SoftState.pressed, focused && SoftState.focusVisible]}
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
                  <ActivityIndicator color={SoftColors.accent} style={{ marginVertical: SoftSpacing.sm }} />
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
                  <Text style={styles.benefitPreviewMuted}>Vorteile werden geladen…</Text>
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
              <View style={styles.paymentMethodsContainer}>
                {methodsLoading ? (
                  <Text style={styles.loadingText}>{t('common:loading')}</Text>
                ) : paymentMethods && paymentMethods.length > 0 ? (
                  paymentMethods.map((method) => {
                    const isSelected = selectedPaymentMethod === method.type;
                    return (
                      <Pressable
                        key={method.id}
                        style={({ pressed }) => [
                          styles.paymentMethod,
                          isSelected && styles.selectedPaymentMethod,
                          pressed && SoftState.pressedScale,
                        ]}
                        onPress={() => {
                          if (method.type !== selectedPaymentMethod) {
                            if (method.type === 'voucher' || selectedPaymentMethod === 'voucher') {
                              resetVoucherUi();
                            }
                            setSelectedPaymentMethod(method.type);
                          }
                        }}
                        accessibilityRole="button"
                        accessibilityState={{ selected: isSelected }}
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
            </View>

            {/* Step 3: Nakit – Betrag & Rückgeld */}
            {requiresCashAmount && (
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
                          pressed && SoftState.pressedScale,
                        ]}
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
                  <Text style={styles.label}>Erhaltener Betrag</Text>
                  <TextInput
                    style={styles.amountInput}
                    value={amountReceived}
                    onChangeText={setAmountReceived}
                    placeholder="0,00"
                    keyboardType="decimal-pad"
                    accessibilityLabel="Erhaltener Betrag in Euro"
                    accessibilityHint="Mindestens den Gesamtbetrag eingeben"
                  />
                </View>
                {parseFloat(amountReceived) >= totalAmount && (
                  <View style={styles.changeRow}>
                    <Text style={styles.changeLabel}>Rückgeld</Text>
                    <Text style={styles.changeAmount}>{formatPrice(changeAmount)}</Text>
                  </View>
                )}
              </View>
            )}

            {selectedPaymentMethod === 'voucher' && (
              <View style={styles.section}>
                <Text style={styles.stepLabel}>3</Text>
                <Text style={styles.sectionTitle}>Gutschein</Text>
                <Text style={styles.voucherFieldLabel}>{t('checkout:posFlow.payment.voucher.codeLabel')}</Text>
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
                  editable={!voucherCheckLoading}
                  accessibilityLabel={t('checkout:posFlow.payment.voucher.codeLabel')}
                />
                <Pressable
                  onPress={handleVoucherCheck}
                  disabled={voucherCheckLoading}
                  style={({ pressed }) => [
                    styles.voucherCheckButton,
                    voucherCheckLoading && styles.voucherCheckButtonDisabled,
                    pressed && !voucherCheckLoading && SoftState.pressedScale,
                  ]}
                  accessibilityRole="button"
                  accessibilityLabel={t('checkout:posFlow.payment.voucher.checkButton')}
                >
                  {voucherCheckLoading ? (
                    <View style={styles.voucherCheckButtonInner}>
                      <ActivityIndicator size="small" color={SoftColors.accent} />
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
                      {formatPrice(voucherSnapshot.maxRedeemableAmount)}
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
                {voucherLocalError ? <Text style={styles.voucherInlineError}>{voucherLocalError}</Text> : null}
                {voucherSnapshot && validatedVoucherCode?.trim() === voucherCode.trim() ? (
                  <Text style={styles.voucherMuted}>{t('checkout:posFlow.payment.voucher.changeCodeHint')}</Text>
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
              <View style={styles.footerButtonRow}>
                <Pressable
                  onPress={handleClose}
                  style={({ pressed, focused }) => [
                    styles.cancelButton,
                    pressed && SoftState.pressed,
                    focused && SoftState.focusVisible,
                  ]}
                  disabled={showPayWorking}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  accessibilityLabel="Abbrechen"
                  accessibilityRole="button"
                >
                  <Text style={styles.cancelButtonText}>Abbrechen</Text>
                </Pressable>
                <Pressable
                  onPress={handlePayment}
                  style={({ pressed, focused }) => [
                    styles.payButton,
                    paySubmitDisabled && styles.payButtonDisabled,
                    pressed && !paySubmitDisabled && SoftState.pressedScale,
                    focused && !paySubmitDisabled && SoftState.focusVisible,
                  ]}
                  disabled={paySubmitDisabled}
                  hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                  accessibilityLabel={`Zahlen ${formatPrice(totalAmount)}`}
                  accessibilityHint={zahlenBlockedByRegisterHint}
                  accessibilityRole="button"
                  accessibilityState={{
                    disabled: paySubmitDisabled,
                  }}
                >
                  {showPayWorking ? (
                    <View style={styles.payButtonContent}>
                      <ActivityIndicator size="small" color={SoftColors.textInverse} />
                      <Text style={styles.payButtonText}>Wird verarbeitet…</Text>
                    </View>
                  ) : (
                    <Text style={styles.payButtonText}>
                      {selectedPaymentMethod === 'voucher' && voucherReady && voucherAmountMatchesTotal
                        ? t('checkout:posFlow.payment.voucher.payCta', { amount: formatPrice(totalAmount) })
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
              ) : null}
            </View>
          ) : (
            <View style={[styles.footerSecondary, { paddingBottom: Math.max(SoftSpacing.md, insets.bottom) }]}>
              {purchaseState === 'printing' && (
                <View style={styles.statusBlock}>
                  <ActivityIndicator size="large" color={SoftColors.accent} />
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
                  <PaymentSuccessQr tse={completedPaymentTse} qrPngDataUrl={qrPngFallback} size={160} />
                  {!completedPaymentTse?.qrPayload && qrPngLoading ? (
                    <Text style={styles.qrStatusHint}>RKSV-QR wird geladen…</Text>
                  ) : null}
                  {!completedPaymentTse?.qrPayload && !qrPngLoading && qrPngFetchFailed ? (
                    <Text style={styles.qrStatusHint}>
                      RKSV-QR (PNG) konnte nicht geladen werden. Die Zahlung ist gültig — nutzen Sie den Druck oder
                      wenden Sie sich an den Administrator.
                    </Text>
                  ) : null}
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
                        <ActivityIndicator size="small" color={SoftColors.accent} />
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
                  <PaymentSuccessQr tse={completedPaymentTse} qrPngDataUrl={qrPngFallback} size={140} />
                  {!completedPaymentTse?.qrPayload && qrPngLoading ? (
                    <Text style={styles.qrStatusHint}>RKSV-QR wird geladen…</Text>
                  ) : null}
                  {!completedPaymentTse?.qrPayload && !qrPngLoading && qrPngFetchFailed ? (
                    <Text style={styles.qrStatusHint}>
                      RKSV-QR (PNG) konnte nicht geladen werden. Die Zahlung ist gültig — nutzen Sie den Druck oder
                      wenden Sie sich an den Administrator.
                    </Text>
                  ) : null}
                  <View style={styles.printErrorActions}>
                    <Pressable
                      onPress={handleOpenReceiptPdf}
                      disabled={pdfLoading || !completedPaymentId}
                      style={[styles.printErrorBtnSecondary, pdfLoading && styles.payButtonDisabled]}
                    >
                      {pdfLoading ? (
                        <ActivityIndicator size="small" color={SoftColors.accent} />
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
  paymentMethodsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
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
  inputRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: SoftSpacing.sm,
    gap: SoftSpacing.sm,
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
  qrStatusHint: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    textAlign: 'center',
    paddingHorizontal: SoftSpacing.md,
    marginTop: SoftSpacing.xs,
    maxWidth: 340,
    alignSelf: 'center',
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
  payButtonDisabled: {
    // Darker than textMuted so white label stays readable (disabled Zahlen was washing out on web).
    backgroundColor: SoftColors.textSecondary,
    opacity: 0.85,
  },
  payButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
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
