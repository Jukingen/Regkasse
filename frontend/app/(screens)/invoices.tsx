import { Ionicons } from '@expo/vector-icons';
import * as FileSystem from 'expo-file-system';
import * as MailComposer from 'expo-mail-composer';
import * as Print from 'expo-print';
import * as Sharing from 'expo-sharing';
import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  RefreshControl,
  Modal,
  TextInput,
  Switch
} from 'react-native';

import { useAuth } from '../../contexts/AuthContext';
import { useTranslation } from '../../i18n';
import * as InvoiceService from '../../services/api/invoiceService';
import { Invoice, InvoiceCreateRequest } from '../../types/invoice';


export default function InvoicesScreen() {
  const { t } = useTranslation(['invoices', 'common']);
  const { user } = useAuth();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [modalVisible, setModalVisible] = useState(false);
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [paymentModalVisible, setPaymentModalVisible] = useState(false);
  const [emailModalVisible, setEmailModalVisible] = useState(false);
  const [statistics, setStatistics] = useState<any>(null);

  // Form state'leri
  const [newInvoice, setNewInvoice] = useState<Partial<InvoiceCreateRequest>>({
    customerId: '',
    items: [],
    notes: ''
  });
  const [paymentData, setPaymentData] = useState({
    paymentMethod: 'cash' as 'cash' | 'card' | 'voucher',
    amount: 0,
    tseRequired: true
  });
  const [emailData, setEmailData] = useState({
    email: '',
    subject: '',
    message: ''
  });

  useEffect(() => {
    loadInvoices();
    loadStatistics();
  }, []);

  const loadInvoices = async () => {
    try {
      setLoading(true);
      const data = await InvoiceService.getInvoices();
      setInvoices(data);
    } catch (error) {
      console.error('Faturalar yüklenirken hata:', error);
      Alert.alert('Hata', 'Faturalar yüklenirken bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  const loadStatistics = async () => {
    try {
      const stats = await InvoiceService.getInvoiceStatistics();
      setStatistics(stats);
    } catch (error) {
      console.error('İstatistikler yüklenirken hata:', error);
    }
  };

  const onRefresh = async () => {
    setRefreshing(true);
    await Promise.all([loadInvoices(), loadStatistics()]);
    setRefreshing(false);
  };

  const handleCreateInvoice = async () => {
    try {
      if (!newInvoice.customerId || !newInvoice.items || newInvoice.items.length === 0) {
        Alert.alert('Hata', 'Müşteri ve ürün bilgileri zorunludur');
        return;
      }

      const invoice = await InvoiceService.createInvoice(newInvoice as InvoiceCreateRequest);
      setInvoices(prev => [invoice, ...prev]);
      setCreateModalVisible(false);
      setNewInvoice({ customerId: '', items: [], notes: '' });
      Alert.alert('Başarılı', 'Fatura başarıyla oluşturuldu');
    } catch (error) {
      console.error('Fatura oluşturulurken hata:', error);
      Alert.alert('Hata', 'Fatura oluşturulurken bir hata oluştu');
    }
  };

  const handleDeleteInvoice = async (id: string) => {
    Alert.alert(
      'Fatura Sil',
      'Bu faturayı silmek istediğinizden emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Sil',
          style: 'destructive',
          onPress: async () => {
            try {
              await InvoiceService.deleteInvoice(id);
              setInvoices(prev => prev.filter(inv => inv.id !== id));
              Alert.alert('Başarılı', 'Fatura başarıyla silindi');
            } catch (error) {
              console.error('Fatura silinirken hata:', error);
              Alert.alert('Hata', 'Fatura silinirken bir hata oluştu');
            }
          }
        }
      ]
    );
  };

  const handleSavePayment = async () => {
    if (!selectedInvoice) return;

    try {
      const updatedInvoice = await InvoiceService.savePayment(selectedInvoice.id, paymentData);
      setInvoices(prev => prev.map(inv => inv.id === selectedInvoice.id ? updatedInvoice : inv));
      setPaymentModalVisible(false);
      setSelectedInvoice(null);
      Alert.alert('Başarılı', 'Ödeme başarıyla kaydedildi');
    } catch (error) {
      console.error('Ödeme kaydedilirken hata:', error);
      Alert.alert('Hata', 'Ödeme kaydedilirken bir hata oluştu');
    }
  };

  // PDF ve CSV indirme fonksiyonları
  const handleDownloadPdf = async (id: string) => {
    try {
      const blob = await InvoiceService.downloadInvoicePdf(id);
      const fileUri = FileSystem.documentDirectory + `invoice_${id}.pdf`;
      const base64 = await blobToBase64(blob);
      await FileSystem.writeAsStringAsync(fileUri, base64, {
        encoding: FileSystem.EncodingType.Base64
      });
      await Sharing.shareAsync(fileUri, {
        mimeType: 'application/pdf',
        dialogTitle: 'Fatura PDF'
      });
    } catch (error) {
      console.error('PDF indirilirken hata:', error);
      Alert.alert('Hata', 'PDF indirilirken bir hata oluştu');
    }
  };

  const handleDownloadCsv = async (id: string) => {
    try {
      const blob = await InvoiceService.downloadInvoiceCsv(id);
      const fileUri = FileSystem.documentDirectory + `invoice_${id}.csv`;
      const base64 = await blobToBase64(blob);
      await FileSystem.writeAsStringAsync(fileUri, base64, {
        encoding: FileSystem.EncodingType.Base64
      });
      await Sharing.shareAsync(fileUri, {
        mimeType: 'text/csv',
        dialogTitle: 'Fatura CSV'
      });
    } catch (error) {
      console.error('CSV indirilirken hata:', error);
      Alert.alert('Hata', 'CSV indirilirken bir hata oluştu');
    }
  };

  const handleSendEmail = async () => {
    if (!selectedInvoice) return;

    try {
      const result = await InvoiceService.sendInvoiceEmail(selectedInvoice.id, emailData);
      setEmailModalVisible(false);
      setSelectedInvoice(null);
      Alert.alert('Başarılı', result.message);
    } catch (error) {
      console.error('Email gönderilirken hata:', error);
      Alert.alert('Hata', 'Email gönderilirken bir hata oluştu');
    }
  };

  const handleCancelInvoice = async (id: string) => {
    Alert.prompt(
      'Fatura İptal',
      'İptal sebebini girin:',
      async (reason) => {
        if (reason) {
          try {
            const updatedInvoice = await InvoiceService.cancelInvoice(id, reason);
            setInvoices(prev => prev.map(inv => inv.id === id ? updatedInvoice : inv));
            Alert.alert('Başarılı', 'Fatura başarıyla iptal edildi');
          } catch (error) {
            console.error('Fatura iptal edilirken hata:', error);
            Alert.alert('Hata', 'Fatura iptal edilirken bir hata oluştu');
          }
        }
      }
    );
  };

  const handleSendToFinanzOnline = async (id: string) => {
    try {
      const result = await InvoiceService.sendToFinanzOnline(id);
      Alert.alert('Sonuç', result.message);
    } catch (error) {
      console.error('FinanzOnline\'a gönderilirken hata:', error);
      Alert.alert('Hata', 'FinanzOnline\'a gönderilirken bir hata oluştu');
    }
  };

  const blobToBase64 = (blob: Blob): Promise<string> => {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        resolve(result.split(',')[1]);
      };
      reader.onerror = reject;
      reader.readAsDataURL(blob);
    });
  };

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'paid': return '#4CAF50';
      case 'pending': return '#FF9800';
      case 'cancelled': return '#F44336';
      default: return '#757575';
    }
  };

  const getPaymentMethodIcon = (method: string) => {
    switch (method.toLowerCase()) {
      case 'cash': return 'cash-outline';
      case 'card': return 'card-outline';
      case 'voucher': return 'gift-outline';
      default: return 'help-outline';
    }
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Faturalar yükleniyor...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>{t('invoices:title')}</Text>
        <TouchableOpacity
          style={styles.addButton}
          onPress={() => setCreateModalVisible(true)}
        >
          <Ionicons name="add" size={24} color="white" />
        </TouchableOpacity>
      </View>

      {/* İstatistikler */}
      {statistics && (
        <View style={styles.statsContainer}>
          <View style={styles.statItem}>
            <Text style={styles.statNumber}>{statistics.totalInvoices}</Text>
            <Text style={styles.statLabel}>{t('invoices:stats.totalInvoices')}</Text>
          </View>
          <View style={styles.statItem}>
            <Text style={styles.statNumber}>€{statistics.totalRevenue?.toFixed(2)}</Text>
            <Text style={styles.statLabel}>{t('invoices:stats.totalRevenue')}</Text>
          </View>
          <View style={styles.statItem}>
            <Text style={styles.statNumber}>{statistics.pendingInvoices}</Text>
            <Text style={styles.statLabel}>{t('invoices:stats.pending')}</Text>
          </View>
        </View>
      )}

      {/* Fatura Listesi */}
      <ScrollView
        style={styles.scrollView}
        refreshControl={
          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
        }
      >
        {invoices.length === 0 ? (
          <View style={styles.emptyContainer}>
            <Ionicons name="document-outline" size={64} color="#ccc" />
            <Text style={styles.emptyText}>{t('invoices:emptyList')}</Text>
          </View>
        ) : (
          invoices.map((invoice) => (
            <TouchableOpacity
              key={invoice.id}
              style={styles.invoiceCard}
              onPress={() => {
                setSelectedInvoice(invoice);
                setModalVisible(true);
              }}
            >
              <View style={styles.invoiceHeader}>
                <Text style={styles.invoiceNumber}>{invoice.receiptNumber}</Text>
                <View style={[styles.statusBadge, { backgroundColor: getStatusColor(invoice.status) }]}>
                  <Text style={styles.statusText}>{invoice.status}</Text>
                </View>
              </View>

              <View style={styles.invoiceDetails}>
                <Text style={styles.customerName}>
                  {invoice.customer?.firstName} {invoice.customer?.lastName}
                </Text>
                <Text style={styles.invoiceDate}>
                  {new Date(invoice.invoiceDate).toLocaleDateString('tr-TR')}
                </Text>
                <Text style={styles.invoiceAmount}>
                  €{invoice.totalAmount?.toFixed(2)}
                </Text>
              </View>

              <View style={styles.invoiceActions}>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => handleDownloadPdf(invoice.id)}
                >
                  <Ionicons name="download-outline" size={20} color="#007AFF" />
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => {
                    setSelectedInvoice(invoice);
                    setEmailModalVisible(true);
                  }}
                >
                  <Ionicons name="mail-outline" size={20} color="#007AFF" />
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.actionButton}
                  onPress={() => handleDeleteInvoice(invoice.id)}
                >
                  <Ionicons name="trash-outline" size={20} color="#FF3B30" />
                </TouchableOpacity>
              </View>
            </TouchableOpacity>
          ))
        )}
      </ScrollView>

      {/* Fatura Detay Modalı */}
      <Modal
        visible={modalVisible}
        animationType="slide"
        transparent
        onRequestClose={() => setModalVisible(false)}
      >
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('invoices:detailTitle')}</Text>
              <TouchableOpacity onPress={() => setModalVisible(false)}>
                <Ionicons name="close" size={24} color="#000" />
              </TouchableOpacity>
            </View>

            {selectedInvoice && (
              <ScrollView style={styles.modalBody}>
                {/* Yasal ve müşteri için alanlar */}
                <Text style={styles.detailLabel}>{t('invoices:invoiceNo')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.receiptNumber}</Text>

                <Text style={styles.detailLabel}>{t('invoices:date')}:</Text>
                <Text style={styles.detailValue}>{new Date(selectedInvoice.invoiceDate).toLocaleDateString('tr-TR')}</Text>

                <Text style={styles.detailLabel}>{t('invoices:time')}:</Text>
                <Text style={styles.detailValue}>{new Date(selectedInvoice.invoiceDate).toLocaleTimeString('tr-TR')}</Text>

                <Text style={styles.detailLabel}>TSE-Signatur:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.tseSignature || '-'}</Text>

                <Text style={styles.detailLabel}>Kassen-ID:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.kassenId || '-'}</Text>

                <Text style={styles.detailLabel}>Steuernummer:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.taxNumber || '-'}</Text>

                <Text style={styles.detailLabel}>{t('invoices:customer')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.customer?.firstName} {selectedInvoice.customer?.lastName}</Text>

                <Text style={styles.detailLabel}>{t('invoices:paymentMethod')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.paymentMethod}</Text>

                <Text style={styles.detailLabel}>{t('invoices:status')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.status}</Text>

                <Text style={styles.detailLabel}>{t('invoices:total')}:</Text>
                <Text style={styles.detailValue}>€{selectedInvoice.totalAmount?.toFixed(2)}</Text>

                <Text style={styles.detailLabel}>{t('invoices:tax')}:</Text>
                <Text style={styles.detailValue}>€{selectedInvoice.taxAmount?.toFixed(2)}</Text>

                <Text style={styles.detailLabel}>{t('invoices:printed')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.isPrinted ? t('invoices:yes') : t('invoices:no')}</Text>

                {/* Ürünler tablosu */}
                <Text style={[styles.detailLabel, { marginTop: 16 }]}>{t('invoices:products')}:</Text>
                <View style={{ borderWidth: 1, borderColor: '#e0e0e0', borderRadius: 8, marginBottom: 12 }}>
                  <View style={{ flexDirection: 'row', backgroundColor: '#f1f1f1', padding: 6 }}>
                    <Text style={{ flex: 2, fontWeight: 'bold' }}>{t('invoices:item')}</Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>{t('invoices:quantity')}</Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>{t('invoices:unit')}</Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>{t('invoices:total')}</Text>
                  </View>
                  {selectedInvoice.items?.map((item, idx) => (
                    <View key={idx} style={{ flexDirection: 'row', padding: 6 }}>
                      <Text style={{ flex: 2 }}>{item.productName}</Text>
                      <Text style={{ flex: 1, textAlign: 'right' }}>{item.quantity}</Text>
                      <Text style={{ flex: 1, textAlign: 'right' }}>€{item.unitPrice?.toFixed(2)}</Text>
                      <Text style={{ flex: 1, textAlign: 'right' }}>€{item.totalAmount?.toFixed(2)}</Text>
                    </View>
                  ))}
                </View>

                {/* Şirket bilgisi */}
                <Text style={styles.detailLabel}>{t('invoices:company')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyName || '-'}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyAddress || ''}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyEmail || ''}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyPhone || ''}</Text>

                {/* PDF/CSV indirme butonları */}
                <View style={{ flexDirection: 'row', gap: 12, marginTop: 16 }}>
                  <TouchableOpacity
                    style={[styles.modalButton, styles.primaryButton, { flex: 1 }]}
                    onPress={() => handleDownloadPdf(selectedInvoice.id)}
                  >
                    <Ionicons name="download-outline" size={18} color="#fff" />
                    <Text style={styles.buttonText}>{t('invoices:actions.downloadPdf')}</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[styles.modalButton, styles.secondaryButton, { flex: 1 }]}
                    onPress={() => handleDownloadCsv(selectedInvoice.id)}
                  >
                    <Ionicons name="document-outline" size={18} color="#fff" />
                    <Text style={styles.buttonText}>{t('invoices:actions.downloadCsv')}</Text>
                  </TouchableOpacity>
                </View>

                {/* Diğer aksiyonlar (ödeme, iptal, finansonline) */}
                <View style={styles.modalActions}>
                  <TouchableOpacity
                    style={[styles.modalButton, styles.primaryButton]}
                    onPress={() => {
                      setModalVisible(false);
                      setPaymentModalVisible(true);
                    }}
                  >
                    <Text style={styles.buttonText}>{t('invoices:actions.recordPayment')}</Text>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={[styles.modalButton, styles.secondaryButton]}
                    onPress={() => handleSendToFinanzOnline(selectedInvoice.id)}
                  >
                    <Text style={styles.buttonText}>{t('invoices:actions.sendFinanzOnline')}</Text>
                  </TouchableOpacity>

                  <TouchableOpacity
                    style={[styles.modalButton, styles.dangerButton]}
                    onPress={() => handleCancelInvoice(selectedInvoice.id)}
                  >
                    <Text style={styles.buttonText}>{t('invoices:actions.cancelInvoice')}</Text>
                  </TouchableOpacity>
                </View>
              </ScrollView>
            )}
          </View>
        </View>
      </Modal>

      {/* Ödeme Modalı */}
      <Modal
        visible={paymentModalVisible}
        animationType="slide"
        transparent
        onRequestClose={() => setPaymentModalVisible(false)}
      >
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('invoices:savePaymentTitle')}</Text>
              <TouchableOpacity onPress={() => setPaymentModalVisible(false)}>
                <Ionicons name="close" size={24} color="#000" />
              </TouchableOpacity>
            </View>

            <View style={styles.modalBody}>
              <Text style={styles.inputLabel}>{t('invoices:payment.method')}:</Text>
              <View style={styles.paymentMethodContainer}>
                {['cash', 'card', 'voucher'].map((method) => (
                  <TouchableOpacity
                    key={method}
                    style={[
                      styles.paymentMethodButton,
                      paymentData.paymentMethod === method && styles.paymentMethodButtonActive
                    ]}
                    onPress={() => setPaymentData(prev => ({ ...prev, paymentMethod: method as any }))}
                  >
                    <Text style={[
                      styles.paymentMethodText,
                      paymentData.paymentMethod === method && styles.paymentMethodTextActive
                    ]}>
                      {method === 'cash' ? t('invoices:methods.cash') : method === 'card' ? t('invoices:methods.card') : t('invoices:methods.voucher')}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.inputLabel}>{t('invoices:payment.amount')}:</Text>
              <TextInput
                style={styles.input}
                value={paymentData.amount.toString()}
                onChangeText={(text) => setPaymentData(prev => ({ ...prev, amount: parseFloat(text) || 0 }))}
                keyboardType="numeric"
                placeholder="0.00"
              />

              <View style={styles.switchContainer}>
                <Text style={styles.inputLabel}>{t('invoices:payment.tseRequired')}:</Text>
                <Switch
                  value={paymentData.tseRequired}
                  onValueChange={(value) => setPaymentData(prev => ({ ...prev, tseRequired: value }))}
                />
              </View>

              <TouchableOpacity
                style={[styles.modalButton, styles.primaryButton]}
                onPress={handleSavePayment}
              >
                <Text style={styles.buttonText}>{t('invoices:payment.save')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* Email Modalı */}
      <Modal
        visible={emailModalVisible}
        animationType="slide"
        transparent
        onRequestClose={() => setEmailModalVisible(false)}
      >
        <View style={styles.modalContainer}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('invoices:emailTitle')}</Text>
              <TouchableOpacity onPress={() => setEmailModalVisible(false)}>
                <Ionicons name="close" size={24} color="#000" />
              </TouchableOpacity>
            </View>

            <View style={styles.modalBody}>
              <Text style={styles.inputLabel}>{t('invoices:email.address')}:</Text>
              <TextInput
                style={styles.input}
                value={emailData.email}
                onChangeText={(text) => setEmailData(prev => ({ ...prev, email: text }))}
                keyboardType="email-address"
                placeholder={t('invoices:email.placeholder')}
              />

              <Text style={styles.inputLabel}>{t('invoices:email.subject')}:</Text>
              <TextInput
                style={styles.input}
                value={emailData.subject}
                onChangeText={(text) => setEmailData(prev => ({ ...prev, subject: text }))}
                placeholder={t('invoices:email.subjectPlaceholder')}
              />

              <Text style={styles.inputLabel}>{t('invoices:email.message')}:</Text>
              <TextInput
                style={[styles.input, styles.textArea]}
                value={emailData.message}
                onChangeText={(text) => setEmailData(prev => ({ ...prev, message: text }))}
                placeholder={t('invoices:email.messagePlaceholder')}
                multiline
                numberOfLines={4}
              />

              <TouchableOpacity
                style={[styles.modalButton, styles.primaryButton]}
                onPress={handleSendEmail}
              >
                <Text style={styles.buttonText}>{t('invoices:email.send')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 10,
    fontSize: 16,
    color: '#666',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    backgroundColor: 'white',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  addButton: {
    backgroundColor: '#007AFF',
    width: 40,
    height: 40,
    borderRadius: 20,
    justifyContent: 'center',
    alignItems: 'center',
  },
  statsContainer: {
    flexDirection: 'row',
    padding: 16,
    backgroundColor: 'white',
    marginBottom: 8,
  },
  statItem: {
    flex: 1,
    alignItems: 'center',
  },
  statNumber: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
  },
  scrollView: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  emptyText: {
    fontSize: 16,
    color: '#666',
    marginTop: 16,
  },
  invoiceCard: {
    backgroundColor: 'white',
    margin: 8,
    padding: 16,
    borderRadius: 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  invoiceHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  invoiceNumber: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 12,
    color: 'white',
    fontWeight: 'bold',
  },
  invoiceDetails: {
    marginBottom: 12,
  },
  customerName: {
    fontSize: 14,
    color: '#333',
    marginBottom: 4,
  },
  invoiceDate: {
    fontSize: 12,
    color: '#666',
    marginBottom: 4,
  },
  invoiceAmount: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  invoiceActions: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 8,
  },
  actionButton: {
    padding: 8,
  },
  modalContainer: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modalContent: {
    backgroundColor: 'white',
    borderRadius: 12,
    width: '90%',
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  modalBody: {
    padding: 16,
  },
  detailLabel: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 12,
    marginBottom: 4,
  },
  detailValue: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
  },
  modalActions: {
    marginTop: 20,
    gap: 12,
  },
  modalButton: {
    padding: 12,
    borderRadius: 8,
    alignItems: 'center',
  },
  primaryButton: {
    backgroundColor: '#007AFF',
  },
  secondaryButton: {
    backgroundColor: '#34C759',
  },
  dangerButton: {
    backgroundColor: '#FF3B30',
  },
  buttonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
  inputLabel: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#333',
    marginTop: 12,
    marginBottom: 4,
  },
  input: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    marginBottom: 8,
  },
  textArea: {
    height: 80,
    textAlignVertical: 'top',
  },
  paymentMethodContainer: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 16,
  },
  paymentMethodButton: {
    flex: 1,
    padding: 12,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    alignItems: 'center',
  },
  paymentMethodButtonActive: {
    backgroundColor: '#007AFF',
    borderColor: '#007AFF',
  },
  paymentMethodText: {
    fontSize: 14,
    color: '#333',
  },
  paymentMethodTextActive: {
    color: 'white',
  },
  switchContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 16,
  },
}); 