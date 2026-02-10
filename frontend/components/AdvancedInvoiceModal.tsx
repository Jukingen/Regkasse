import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Alert,
  Vibration,
  Dimensions,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { CartItem } from '../types/cart';

interface InvoiceCustomer {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
}

interface InvoiceSettings {
  dueDate: Date;
  notes: string;
  termsAndConditions: string;
  autoSend: boolean;
  sendToEmail: string;
}

interface AdvancedInvoiceModalProps {
  visible: boolean;
  onClose: () => void;
  onInvoiceCreate: (invoiceData: any) => void;
  cart: CartItem[];
  totalAmount: number;
  taxAmount: number;
  subtotal: number;
}

const { width: screenWidth } = Dimensions.get('window');

const AdvancedInvoiceModal: React.FC<AdvancedInvoiceModalProps> = ({
  visible,
  onClose,
  onInvoiceCreate,
  cart,
  totalAmount,
  taxAmount,
  subtotal,
}) => {
  const { t } = useTranslation();
  const [activeStep, setActiveStep] = useState(1);
  const [customer, setCustomer] = useState<InvoiceCustomer>({
    name: '',
    email: '',
    phone: '',
    address: '',
    taxNumber: '',
  });
  const [settings, setSettings] = useState<InvoiceSettings>({
    dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000), // 30 gün sonra
    notes: '',
    termsAndConditions: 'Payment is due within 30 days of invoice date.',
    autoSend: false,
    sendToEmail: '',
  });
  const [isCreating, setIsCreating] = useState(false);

  // Müşteri bilgilerini güncelle
  const updateCustomer = (field: keyof InvoiceCustomer, value: string) => {
    setCustomer(prev => ({ ...prev, [field]: value }));
  };

  // Ayarları güncelle
  const updateSettings = (field: keyof InvoiceSettings, value: any) => {
    setSettings(prev => ({ ...prev, [field]: value }));
  };

  // Sonraki adım
  const nextStep = () => {
    if (activeStep < 3) {
      setActiveStep(activeStep + 1);
      Vibration.vibrate(50);
    }
  };

  // Önceki adım
  const prevStep = () => {
    if (activeStep > 1) {
      setActiveStep(activeStep - 1);
      Vibration.vibrate(50);
    }
  };

  // Fatura oluştur
  const handleCreateInvoice = async () => {
    if (!customer.name.trim()) {
      Alert.alert(t('invoice.error', 'Error'), t('invoice.customerNameRequired', 'Customer name is required'));
      return;
    }

    if (settings.autoSend && !settings.sendToEmail.trim()) {
      Alert.alert(t('invoice.error', 'Error'), t('invoice.emailRequired', 'Email is required for auto-send'));
      return;
    }

    setIsCreating(true);

    try {
      const invoiceData = {
        customer,
        settings,
        items: cart?.items?.map(item => ({
          productId: item.product.id,
          productName: item.product.name,
          description: item.product.description || '',
          quantity: item.quantity,
          unitPrice: item.product.price,
          taxRate: getTaxRate(item.product.taxType),
          taxAmount: (item.product.price * item.quantity * getTaxRate(item.product.taxType)),
          totalAmount: item.product.price * item.quantity,
          taxType: item.product.taxType,
        })),
        subtotal,
        taxAmount,
        totalAmount,
        dueDate: settings.dueDate,
        notes: settings.notes,
        termsAndConditions: settings.termsAndConditions,
        autoSend: settings.autoSend,
        sendToEmail: settings.sendToEmail,
      };

      await onInvoiceCreate(invoiceData);
      
      Alert.alert(
        t('invoice.success', 'Success'),
        t('invoice.created', 'Invoice created successfully!'),
        [{ text: t('common.ok', 'OK'), onPress: handleClose }]
      );
    } catch (error) {
      Alert.alert(t('invoice.error', 'Error'), t('invoice.createFailed', 'Failed to create invoice. Please try again.'));
    } finally {
      setIsCreating(false);
    }
  };

  const handleClose = () => {
    setActiveStep(1);
    setCustomer({
      name: '',
      email: '',
      phone: '',
      address: '',
      taxNumber: '',
    });
    setSettings({
      dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
      notes: '',
      termsAndConditions: 'Payment is due within 30 days of invoice date.',
      autoSend: false,
      sendToEmail: '',
    });
    onClose();
  };

  const getTaxRate = (taxType: string) => {
    switch (taxType) {
      case 'standard': return 0.20;
      case 'reduced': return 0.10;
      case 'special': return 0.13;
      default: return 0.20;
    }
  };

  const formatDate = (date: Date) => {
    return date.toLocaleDateString('de-DE');
  };

  const renderStepIndicator = () => (
    <View style={styles.stepIndicator}>
      {[1, 2, 3].map(step => (
        <View key={step} style={styles.stepContainer}>
          <View style={[
            styles.stepCircle,
            activeStep >= step && styles.stepCircleActive
          ]}>
            <Text style={[
              styles.stepNumber,
              activeStep >= step && styles.stepNumberActive
            ]}>
              {step}
            </Text>
          </View>
          <Text style={[
            styles.stepLabel,
            activeStep >= step && styles.stepLabelActive
          ]}>
            {step === 1 ? t('invoice.customer', 'Customer') : step === 2 ? t('invoice.items', 'Items') : t('invoice.settings', 'Settings')}
          </Text>
        </View>
      ))}
    </View>
  );

  const renderCustomerStep = () => (
    <View style={styles.stepContent}>
      <Text style={styles.stepTitle}>{t('invoice.customerInfo', 'Customer Information')}</Text>
      
      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.customerName', 'Customer Name *')}</Text>
        <TextInput
          style={styles.textInput}
          value={customer.name}
          onChangeText={(value) => updateCustomer('name', value)}
          placeholder={t('invoice.enterCustomerName', 'Enter customer name')}
        />
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.email', 'Email')}</Text>
        <TextInput
          style={styles.textInput}
          value={customer.email}
          onChangeText={(value) => updateCustomer('email', value)}
          placeholder={t('invoice.emailPlaceholder', 'customer@example.com')}
          keyboardType="email-address"
        />
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.phone', 'Phone')}</Text>
        <TextInput
          style={styles.textInput}
          value={customer.phone}
          onChangeText={(value) => updateCustomer('phone', value)}
          placeholder={t('invoice.phonePlaceholder', '+43 123 456 789')}
          keyboardType="phone-pad"
        />
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.address', 'Address')}</Text>
        <TextInput
          style={[styles.textInput, styles.textArea]}
          value={customer.address}
          onChangeText={(value) => updateCustomer('address', value)}
          placeholder={t('invoice.enterAddress', 'Enter full address')}
          multiline
          numberOfLines={3}
        />
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.taxNumber', 'Tax Number (ATU)')}</Text>
        <TextInput
          style={styles.textInput}
          value={customer.taxNumber}
          onChangeText={(value) => updateCustomer('taxNumber', value)}
          placeholder={t('invoice.taxNumberPlaceholder', 'ATU12345678')}
          autoCapitalize="characters"
        />
      </View>
    </View>
  );

  const renderItemsStep = () => (
    <View style={styles.stepContent}>
      <Text style={styles.stepTitle}>{t('invoice.itemsTitle', 'Invoice Items')}</Text>
      
      <View style={styles.itemsContainer}>
        {cart?.items?.map((item, index) => (
          <View key={index} style={styles.itemRow}>
            <View style={styles.itemInfo}>
              <Text style={styles.itemName}>{item.product.name}</Text>
              <Text style={styles.itemDetails}>
                {item.quantity} x €{item.product.price.toFixed(2)} = €{(item.quantity * item.product.price).toFixed(2)}
              </Text>
            </View>
            <View style={styles.itemTax}>
              <Text style={styles.taxLabel}>{item.product.taxType.toUpperCase()}</Text>
              <Text style={styles.taxRate}>{Math.round(getTaxRate(item.product.taxType) * 100)}%</Text>
            </View>
          </View>
        ))}
      </View>

      <View style={styles.summaryContainer}>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>{t('invoice.subtotal', 'Subtotal:')}</Text>
          <Text style={styles.summaryValue}>€{subtotal.toFixed(2)}</Text>
        </View>
        <View style={styles.summaryRow}>
          <Text style={styles.summaryLabel}>{t('invoice.tax', 'Tax:')}</Text>
          <Text style={styles.summaryValue}>€{taxAmount.toFixed(2)}</Text>
        </View>
        <View style={[styles.summaryRow, styles.totalRow]}>
          <Text style={styles.totalLabel}>{t('invoice.total', 'Total:')}</Text>
          <Text style={styles.totalValue}>€{totalAmount.toFixed(2)}</Text>
        </View>
      </View>
    </View>
  );

  const renderSettingsStep = () => (
    <View style={styles.stepContent}>
      <Text style={styles.stepTitle}>{t('invoice.settingsTitle', 'Invoice Settings')}</Text>
      
      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.dueDate', 'Due Date')}</Text>
        <TouchableOpacity
          style={styles.dateButton}
          onPress={() => {
            // Date picker burada implement edilecek
            Alert.alert(t('invoice.datePicker', 'Date Picker'), t('invoice.datePickerMsg', 'Date picker will be implemented'));
          }}
        >
          <Text style={styles.dateButtonText}>{formatDate(settings.dueDate)}</Text>
          <Ionicons name="calendar" size={20} color={Colors.light.primary} />
        </TouchableOpacity>
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.notes', 'Notes')}</Text>
        <TextInput
          style={[styles.textInput, styles.textArea]}
          value={settings.notes}
          onChangeText={(value) => updateSettings('notes', value)}
          placeholder={t('invoice.notesPlaceholder', 'Additional notes for the customer')}
          multiline
          numberOfLines={3}
        />
      </View>

      <View style={styles.inputGroup}>
        <Text style={styles.inputLabel}>{t('invoice.terms', 'Terms & Conditions')}</Text>
        <TextInput
          style={[styles.textInput, styles.textArea]}
          value={settings.termsAndConditions}
          onChangeText={(value) => updateSettings('termsAndConditions', value)}
          placeholder={t('invoice.termsPlaceholder', 'Payment terms and conditions')}
          multiline
          numberOfLines={4}
        />
      </View>

      <View style={styles.autoSendContainer}>
        <TouchableOpacity
          style={styles.checkboxContainer}
          onPress={() => updateSettings('autoSend', !settings.autoSend)}
        >
          <View style={[
            styles.checkbox,
            settings.autoSend && styles.checkboxChecked
          ]}>
            {settings.autoSend && (
              <Ionicons name="checkmark" size={16} color="white" />
            )}
          </View>
          <Text style={styles.checkboxLabel}>{t('invoice.autoSend', 'Auto-send invoice')}</Text>
        </TouchableOpacity>

        {settings.autoSend && (
          <View style={styles.inputGroup}>
            <Text style={styles.inputLabel}>{t('invoice.sendToEmail', 'Send to Email')}</Text>
            <TextInput
              style={styles.textInput}
              value={settings.sendToEmail}
              onChangeText={(value) => updateSettings('sendToEmail', value)}
              placeholder={t('invoice.emailPlaceholder', 'customer@example.com')}
              keyboardType="email-address"
            />
          </View>
        )}
      </View>
    </View>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      onRequestClose={handleClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity style={styles.closeButton} onPress={handleClose}>
            <Ionicons name="close" size={24} color="white" />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>{t('invoice.createInvoice', 'Create Invoice')}</Text>
          <View style={styles.headerActions}>
            <TouchableOpacity
              style={styles.previewButton}
              onPress={() => Alert.alert(t('invoice.preview', 'Preview'), t('invoice.previewMsg', 'Invoice preview will be implemented'))}
            >
              <Ionicons name="eye" size={20} color="white" />
            </TouchableOpacity>
          </View>
        </View>

        {/* Step Indicator */}
        {renderStepIndicator()}

        {/* Content */}
        <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
          {activeStep === 1 && renderCustomerStep()}
          {activeStep === 2 && renderItemsStep()}
          {activeStep === 3 && renderSettingsStep()}
        </ScrollView>

        {/* Footer */}
        <View style={styles.footer}>
          <View style={styles.footerActions}>
            {activeStep > 1 && (
              <TouchableOpacity
                style={styles.secondaryButton}
                onPress={prevStep}
              >
                <Ionicons name="arrow-back" size={20} color={Colors.light.primary} />
                <Text style={styles.secondaryButtonText}>{t('invoice.previous', 'Previous')}</Text>
              </TouchableOpacity>
            )}

            {activeStep < 3 ? (
              <TouchableOpacity
                style={styles.primaryButton}
                onPress={nextStep}
              >
                <Text style={styles.primaryButtonText}>{t('invoice.next', 'Next')}</Text>
                <Ionicons name="arrow-forward" size={20} color="white" />
              </TouchableOpacity>
            ) : (
              <TouchableOpacity
                style={[styles.primaryButton, isCreating && styles.primaryButtonDisabled]}
                onPress={handleCreateInvoice}
                disabled={isCreating}
              >
                {isCreating ? (
                  <Text style={styles.primaryButtonText}>{t('invoice.creating', 'Creating...')}</Text>
                ) : (
                  <>
                    <Ionicons name="document-text" size={20} color="white" />
                    <Text style={styles.primaryButtonText}>{t('invoice.createInvoice', 'Create Invoice')}</Text>
                  </>
                )}
              </TouchableOpacity>
            )}
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    paddingTop: Spacing.xl,
    backgroundColor: Colors.light.primary,
  },
  closeButton: {
    padding: Spacing.sm,
  },
  headerTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: 'white',
  },
  headerActions: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  previewButton: {
    padding: Spacing.sm,
  },
  stepIndicator: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    padding: Spacing.lg,
    backgroundColor: Colors.light.surface,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  stepContainer: {
    alignItems: 'center',
  },
  stepCircle: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: Colors.light.border,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.xs,
  },
  stepCircleActive: {
    backgroundColor: Colors.light.primary,
  },
  stepNumber: {
    fontSize: 14,
    fontWeight: '600',
    color: Colors.light.textSecondary,
  },
  stepNumberActive: {
    color: 'white',
  },
  stepLabel: {
    fontSize: 12,
    color: Colors.light.textSecondary,
  },
  stepLabelActive: {
    color: Colors.light.primary,
    fontWeight: '500',
  },
  content: {
    flex: 1,
    padding: Spacing.lg,
  },
  stepContent: {
    flex: 1,
  },
  stepTitle: {
    fontSize: 20,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: Spacing.lg,
  },
  inputGroup: {
    marginBottom: Spacing.lg,
  },
  inputLabel: {
    fontSize: 14,
    fontWeight: '500',
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  textInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    fontSize: 16,
    color: Colors.light.text,
    backgroundColor: Colors.light.surface,
  },
  textArea: {
    height: 80,
    textAlignVertical: 'top',
  },
  dateButton: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
  },
  dateButtonText: {
    fontSize: 16,
    color: Colors.light.text,
  },
  itemsContainer: {
    marginBottom: Spacing.lg,
  },
  itemRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.xs,
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 16,
    fontWeight: '500',
    color: Colors.light.text,
  },
  itemDetails: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  itemTax: {
    alignItems: 'flex-end',
  },
  taxLabel: {
    fontSize: 12,
    fontWeight: '500',
    color: Colors.light.primary,
  },
  taxRate: {
    fontSize: 12,
    color: Colors.light.textSecondary,
  },
  summaryContainer: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    padding: Spacing.lg,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  summaryLabel: {
    fontSize: 16,
    color: Colors.light.text,
  },
  summaryValue: {
    fontSize: 16,
    fontWeight: '500',
    color: Colors.light.text,
  },
  totalRow: {
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
    paddingTop: Spacing.sm,
    marginTop: Spacing.sm,
  },
  totalLabel: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.text,
  },
  totalValue: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.primary,
  },
  autoSendContainer: {
    marginTop: Spacing.lg,
  },
  checkboxContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: Spacing.md,
  },
  checkbox: {
    width: 24,
    height: 24,
    borderRadius: 4,
    borderWidth: 2,
    borderColor: Colors.light.border,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: Spacing.sm,
  },
  checkboxChecked: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  checkboxLabel: {
    fontSize: 16,
    color: Colors.light.text,
  },
  footer: {
    padding: Spacing.lg,
    backgroundColor: Colors.light.surface,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  footerActions: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  secondaryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    gap: Spacing.xs,
  },
  secondaryButtonText: {
    fontSize: 16,
    color: Colors.light.primary,
    fontWeight: '500',
  },
  primaryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.primary,
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.xs,
  },
  primaryButtonDisabled: {
    backgroundColor: Colors.light.border,
  },
  primaryButtonText: {
    fontSize: 16,
    color: 'white',
    fontWeight: '600',
  },
});

export default AdvancedInvoiceModal; 