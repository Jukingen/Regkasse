import { Ionicons } from '@expo/vector-icons';
import * as FileSystem from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import React, { useState, useEffect, useMemo, useCallback } from 'react';
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
} from 'react-native';

import { useTranslation } from 'react-i18next';
import * as InvoiceService from '../../services/api/invoiceService';
import type { PosInvoiceView } from '../../services/api/invoiceService';
import { getFormattingLocaleForTextLocale } from '../../i18n/localeUtils';

export default function InvoicesScreen() {
  const { t, i18n } = useTranslation(['invoices', 'common']);
  const uiLocale = useMemo(() => {
    return getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language);
  }, [i18n.language, i18n.resolvedLanguage]);

  const [invoices, setInvoices] = useState<PosInvoiceView[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedInvoice, setSelectedInvoice] = useState<PosInvoiceView | null>(null);
  const [modalVisible, setModalVisible] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);

  const statistics = useMemo(() => {
    const totalInvoices = invoices.length;
    const totalRevenue = invoices.reduce((sum, i) => sum + (i.totalAmount || 0), 0);
    const pendingInvoices = invoices.filter(
      (i) => !String(i.status || '').toLowerCase().includes('paid')
    ).length;
    return { totalInvoices, totalRevenue, pendingInvoices };
  }, [invoices]);

  const loadInvoices = useCallback(async () => {
    try {
      setLoading(true);
      const data = await InvoiceService.getPosInvoices(1, 100);
      setInvoices(data);
    } catch (error) {
      console.error('Invoice loading failed:', error);
      Alert.alert(t('common:error'), t('invoices:errors.loadInvoices'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadInvoices();
  }, [loadInvoices]);

  const onRefresh = async () => {
    setRefreshing(true);
    await loadInvoices();
    setRefreshing(false);
  };

  const openInvoiceDetail = async (row: PosInvoiceView) => {
    setModalVisible(true);
    setSelectedInvoice(row);
    setDetailLoading(true);
    try {
      const full = await InvoiceService.getPosInvoiceDetail(row.id);
      setSelectedInvoice(full);
    } catch (error) {
      console.error('Invoice detail loading failed:', error);
      Alert.alert(t('common:error'), t('invoices:errors.loadDetail'));
      setModalVisible(false);
      setSelectedInvoice(null);
    } finally {
      setDetailLoading(false);
    }
  };

  const handleDownloadPdf = async (id: string) => {
    try {
      const blob = await InvoiceService.downloadInvoicePdf(id);
      const fileUri = FileSystem.documentDirectory + `invoice_${id}.pdf`;
      const base64 = await blobToBase64(blob);
      await FileSystem.writeAsStringAsync(fileUri, base64, {
        encoding: FileSystem.EncodingType.Base64,
      });
      await Sharing.shareAsync(fileUri, {
        mimeType: 'application/pdf',
        dialogTitle: t('invoices:pdfDialogTitle'),
      });
    } catch (error) {
      console.error('Invoice PDF download failed:', error);
      Alert.alert(t('common:error'), t('invoices:errors.downloadPdf'));
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
      case 'paid':
        return '#4CAF50';
      case 'pending':
      case 'draft':
        return '#FF9800';
      case 'cancelled':
        return '#F44336';
      default:
        return '#757575';
    }
  };

  const getStatusLabel = (status: string) => {
    const normalized = String(status || '').toLowerCase();
    if (normalized === 'paid') return t('invoices:statusLabels.paid');
    if (normalized === 'pending') return t('invoices:statusLabels.pending');
    if (normalized === 'draft') return t('invoices:statusLabels.draft');
    if (normalized === 'cancelled') return t('invoices:statusLabels.cancelled');
    return t('invoices:statusLabels.unknown');
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>{t('invoices:loading')}</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('invoices:title')}</Text>
      </View>

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

      <ScrollView
        style={styles.scrollView}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
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
              onPress={() => openInvoiceDetail(invoice)}
            >
              <View style={styles.invoiceHeader}>
                <Text style={styles.invoiceNumber}>{invoice.receiptNumber}</Text>
                <View style={[styles.statusBadge, { backgroundColor: getStatusColor(invoice.status) }]}>
                  <Text style={styles.statusText}>{getStatusLabel(invoice.status)}</Text>
                </View>
              </View>

              <View style={styles.invoiceDetails}>
                <Text style={styles.customerName}>
                  {invoice.customer?.firstName} {invoice.customer?.lastName}
                </Text>
                <Text style={styles.invoiceDate}>
                  {new Date(invoice.invoiceDate).toLocaleDateString(uiLocale)}
                </Text>
                <Text style={styles.invoiceAmount}>€{invoice.totalAmount?.toFixed(2)}</Text>
              </View>
            </TouchableOpacity>
          ))
        )}
      </ScrollView>

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

            {detailLoading ? (
              <View style={{ padding: 24, alignItems: 'center' }}>
                <ActivityIndicator size="large" color="#007AFF" />
                <Text style={styles.loadingText}>{t('invoices:detailLoading')}</Text>
              </View>
            ) : selectedInvoice ? (
              <ScrollView style={styles.modalBody}>
                <Text style={styles.detailLabel}>{t('invoices:invoiceNo')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.receiptNumber}</Text>

                <Text style={styles.detailLabel}>{t('invoices:date')}:</Text>
                <Text style={styles.detailValue}>
                  {new Date(selectedInvoice.invoiceDate).toLocaleDateString(uiLocale)}
                </Text>

                <Text style={styles.detailLabel}>{t('invoices:time')}:</Text>
                <Text style={styles.detailValue}>
                  {new Date(selectedInvoice.invoiceDate).toLocaleTimeString(uiLocale)}
                </Text>

                <Text style={styles.detailLabel}>{t('invoices:tseSignature')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.tseSignature || '-'}</Text>

                <Text style={styles.detailLabel}>{t('invoices:kassenId')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.kassenId || '-'}</Text>

                <Text style={styles.detailLabel}>{t('invoices:taxNumber')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.taxNumber || '-'}</Text>

                <Text style={styles.detailLabel}>{t('invoices:customer')}:</Text>
                <Text style={styles.detailValue}>
                  {selectedInvoice.customer?.firstName} {selectedInvoice.customer?.lastName}
                </Text>

                <Text style={styles.detailLabel}>{t('invoices:paymentMethod')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.paymentMethod || '-'}</Text>

                <Text style={styles.detailLabel}>{t('invoices:status')}:</Text>
                <Text style={styles.detailValue}>{getStatusLabel(selectedInvoice.status)}</Text>

                <Text style={styles.detailLabel}>{t('invoices:total')}:</Text>
                <Text style={styles.detailValue}>€{selectedInvoice.totalAmount?.toFixed(2)}</Text>

                <Text style={styles.detailLabel}>{t('invoices:tax')}:</Text>
                <Text style={styles.detailValue}>€{selectedInvoice.taxAmount?.toFixed(2)}</Text>

                <Text style={styles.detailLabel}>{t('invoices:printed')}:</Text>
                <Text style={styles.detailValue}>
                  {selectedInvoice.isPrinted ? t('invoices:yes') : t('invoices:no')}
                </Text>

                <Text style={[styles.detailLabel, { marginTop: 16 }]}>{t('invoices:products')}:</Text>
                <View style={{ borderWidth: 1, borderColor: '#e0e0e0', borderRadius: 8, marginBottom: 12 }}>
                  <View style={{ flexDirection: 'row', backgroundColor: '#f1f1f1', padding: 6 }}>
                    <Text style={{ flex: 2, fontWeight: 'bold' }}>{t('invoices:item')}</Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>
                      {t('invoices:quantity')}
                    </Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>
                      {t('invoices:unit')}
                    </Text>
                    <Text style={{ flex: 1, fontWeight: 'bold', textAlign: 'right' }}>
                      {t('invoices:total')}
                    </Text>
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

                <Text style={styles.detailLabel}>{t('invoices:company')}:</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyName || '-'}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyAddress || ''}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyEmail || ''}</Text>
                <Text style={styles.detailValue}>{selectedInvoice.companyPhone || ''}</Text>

                <TouchableOpacity
                  style={[styles.modalButton, styles.primaryButton, { marginTop: 16 }]}
                  onPress={() => handleDownloadPdf(selectedInvoice.id)}
                >
                  <Ionicons name="download-outline" size={18} color="#fff" />
                  <Text style={styles.buttonText}>{t('invoices:actions.downloadPdf')}</Text>
                </TouchableOpacity>
              </ScrollView>
            ) : null}
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
    marginBottom: 4,
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
  modalButton: {
    padding: 12,
    borderRadius: 8,
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'center',
    gap: 8,
  },
  primaryButton: {
    backgroundColor: '#007AFF',
  },
  buttonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
});
