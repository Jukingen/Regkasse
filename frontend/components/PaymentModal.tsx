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
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftState, SoftTypography } from '../constants/SoftTheme';
import { formatPrice } from '../utils/formatPrice';
import { useAuth } from '../contexts/AuthContext';
import { usePayment } from '../hooks/usePayment';
import { paymentService, PaymentRequest, PaymentItem } from '../services/api/paymentService';
import { isPaymentError, getPaymentErrorMessage } from '../features/payment/paymentErrors';
import { cartService } from '../services/api/cartService';
import { customerService, GUEST_CUSTOMER_ID, type BenefitEligibilityPreviewResponse } from '../services/api/customerService';
import { validateAmount } from '../utils/validation';
import { usePosCashRegisterAssignment } from '../hooks/usePosCashRegisterAssignment';
import { receiptPrinter } from '../services/receiptPrinter';
import { PaymentSuccessQr } from './PaymentSuccessQr';
import { ReceiptSummary, type ReceiptSummaryReceipt } from './ReceiptSummary';
import type { PaymentTseInfo } from '../services/api/paymentService';
import type { ReceiptDTO } from '../types/ReceiptDTO';
import type { PosPaymentMethodCode } from '../services/api/paymentService';
import { downloadInvoicePdf, InvoicePdfHttpError } from '../services/api/invoiceService';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import { resolveCashierIdForPayment } from '../utils/paymentSessionUser';
import {
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerIntro,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

/** Known blocked reason codes (must match backend BenefitBlockedReasonCodes). Used for stable German UI text only. */
const BLOCKED_REASON_DE: Record<string, string> = {
  DailyLimitReached: 'Tageslimit erreicht',
  NoEligibleItems: 'Keine passenden Artikel im Warenkorb',
  QuantityNotReached: 'Mindestmenge nicht erreicht',
};

/** Neutral German fallback when code is unknown or invalid. Never show raw code or empty. */
const BLOCKED_REASON_FALLBACK_DE = 'Vorteil derzeit nicht anwendbar';

/**
 * Map backend blocked reason to short German text for UI. Fail-safe: unknown codes and invalid input
 * return a neutral German message; backend message is not used for display (may be English).
 */
function formatBlockedReason(b: {
  blockedReasonCode?: string | null;
  message?: string | null;
  requiredMoreQuantity?: number | null;
}): string {
  const code = typeof b?.blockedReasonCode === 'string' ? b.blockedReasonCode.trim() : '';
  if (!code) return BLOCKED_REASON_FALLBACK_DE;

  if (code === 'QuantityNotReached' && typeof b?.requiredMoreQuantity === 'number' && b.requiredMoreQuantity > 0) {
    return `Noch ${b.requiredMoreQuantity} Artikel für Aktion nötig`;
  }

  const known = BLOCKED_REASON_DE[code];
  return known ?? BLOCKED_REASON_FALLBACK_DE;
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
    cashierName: r.cashierName ?? r.CashierName ?? '',
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

function isPosPaymentMethodCode(value: string): value is PosPaymentMethodCode {
  return value === 'cash' || value === 'card' || value === 'voucher' || value === 'transfer';
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
  const { user } = useAuth();
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<'cash' | 'card' | 'voucher' | 'transfer'>('cash');
  const [amountReceived, setAmountReceived] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [guestCustomerId, setGuestCustomerId] = useState<string>(GUEST_CUSTOMER_ID);
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
    processPayment,
    clearError
  } = usePayment();

  // Backend line toplamları kullan - FE hesaplama yapmaz (totalPrice = lineGross)
  const calculatedCartItems = useMemo(() => {
    return cartItems.map(item => ({
      ...item,
      lineTotal: item.totalPrice ?? (item.quantity * item.unitPrice)
    }));
  }, [cartItems]);

  const totalAmount = grandTotalGross ?? calculatedCartItems.reduce((sum, item) => sum + item.lineTotal, 0);

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
  const paySubmitDisabled =
    purchaseState === 'processing' ||
    paymentBusy ||
    isRegisterGateBlockingPayment;

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

  // Eligibility preview: only when customer selected (not guest), cart has items, and modal visible. Race-safe.
  const shouldFetchEligibility =
    visible &&
    !!customerId &&
    customerId !== '00000000-0000-0000-0000-000000000000' &&
    customerId !== GUEST_CUSTOMER_ID &&
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
          dialogTitle: 'Beleg PDF',
        });
      }
    } catch (e) {
      console.warn('[PaymentModal] PDF open failed:', e);
      if (e instanceof InvoicePdfHttpError) {
        const ok = 'Die Zahlung war erfolgreich.';
        if (e.status === 401) {
          Alert.alert(
            'Beleg-PDF',
            `${ok} Anmeldung abgelaufen oder ungültig. Bitte erneut anmelden und „Beleg-PDF“ erneut versuchen.`
          );
        } else if (e.status === 403) {
          Alert.alert(
            'Beleg-PDF',
            `${ok} Keine Berechtigung für PDF-Export. QR-Code und Druck bleiben nutzbar. Bei Bedarf den Administrator bitten (Berechtigung „Rechnung ansehen“ / InvoiceView).`
          );
        } else if (e.status === 404) {
          Alert.alert(
            'Beleg-PDF',
            `${ok} PDF wurde nicht gefunden. Bitte später erneut versuchen oder den Administrator informieren.`
          );
        } else {
          Alert.alert(
            'Beleg-PDF',
            `${ok} PDF konnte nicht geladen werden (HTTP ${e.status}). Bitte später erneut versuchen.`
          );
        }
      } else {
        Alert.alert(
          'Hinweis',
          'Die Zahlung war erfolgreich. Beleg-PDF konnte nicht geöffnet werden. Bitte später erneut versuchen.'
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
        Alert.alert('Hinweis', 'Der Warenkorb ist leer.');
        return;
      }

      if (selectedPaymentMethod === 'cash') {
        const received = parseFloat(amountReceived);
        if (isNaN(received) || received < totalAmount) {
          debugPosPaymentTrace('submit_blocked_cash_amount', { received, totalAmount });
          Alert.alert(
            'Hinweis',
            'Der erhaltene Betrag muss mindestens dem Gesamtbetrag entsprechen.'
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
        Alert.alert('Zahlung nicht möglich', registerGateAlertMessage(registerGateCtx));
        return;
      }

      if (!validateAmount(totalAmount)) {
        debugPosPaymentTrace('submit_blocked_invalid_amount', { totalAmount });
        Alert.alert('Hinweis', 'Ungültiger Betrag (muss größer als 0,01 € sein).');
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
          'Fehler',
          'Warenkorb konnte nicht geladen werden. Bitte Seite aktualisieren oder erneut versuchen.'
        );
        return;
      }

      // 3. Determine customer ID (use guest if walk-in)
      const finalCustomerId = customerId && customerId !== '00000000-0000-0000-0000-000000000000'
        ? customerId
        : guestCustomerId;

      const cashierId = await resolveCashierIdForPayment(user?.id);
      if (!user?.id && cashierId !== 'UNKNOWN') {
        debugPosPaymentTrace('submit_auth_user_null_using_jwt_cashier', { cashierId: cashierId.slice(0, 8) + '…' });
      }

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
          amount: selectedPaymentMethod === 'cash' ? parseFloat(amountReceived) : undefined
        },
        tableNumber: resolvedTableNumber,
        cashierId,
        totalAmount: totalAmount,
        cashRegisterId,
        notes: notes || `Tisch ${resolvedTableNumber} - ${new Date().toLocaleString('de-DE')}`,
        idempotencyKey
      };

      // 5. PAYMENT REQUEST (STRICT: error handling)
      setPurchaseState('processing');
      debugPosPaymentTrace('process_payment_called', {
        cashRegisterId: paymentRequest.cashRegisterId,
        cashierId: paymentRequest.cashierId,
        itemCount: paymentItems.length,
        method: paymentRequest.payment.method,
        tseRequired: paymentRequest.payment.tseRequired,
        totalAmount: paymentRequest.totalAmount,
        idempotencyKey: paymentRequest.idempotencyKey,
      });
      console.log('[PAYMENT] Request:', JSON.stringify(paymentRequest, null, 2));
      const response = await processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        debugPosPaymentTrace('payment_non_fiscal_pending_ui', { pendingQueueId: response.pendingQueueId });
        setPurchaseState('input');
        Alert.alert(
          'Hinweis',
          'Keine Verbindung zur Kasse. Die Zahlung ist nicht fiscal verbucht und steht in der Warteschlange. Bei Verbindung wird automatisch synchronisiert — bis dahin keinen Warenkorb als bezahlt werten.'
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
            ? (response.message || response.error || 'Zahlung nicht fiscal bestätigt')
            : response.message || response.error || 'Zahlung fehlgeschlagen';
        Alert.alert('Zahlung fehlgeschlagen', errorMsg);
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
          'Hinweis',
          'Zahlung erfolgreich. Die Belegabstimmung erfordert jedoch Ihre Aufmerksamkeit. Bitte prüfen Sie die Belege bzw. wenden Sie sich an den Administrator.'
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
          'Hinweis',
          'Die Zahlung war erfolgreich, der Warenkorb konnte jedoch nicht abgeschlossen werden. Bitte Administrator informieren.'
        );
      }

      // 8. CART RESET
      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
        console.log('[CART] Reset complete');
      } catch (resetErr) {
        console.warn('[CART] Reset warning:', resetErr);
        Alert.alert(
          'Warnung',
          'Zahlung abgeschlossen. Der Warenkorb konnte nicht zurückgesetzt werden. Bitte informieren Sie den Administrator.'
        );
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
        : (err instanceof Error ? err.message : 'Unbekannter Fehler bei der Zahlung.');
      const title =
        isPaymentError(err) && err.code === 'BENEFIT_DAILY_ALLOWANCE_CONFLICT'
          ? 'Hinweis'
          : isPaymentError(err) && err.code === 'DEMO_PAYMENT_RESTRICTED'
            ? 'Hinweis'
            : 'Fehler';
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
      accessibilityLabel="Zahlung"
    >
      <View style={styles.overlay} accessibilityViewIsModal>
        {/* Use View (not Pressable) so web does not swallow inner button presses. */}
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>Zahlung</Text>
            <Pressable
              onPress={handleClose}
              style={({ pressed, focused }) => [styles.closeButton, pressed && SoftState.pressed, focused && SoftState.focusVisible]}
              hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
              accessibilityLabel="Schließen"
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
                    {registerGateBannerIntro()}
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
                            • {formatBlockedReason(b)}
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
              <Text style={styles.sectionTitle}>Zahlungsart</Text>
              <View style={styles.paymentMethodsContainer}>
                {methodsLoading ? (
                  <Text style={styles.loadingText}>Zahlungsarten werden geladen…</Text>
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
                          if (isPosPaymentMethodCode(method.type)) {
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
                    <Text style={styles.errorText}>Keine Zahlungsart gefunden</Text>
                    <Pressable onPress={getPaymentMethods} style={styles.retryLink}>
                      <Text style={styles.retryLinkText}>Erneut versuchen</Text>
                    </Pressable>
                  </View>
                )}
              </View>
            </View>

            {/* Step 3: Nakit – Betrag & Rückgeld */}
            {selectedPaymentMethod === 'cash' && (
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
                      {formatPrice(totalAmount)} zahlen
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
