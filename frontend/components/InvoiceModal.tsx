import React, { useState, useEffect } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Alert,
  ActivityIndicator,
  Switch,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { Invoice, InvoiceItem, invoiceService } from '../services/api/invoiceService';
import { Product } from '../services/api/productService';

interface InvoiceModalProps {
  visible: boolean;
  onClose: () => void;
  onInvoiceCreated: (invoice: Invoice) => void;
  products: Product[];
  customerId?: string;
  customerName?: string;
  customerEmail?: string;
  customerTaxNumber?: string;
  customerVatNumber?: string;
}

const InvoiceModal: React.FC<InvoiceModalProps> = ({
  visible,
  onClose,
  onInvoiceCreated,
  products,
  customerId,
  customerName,
  customerEmail,
  customerTaxNumber,
  customerVatNumber,
}) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [selectedItems, setSelectedItems] = useState<InvoiceItem[]>([]);
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [quantity, setQuantity] = useState('1');
  const [discountAmount, setDiscountAmount] = useState('0');
  const [invoiceType, setInvoiceType] = useState<'standard' | 'proforma'>('standard');
  const [paymentMethod, setPaymentMethod] = useState<'cash' | 'card' | 'voucher' | 'mixed'>('cash');
  const [dueDate, setDueDate] = useState('');
  const [notes, setNotes] = useState('');
  const [showCustomerForm, setShowCustomerForm] = useState(false);
  const [customerForm, setCustomerForm] = useState({
    name: customerName || '',
    email: customerEmail || '',
    taxNumber: customerTaxNumber || '',
    vatNumber: customerVatNumber || '',
  });

  // Vade tarihini varsayılan olarak 30 gün sonra ayarla
  useEffect(() => {
    const defaultDueDate = new Date();
    defaultDueDate.setDate(defaultDueDate.getDate() + 30);
    setDueDate(defaultDueDate.toISOString().split('T')[0]);
  }, []);

  // Toplam hesapla
  const calculateTotals = () => {
    const subtotal = selectedItems.reduce((sum, item) => sum + item.totalAmount, 0);
    const totalDiscount = selectedItems.reduce((sum, item) => sum + item.discountAmount, 0);
    const taxStandard = selectedItems
      .filter(item => item.taxType === 'standard')
      .reduce((sum, item) => sum + item.taxAmount, 0);
    const taxReduced = selectedItems
      .filter(item => item.taxType === 'reduced')
      .reduce((sum, item) => sum + item.taxAmount, 0);
    const taxSpecial = selectedItems
      .filter(item => item.taxType === 'special')
      .reduce((sum, item) => sum + item.taxAmount, 0);
    const total = subtotal + taxStandard + taxReduced + taxSpecial;
    
    return { subtotal, totalDiscount, taxStandard, taxReduced, taxSpecial, total };
  };

  // Ürün ekle
  const addItem = () => {
    if (!selectedProduct) {
      Alert.alert(t('invoice.error.title'), t('invoice.error.select_product'));
      return;
    }

    const qty = parseFloat(quantity);
    const discount = parseFloat(discountAmount);
    
    if (isNaN(qty) || qty <= 0) {
      Alert.alert(t('invoice.error.title'), t('invoice.error.invalid_quantity'));
      return;
    }

    if (qty > selectedProduct.stock) {
      Alert.alert(t('invoice.error.title'), t('invoice.error.insufficient_stock'));
      return;
    }

    const unitPrice = selectedProduct.price;
    const itemTotal = (unitPrice * qty) - discount;
    const taxRate = selectedProduct.taxType === 'standard' ? 0.20 : 
                   selectedProduct.taxType === 'reduced' ? 0.10 : 0.13;
    const taxAmount = itemTotal * taxRate;

    const newItem: InvoiceItem = {
      productId: selectedProduct.id,
      productName: selectedProduct.name,
      quantity: qty,
      unitPrice,
      discountAmount: discount,
      taxType: selectedProduct.taxType,
      taxAmount,
      totalAmount: itemTotal + taxAmount,
    };

    setSelectedItems([...selectedItems, newItem]);
    setSelectedProduct(null);
    setQuantity('1');
    setDiscountAmount('0');
  };

  // Ürün çıkar
  const removeItem = (index: number) => {
    setSelectedItems(selectedItems.filter((_, i) => i !== index));
  };

  // Fatura oluştur
  const createInvoice = async () => {
    if (selectedItems.length === 0) {
      Alert.alert(t('invoice.error.title'), t('invoice.error.no_items'));
      return;
    }

    if (!dueDate) {
      Alert.alert(t('invoice.error.title'), t('invoice.error.select_due_date'));
      return;
    }

    setLoading(true);

    try {
      const totals = calculateTotals();
      
      const invoiceData = {
        customerId: customerId || undefined,
        customerName: customerForm.name || customerName,
        customerEmail: customerForm.email || customerEmail,
        customerTaxNumber: customerForm.taxNumber || customerTaxNumber,
        customerVatNumber: customerForm.vatNumber || customerVatNumber,
        items: selectedItems,
        subtotal: totals.subtotal,
        discountAmount: totals.totalDiscount,
        taxStandard: totals.taxStandard,
        taxReduced: totals.taxReduced,
        taxSpecial: totals.taxSpecial,
        totalAmount: totals.total,
        paymentMethod,
        paymentStatus: 'pending' as const,
        invoiceStatus: 'draft' as const,
        invoiceType,
        dueDate,
        issueDate: new Date().toISOString(),
        notes,
        isPrinted: false,
        isEmailed: false,
      };

      const invoice = await invoiceService.createInvoice(invoiceData);
      
      Alert.alert(
        t('invoice.success.title'),
        t('invoice.success.created'),
        [
          {
            text: t('common.ok'),
            onPress: () => {
              onInvoiceCreated(invoice);
              onClose();
              resetForm();
            }
          }
        ]
      );
    } catch (error) {
      console.error('Invoice creation failed:', error);
      Alert.alert(
        t('invoice.error.title'),
        t('invoice.error.creation_failed'),
        [{ text: t('common.ok') }]
      );
    } finally {
      setLoading(false);
    }
  };

  // Formu sıfırla
  const resetForm = () => {
    setSelectedItems([]);
    setSelectedProduct(null);
    setQuantity('1');
    setDiscountAmount('0');
    setInvoiceType('standard');
    setPaymentMethod('cash');
    setNotes('');
    setCustomerForm({
      name: customerName || '',
      email: customerEmail || '',
      taxNumber: customerTaxNumber || '',
      vatNumber: customerVatNumber || '',
    });
  };

  const totals = calculateTotals();

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('invoice.create')}</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.content}>
            {/* Müşteri Bilgileri */}
            <View style={styles.section}>
              <View style={styles.sectionHeader}>
                <Text style={styles.sectionTitle}>{t('invoice.customer_info')}</Text>
                <Switch
                  value={showCustomerForm}
                  onValueChange={setShowCustomerForm}
                />
              </View>
              
              {showCustomerForm ? (
                <View style={styles.customerForm}>
                  <TextInput
                    style={styles.input}
                    placeholder={t('invoice.customer_name')}
                    value={customerForm.name}
                    onChangeText={(text) => setCustomerForm({...customerForm, name: text})}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder={t('invoice.customer_email')}
                    value={customerForm.email}
                    onChangeText={(text) => setCustomerForm({...customerForm, email: text})}
                    keyboardType="email-address"
                  />
                  <TextInput
                    style={styles.input}
                    placeholder={t('invoice.tax_number')}
                    value={customerForm.taxNumber}
                    onChangeText={(text) => setCustomerForm({...customerForm, taxNumber: text})}
                  />
                  <TextInput
                    style={styles.input}
                    placeholder={t('invoice.vat_number')}
                    value={customerForm.vatNumber}
                    onChangeText={(text) => setCustomerForm({...customerForm, vatNumber: text})}
                  />
                </View>
              ) : (
                <View style={styles.customerInfo}>
                  <Text style={styles.customerText}>
                    {customerName || t('invoice.no_customer')}
                  </Text>
                  {customerEmail && (
                    <Text style={styles.customerText}>{customerEmail}</Text>
                  )}
                </View>
              )}
            </View>

            {/* Fatura Ayarları */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.settings')}</Text>
              
              <View style={styles.settingRow}>
                <Text style={styles.settingLabel}>{t('invoice.type')}</Text>
                <View style={styles.settingOptions}>
                  <TouchableOpacity
                    style={[
                      styles.optionButton,
                      invoiceType === 'standard' && styles.optionButtonSelected
                    ]}
                    onPress={() => setInvoiceType('standard')}
                  >
                    <Text style={[
                      styles.optionText,
                      invoiceType === 'standard' && styles.optionTextSelected
                    ]}>
                      {t('invoice.standard')}
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[
                      styles.optionButton,
                      invoiceType === 'proforma' && styles.optionButtonSelected
                    ]}
                    onPress={() => setInvoiceType('proforma')}
                  >
                    <Text style={[
                      styles.optionText,
                      invoiceType === 'proforma' && styles.optionTextSelected
                    ]}>
                      {t('invoice.proforma')}
                    </Text>
                  </TouchableOpacity>
                </View>
              </View>

              <View style={styles.settingRow}>
                <Text style={styles.settingLabel}>{t('invoice.payment_method')}</Text>
                <View style={styles.settingOptions}>
                  {['cash', 'card', 'voucher', 'mixed'].map((method) => (
                    <TouchableOpacity
                      key={method}
                      style={[
                        styles.optionButton,
                        paymentMethod === method && styles.optionButtonSelected
                      ]}
                      onPress={() => setPaymentMethod(method as any)}
                    >
                      <Text style={[
                        styles.optionText,
                        paymentMethod === method && styles.optionTextSelected
                      ]}>
                        {t(`payment.${method}`)}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>

              <View style={styles.settingRow}>
                <Text style={styles.settingLabel}>{t('invoice.due_date')}</Text>
                <TextInput
                  style={styles.dateInput}
                  value={dueDate}
                  onChangeText={setDueDate}
                  placeholder="YYYY-MM-DD"
                />
              </View>
            </View>

            {/* Ürün Ekleme */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.add_items')}</Text>
              
              <View style={styles.productSelector}>
                <Text style={styles.label}>{t('invoice.select_product')}</Text>
                <ScrollView horizontal style={styles.productList}>
                  {products.map((product) => (
                    <TouchableOpacity
                      key={product.id}
                      style={[
                        styles.productItem,
                        selectedProduct?.id === product.id && styles.productItemSelected
                      ]}
                      onPress={() => setSelectedProduct(product)}
                    >
                      <Text style={styles.productName}>{product.name}</Text>
                      <Text style={styles.productPrice}>{product.price.toFixed(2)}€</Text>
                      <Text style={styles.productStock}>
                        {t('product.stock')}: {product.stock}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </ScrollView>
              </View>

              {selectedProduct && (
                <View style={styles.itemForm}>
                  <View style={styles.formRow}>
                    <Text style={styles.label}>{t('invoice.quantity')}</Text>
                    <TextInput
                      style={styles.numberInput}
                      value={quantity}
                      onChangeText={setQuantity}
                      keyboardType="numeric"
                    />
                  </View>
                  
                  <View style={styles.formRow}>
                    <Text style={styles.label}>{t('invoice.discount')}</Text>
                    <TextInput
                      style={styles.numberInput}
                      value={discountAmount}
                      onChangeText={setDiscountAmount}
                      keyboardType="numeric"
                    />
                  </View>
                  
                  <TouchableOpacity
                    style={styles.addButton}
                    onPress={addItem}
                  >
                    <Ionicons name="add" size={20} color="white" />
                    <Text style={styles.addButtonText}>{t('invoice.add_item')}</Text>
                  </TouchableOpacity>
                </View>
              )}
            </View>

            {/* Seçili Ürünler */}
            {selectedItems.length > 0 && (
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>{t('invoice.selected_items')}</Text>
                
                {selectedItems.map((item, index) => (
                  <View key={index} style={styles.itemRow}>
                    <View style={styles.itemInfo}>
                      <Text style={styles.itemName}>{item.productName}</Text>
                      <Text style={styles.itemDetails}>
                        {item.quantity} x {item.unitPrice.toFixed(2)}€
                        {item.discountAmount > 0 && ` - ${item.discountAmount.toFixed(2)}€`}
                      </Text>
                    </View>
                    <View style={styles.itemActions}>
                      <Text style={styles.itemTotal}>{item.totalAmount.toFixed(2)}€</Text>
                      <TouchableOpacity
                        onPress={() => removeItem(index)}
                        style={styles.removeButton}
                      >
                        <Ionicons name="trash-outline" size={16} color="#F44336" />
                      </TouchableOpacity>
                    </View>
                  </View>
                ))}
              </View>
            )}

            {/* Notlar */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.notes')}</Text>
              <TextInput
                style={styles.notesInput}
                value={notes}
                onChangeText={setNotes}
                placeholder={t('invoice.notes_placeholder')}
                multiline
                numberOfLines={3}
              />
            </View>

            {/* Toplam */}
            {selectedItems.length > 0 && (
              <View style={styles.totalsSection}>
                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>{t('invoice.subtotal')}</Text>
                  <Text style={styles.totalValue}>{totals.subtotal.toFixed(2)}€</Text>
                </View>
                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>{t('invoice.tax_standard')}</Text>
                  <Text style={styles.totalValue}>{totals.taxStandard.toFixed(2)}€</Text>
                </View>
                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>{t('invoice.tax_reduced')}</Text>
                  <Text style={styles.totalValue}>{totals.taxReduced.toFixed(2)}€</Text>
                </View>
                <View style={styles.totalRow}>
                  <Text style={styles.totalLabel}>{t('invoice.tax_special')}</Text>
                  <Text style={styles.totalValue}>{totals.taxSpecial.toFixed(2)}€</Text>
                </View>
                <View style={[styles.totalRow, styles.grandTotal]}>
                  <Text style={styles.grandTotalLabel}>{t('invoice.total')}</Text>
                  <Text style={styles.grandTotalValue}>{totals.total.toFixed(2)}€</Text>
                </View>
              </View>
            )}
          </ScrollView>

          {/* Butonlar */}
          <View style={styles.footer}>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={onClose}
              disabled={loading}
            >
              <Text style={styles.cancelButtonText}>{t('common.cancel')}</Text>
            </TouchableOpacity>
            
            <TouchableOpacity
              style={[styles.createButton, loading && styles.createButtonDisabled]}
              onPress={createInvoice}
              disabled={loading || selectedItems.length === 0}
            >
              {loading ? (
                <ActivityIndicator color="white" />
              ) : (
                <>
                  <Ionicons name="document-text" size={20} color="white" />
                  <Text style={styles.createButtonText}>{t('invoice.create')}</Text>
                </>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modal: {
    backgroundColor: 'white',
    borderRadius: 16,
    width: '95%',
    height: '90%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
  },
  closeButton: {
    padding: 4,
  },
  content: {
    flex: 1,
    padding: 20,
  },
  section: {
    marginBottom: 24,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 12,
  },
  customerForm: {
    gap: 12,
  },
  customerInfo: {
    padding: 12,
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
  },
  customerText: {
    fontSize: 14,
    color: '#666',
  },
  input: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
  },
  settingRow: {
    marginBottom: 16,
  },
  settingLabel: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 8,
  },
  settingOptions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  optionButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
  },
  optionButtonSelected: {
    borderColor: '#2196F3',
    backgroundColor: '#f3f8ff',
  },
  optionText: {
    fontSize: 14,
    color: '#666',
  },
  optionTextSelected: {
    color: '#2196F3',
    fontWeight: '600',
  },
  dateInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
  },
  productSelector: {
    marginBottom: 16,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 8,
  },
  productList: {
    maxHeight: 120,
  },
  productItem: {
    padding: 12,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    marginRight: 8,
    minWidth: 120,
  },
  productItemSelected: {
    borderColor: '#4CAF50',
    backgroundColor: '#f0f8f0',
  },
  productName: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 4,
  },
  productPrice: {
    fontSize: 16,
    fontWeight: '700',
    color: '#2196F3',
    marginBottom: 4,
  },
  productStock: {
    fontSize: 12,
    color: '#666',
  },
  itemForm: {
    gap: 12,
  },
  formRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  numberInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    width: 100,
    textAlign: 'center',
  },
  addButton: {
    backgroundColor: '#4CAF50',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 12,
    borderRadius: 8,
    gap: 8,
  },
  addButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  itemRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 12,
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    marginBottom: 8,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '500',
    marginBottom: 4,
  },
  itemDetails: {
    fontSize: 14,
    color: '#666',
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  itemTotal: {
    fontSize: 16,
    fontWeight: '600',
    color: '#2196F3',
  },
  removeButton: {
    padding: 4,
  },
  notesInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    minHeight: 80,
    textAlignVertical: 'top',
  },
  totalsSection: {
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    padding: 16,
    marginBottom: 16,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  totalLabel: {
    fontSize: 14,
    color: '#666',
  },
  totalValue: {
    fontSize: 14,
    fontWeight: '500',
  },
  grandTotal: {
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    paddingTop: 8,
    marginTop: 8,
  },
  grandTotalLabel: {
    fontSize: 18,
    fontWeight: '600',
  },
  grandTotalValue: {
    fontSize: 18,
    fontWeight: '600',
    color: '#2196F3',
  },
  footer: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    gap: 12,
  },
  cancelButton: {
    flex: 1,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
  },
  createButton: {
    flex: 2,
    backgroundColor: '#2196F3',
    padding: 16,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  createButtonDisabled: {
    backgroundColor: '#ccc',
  },
  createButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: 'white',
  },
});

export default InvoiceModal; 