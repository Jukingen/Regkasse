import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useMemo } from 'react';
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
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftState, SoftTypography } from '../constants/SoftTheme';
import { formatPrice } from '../utils/formatPrice';
import { useAuth } from '../contexts/AuthContext';
import { usePayment } from '../hooks/usePayment';
import { paymentService, PaymentRequest, PaymentItem } from '../services/api/paymentService';
import { cartService } from '../services/api/cartService';
import { customerService, GUEST_CUSTOMER_ID } from '../services/api/customerService';
import { validateSteuernummer, validateKassenId, validateAmount } from '../utils/validation';
import { receiptPrinter } from '../services/receiptPrinter';
import { PaymentSuccessQr } from './PaymentSuccessQr';
import { ReceiptSummary, type ReceiptSummaryReceipt } from './ReceiptSummary';
import type { PaymentTseInfo } from '../services/api/paymentService';
import type { ReceiptDTO } from '../types/ReceiptDTO';

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
    taxType?: string;
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
  /** Ödeme sonrası fiş verisi – GET /Payment/{id}/receipt */
  const [receiptData, setReceiptData] = useState<ReceiptDTO | null>(null);

  // DEV: TSE Simulation Toggle
  // Default: AÇIK (Bypass) in Development
  const [isTseSimulationEnabled, setIsTseSimulationEnabled] = useState<boolean>(__DEV__);

  const {
    loading,
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

  // Load payment methods and guest customer
  useEffect(() => {
    if (visible) {
      getPaymentMethods();

      // Load guest customer ID
      customerService.getGuestCustomer()
        .then(id => setGuestCustomerId(id))
        .catch(err => console.warn('[PaymentModal] Failed to load guest customer:', err));
    }
  }, [visible, getPaymentMethods]);

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

  // Handler for preset buttons
  const handlePresetPress = (amount: number) => {
    setAmountReceived(amount.toString());
  };

  // Ödeme işlemi
  const handlePayment = async () => {
    try {
      // 1. Validasyonlar
      if (!tableNumber) {
        Alert.alert('Hata', 'Masa numarası gerekli');
        return;
      }

      if (cartItems.length === 0) {
        Alert.alert('Hata', 'Sepet boş');
        return;
      }

      if (selectedPaymentMethod === 'cash') {
        const received = parseFloat(amountReceived);
        if (isNaN(received) || received < totalAmount) {
          Alert.alert('Hata', 'Alınan tutar toplam tutardan az olamaz');
          return;
        }
      }

      // Avusturya yasal gereksinimleri validasyonu
      const steuernummer = 'ATU12345678';
      const kassenId = 'KASSE-001';

      if (!validateSteuernummer(steuernummer)) {
        Alert.alert('Hata', 'Geçersiz Steuernummer formatı (ATU12345678)');
        return;
      }
      if (!validateKassenId(kassenId)) {
        Alert.alert('Hata', 'Geçersiz KassenId (3-50 karakter)');
        return;
      }
      if (!validateAmount(totalAmount)) {
        Alert.alert('Hata', 'Geçersiz tutar (0.01\'den büyük olmalı)');
        return;
      }

      // 2. Aktif sepet ID'sini al (Source of Truth for Cart ID)
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(tableNumber);

        // Eğer backend cart boş geliyorsa ama frontend doluysa, frontend items kullanmaya devam et
        // Ancak cartId'ye ihtiyacımız var complete için.
        if (!cart || !cart.cartId) {
          throw new Error('Aktif masa sepeti bulunamadı.');
        }
        currentCartId = cart.cartId;
      } catch (cartErr) {
        console.error('Cart fetch failed:', cartErr);
        Alert.alert('Hata', 'Masa sepet bilgisi alınamadı. Lütfen sayfayı yenileyin.');
        return;
      }

      // 3. Determine customer ID (use guest if walk-in)
      const finalCustomerId = customerId && customerId !== '00000000-0000-0000-0000-000000000000'
        ? customerId
        : guestCustomerId;

      // 4. Build payment request: flat items (one PaymentItem per cart line). Phase D: no modifierIds emission; add-ons = product lines only.
      // Guard: flat items only — do not add modifierIds or modifiers (one item per cart line).
      const paymentItems: PaymentItem[] = cartItems.map(item => ({
        productId: item.productId,
        quantity: (item as any).qty ?? item.quantity,
        taxType: (item.taxType as 'standard' | 'reduced' | 'special') || 'standard'
      }));

      // NOTE: TSE Logic
      // If Simulation Enabled (Bypass) -> tseRequired: false
      // If Simulation Disabled (Real)  -> tseRequired: true
      // In PROD, always true
      const shouldRequireTse = __DEV__ ? !isTseSimulationEnabled : true;

      const paymentRequest: PaymentRequest = {
        customerId: finalCustomerId, // Always send valid customer ID (guest or registered)
        items: paymentItems,
        payment: {
          method: selectedPaymentMethod as 'cash' | 'card' | 'voucher',
          tseRequired: shouldRequireTse,
          amount: selectedPaymentMethod === 'cash' ? parseFloat(amountReceived) : undefined
        },
        tableNumber: tableNumber || 1,
        cashierId: user?.id || 'UNKNOWN', // Use logged-in user ID
        totalAmount: totalAmount,
        steuernummer: steuernummer,
        kassenId: kassenId,
        notes: notes || `Masa ${tableNumber} - ${new Date().toLocaleString('de-DE')}`
      };

      // 5. PAYMENT REQUEST (STRICT: error handling)
      setPurchaseState('processing');
      console.log('[PAYMENT] Request:', JSON.stringify(paymentRequest, null, 2));
      const response = await processPayment(paymentRequest);

      // 6. STRICT SUCCESS CHECK (Do NOT proceed if payment failed)
      if (!response.success) {
        console.error('[PAYMENT] Failed:', response);
        const errorMsg = response.message || response.error || 'Ödeme işlemi başarısız';
        Alert.alert('Ödeme Başarısız', errorMsg);
        setPurchaseState('input'); // Reset to input on failure
        return; // CRITICAL: Do NOT proceed to complete/reset
      }

      console.log('[PAYMENT] Success, paymentId:', response.paymentId);
      setCompletedPaymentId(response.paymentId);
      setCompletedPaymentTse(response.tse ?? null);

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
        Alert.alert('Uyarı', 'Ödeme alındı ancak sipariş kapatılamadı. Lütfen yöneticiye bildirin.');
      }

      // 8. CART RESET
      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
        console.log('[CART] Reset complete');
      } catch (resetErr) {
        console.warn('[CART] Reset warning:', resetErr);
      }

      // 9. START PRINTING (QR backend /api/Payment/{id}/qr.png'den base64 embed)
      setPurchaseState('printing');
      try {
        await receiptPrinter.print(response.paymentId, {
          isDemoFiscal: response.tse?.isDemoFiscal ?? false,
        });
        setPurchaseState('completed');
        // Auto close after meaningful delay
        setTimeout(() => {
          handleSuccessAndClose(response.paymentId);
        }, 2000);
      } catch (printErr) {
        console.error('[PRINT] Failed:', printErr);
        setPurchaseState('print_error');
        // User will now see "Retry" or "Skip" buttons
      }

    } catch (err) {
      console.error('Handle Payment Error:', err);
      Alert.alert('Hata', err instanceof Error ? err.message : 'Bilinmeyen bir hata oluştu');
      setPurchaseState('input');
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
      setTimeout(() => {
        handleSuccessAndClose(completedPaymentId);
      }, 2000);
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
    onClose();
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={handleClose}
      accessibilityRole="dialog"
      accessibilityLabel="Ödeme"
    >
      <View style={styles.overlay} accessibilityViewIsModal>
        <Pressable style={styles.modal} onPress={undefined}>
          <View style={styles.header}>
            <Text style={styles.title}>Ödeme</Text>
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
                        onPress={() => setSelectedPaymentMethod(method.type as any)}
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
              <Pressable
                onPress={handleClose}
                style={({ pressed, focused }) => [
                  styles.cancelButton,
                  pressed && SoftState.pressed,
                  focused && SoftState.focusVisible,
                ]}
                disabled={purchaseState === 'processing'}
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
                  purchaseState === 'processing' && styles.payButtonDisabled,
                  pressed && purchaseState !== 'processing' && SoftState.pressedScale,
                  focused && purchaseState !== 'processing' && SoftState.focusVisible,
                ]}
                disabled={purchaseState === 'processing'}
                hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                accessibilityLabel={`Zahlen ${formatPrice(totalAmount)}`}
                accessibilityRole="button"
                accessibilityState={{ disabled: purchaseState === 'processing' }}
              >
                {purchaseState === 'processing' ? (
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
                    // Receipt: GET /Payment/{id}/receipt. TODO: use payment response.receipt/totals/vatBreakdown when backend returns them.
                    const summaryReceipt = toSummaryReceipt(receiptData ?? null);
                    return summaryReceipt ? (
                      <View style={[styles.receiptPreview, { marginTop: SoftSpacing.sm, maxHeight: 320 }]}>
                        <ReceiptSummary receipt={summaryReceipt} mode="cashier" />
                      </View>
                    ) : null;
                  })()}
                  <PaymentSuccessQr tse={completedPaymentTse} size={160} />
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
                  <PaymentSuccessQr tse={completedPaymentTse} size={140} />
                  <View style={styles.printErrorActions}>
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
        </Pressable>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: SoftColors.bgCard,
    borderTopLeftRadius: SoftRadius.xl,
    borderTopRightRadius: SoftRadius.xl,
    maxHeight: '90%',
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
    flexDirection: 'row',
    gap: SoftSpacing.sm,
    padding: SoftSpacing.md,
    borderTopWidth: 1,
    borderTopColor: SoftColors.borderLight,
    backgroundColor: SoftColors.bgCard,
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
    backgroundColor: SoftColors.textMuted,
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
}); 
