// Türkçe Açıklama: Rapor gösterim komponenti - farklı rapor tipleri için görsel gösterim ve filtreleme özellikleri.

import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

import ReportFilterPanel, { ReportFilter } from './ReportFilterPanel';

export interface ReportData {
  id: string;
  type: string;
  title: string;
  data: any;
  generatedAt: string;
  filters?: ReportFilter;
}

interface ReportDisplayProps {
  reportData: ReportData | null;
  loading: boolean;
  error: string | null;
  onGenerateReport: (filters: ReportFilter) => void;
  onSaveReport: (report: ReportData) => void;
  onExportReport: (format: 'pdf' | 'excel' | 'csv') => void;
  userRole: 'Cashier' | 'Manager' | 'Administrator';
}

const ReportDisplay: React.FC<ReportDisplayProps> = ({
  reportData,
  loading,
  error,
  onGenerateReport,
  onSaveReport,
  onExportReport,
  userRole
}) => {
  const { t } = useTranslation();
  const [filter, setFilter] = useState<ReportFilter>({});
  const [showExportOptions, setShowExportOptions] = useState(false);

  const handleApplyFilter = () => {
    onGenerateReport(filter);
  };

  const handleResetFilter = () => {
    setFilter({});
  };

  const handleSaveReport = () => {
    if (reportData) {
      onSaveReport(reportData);
      Alert.alert(
        t('report.saved', 'Rapor Kaydedildi'),
        t('report.savedMessage', 'Rapor başarıyla kaydedildi.')
      );
    }
  };

  const handleExport = (format: 'pdf' | 'excel' | 'csv') => {
    onExportReport(format);
    setShowExportOptions(false);
  };

  const renderSalesReport = (data: any) => (
    <View style={styles.reportSection}>
      <Text style={styles.reportTitle}>{t('report.salesReport', 'Satış Raporu')}</Text>
      
      {/* Özet Kartları */}
      <View style={styles.summaryCards}>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>{data.invoiceCount}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.invoices', 'Fatura')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>€{data.totalSales?.toFixed(2)}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalSales', 'Toplam Satış')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>€{data.netSales?.toFixed(2)}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.netSales', 'Net Satış')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>{data.totalItems}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalItems', 'Toplam Ürün')}</Text>
        </View>
      </View>

      {/* Kategori Bazlı Satışlar */}
      {data.salesByCategory && Object.keys(data.salesByCategory).length > 0 && (
        <View style={styles.detailSection}>
          <Text style={styles.sectionTitle}>{t('report.salesByCategory', 'Kategori Bazlı Satışlar')}</Text>
          {Object.entries(data.salesByCategory).map(([category, amount]) => (
            <View key={category} style={styles.detailRow}>
              <Text style={styles.detailLabel}>{category}</Text>
              <Text style={styles.detailValue}>€{(amount as number).toFixed(2)}</Text>
            </View>
          ))}
        </View>
      )}

      {/* En Çok Satan Ürünler */}
      {data.topProducts && Object.keys(data.topProducts).length > 0 && (
        <View style={styles.detailSection}>
          <Text style={styles.sectionTitle}>{t('report.topProducts', 'En Çok Satan Ürünler')}</Text>
          {Object.entries(data.topProducts).slice(0, 5).map(([product, quantity]) => (
            <View key={product} style={styles.detailRow}>
              <Text style={styles.detailLabel}>{product}</Text>
              <Text style={styles.detailValue}>{quantity} adet</Text>
            </View>
          ))}
        </View>
      )}
    </View>
  );

  const renderProductReport = (data: any[]) => (
    <View style={styles.reportSection}>
      <Text style={styles.reportTitle}>{t('report.productReport', 'Ürün Raporu')}</Text>
      
      <View style={styles.summaryCards}>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>{data.length}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.products', 'Ürün')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            {data.filter((p: any) => p.isLowStock).length}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.lowStock', 'Düşük Stok')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            €{data.reduce((sum: number, p: any) => sum + p.totalRevenue, 0).toFixed(2)}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalRevenue', 'Toplam Gelir')}</Text>
        </View>
      </View>

      {/* Ürün Listesi */}
      <View style={styles.detailSection}>
        <Text style={styles.sectionTitle}>{t('report.productDetails', 'Ürün Detayları')}</Text>
        {data.slice(0, 10).map((product: any) => (
          <View key={product.productId} style={styles.productCard}>
            <View style={styles.productHeader}>
              <Text style={styles.productName}>{product.productName}</Text>
              <Text style={styles.productCategory}>{product.category}</Text>
            </View>
            <View style={styles.productDetails}>
              <View style={styles.productDetail}>
                <Text style={styles.productDetailLabel}>{t('report.soldQuantity', 'Satılan')}</Text>
                <Text style={styles.productDetailValue}>{product.soldQuantity}</Text>
              </View>
              <View style={styles.productDetail}>
                <Text style={styles.productDetailLabel}>{t('report.revenue', 'Gelir')}</Text>
                <Text style={styles.productDetailValue}>€{product.totalRevenue.toFixed(2)}</Text>
              </View>
              <View style={styles.productDetail}>
                <Text style={styles.productDetailLabel}>{t('report.stock', 'Stok')}</Text>
                <Text style={[
                  styles.productDetailValue,
                  product.isLowStock && styles.lowStockText
                ]}>
                  {product.currentStock}
                </Text>
              </View>
            </View>
          </View>
        ))}
      </View>
    </View>
  );

  const renderCategoryReport = (data: any[]) => (
    <View style={styles.reportSection}>
      <Text style={styles.reportTitle}>{t('report.categoryReport', 'Kategori Raporu')}</Text>
      
      <View style={styles.summaryCards}>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>{data.length}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.categories', 'Kategori')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            {data.reduce((sum: number, c: any) => sum + c.productCount, 0)}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalProducts', 'Toplam Ürün')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            €{data.reduce((sum: number, c: any) => sum + c.totalRevenue, 0).toFixed(2)}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalRevenue', 'Toplam Gelir')}</Text>
        </View>
      </View>

      {/* Kategori Listesi */}
      <View style={styles.detailSection}>
        <Text style={styles.sectionTitle}>{t('report.categoryDetails', 'Kategori Detayları')}</Text>
        {data.map((category: any) => (
          <View key={category.categoryId} style={styles.categoryCard}>
            <View style={styles.categoryHeader}>
              <Text style={styles.categoryName}>{category.categoryName}</Text>
              <Text style={styles.categoryMarketShare}>%{category.marketShare.toFixed(1)}</Text>
            </View>
            <View style={styles.categoryDetails}>
              <View style={styles.categoryDetail}>
                <Text style={styles.categoryDetailLabel}>{t('report.products', 'Ürün')}</Text>
                <Text style={styles.categoryDetailValue}>{category.productCount}</Text>
              </View>
              <View style={styles.categoryDetail}>
                <Text style={styles.categoryDetailLabel}>{t('report.soldQuantity', 'Satılan')}</Text>
                <Text style={styles.categoryDetailValue}>{category.soldQuantity}</Text>
              </View>
              <View style={styles.categoryDetail}>
                <Text style={styles.categoryDetailLabel}>{t('report.revenue', 'Gelir')}</Text>
                <Text style={styles.categoryDetailValue}>€{category.totalRevenue.toFixed(2)}</Text>
              </View>
            </View>
          </View>
        ))}
      </View>
    </View>
  );

  const renderInventoryReport = (data: any[]) => (
    <View style={styles.reportSection}>
      <Text style={styles.reportTitle}>{t('report.inventoryReport', 'Stok Raporu')}</Text>
      
      <View style={styles.summaryCards}>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>{data.length}</Text>
          <Text style={styles.summaryCardLabel}>{t('report.products', 'Ürün')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            {data.filter((p: any) => p.isLowStock).length}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.lowStock', 'Düşük Stok')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            {data.filter((p: any) => p.isOutOfStock).length}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.outOfStock', 'Stok Yok')}</Text>
        </View>
        <View style={styles.summaryCard}>
          <Text style={styles.summaryCardValue}>
            €{data.reduce((sum: number, p: any) => sum + p.totalValue, 0).toFixed(2)}
          </Text>
          <Text style={styles.summaryCardLabel}>{t('report.totalValue', 'Toplam Değer')}</Text>
        </View>
      </View>

      {/* Stok Uyarıları */}
      <View style={styles.detailSection}>
        <Text style={styles.sectionTitle}>{t('report.stockAlerts', 'Stok Uyarıları')}</Text>
        {data.filter((p: any) => p.isLowStock || p.isOutOfStock).map((product: any) => (
          <View key={product.productId} style={[
            styles.stockAlertCard,
            product.isOutOfStock && styles.outOfStockCard
          ]}>
            <View style={styles.stockAlertHeader}>
              <Text style={styles.stockAlertProduct}>{product.productName}</Text>
              <Text style={styles.stockAlertCategory}>{product.category}</Text>
            </View>
            <View style={styles.stockAlertDetails}>
              <Text style={styles.stockAlertStatus}>
                {product.isOutOfStock 
                  ? t('report.outOfStock', 'Stok Yok')
                  : t('report.lowStock', 'Düşük Stok')
                }
              </Text>
              <Text style={styles.stockAlertQuantity}>
                {t('report.currentStock', 'Mevcut Stok')}: {product.currentStock}
              </Text>
              <Text style={styles.stockAlertMin}>
                {t('report.minStock', 'Min Stok')}: {product.minStockLevel}
              </Text>
            </View>
          </View>
        ))}
      </View>
    </View>
  );

  const renderReportContent = () => {
    if (!reportData) return null;

    switch (reportData.type) {
      case 'sales':
        return renderSalesReport(reportData.data);
      case 'products':
        return renderProductReport(reportData.data);
      case 'categories':
        return renderCategoryReport(reportData.data);
      case 'inventory':
        return renderInventoryReport(reportData.data);
      default:
        return (
          <View style={styles.reportSection}>
            <Text style={styles.reportTitle}>{reportData.title}</Text>
            <Text style={styles.noDataText}>{t('report.noData', 'Veri bulunamadı')}</Text>
          </View>
        );
    }
  };

  return (
    <View style={styles.container}>
      {/* Filtre Paneli */}
      <ReportFilterPanel
        filter={filter}
        onFilterChange={setFilter}
        onApplyFilter={handleApplyFilter}
        onResetFilter={handleResetFilter}
        showAdvancedFilters={userRole !== 'Cashier'}
      />

      {/* Rapor Başlığı ve Aksiyonlar */}
      {reportData && (
        <View style={styles.reportHeader}>
          <View style={styles.reportHeaderInfo}>
            <Text style={styles.reportHeaderTitle}>{reportData.title}</Text>
            <Text style={styles.reportHeaderDate}>
              {new Date(reportData.generatedAt).toLocaleDateString('de-DE')}
            </Text>
          </View>
          <View style={styles.reportHeaderActions}>
            <TouchableOpacity
              style={styles.actionButton}
              onPress={handleSaveReport}
            >
              <Ionicons name="save" size={20} color="#1976d2" />
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.actionButton}
              onPress={() => setShowExportOptions(true)}
            >
              <Ionicons name="download" size={20} color="#1976d2" />
            </TouchableOpacity>
          </View>
        </View>
      )}

      {/* Yükleme Durumu */}
      {loading && (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#1976d2" />
          <Text style={styles.loadingText}>{t('report.loading', 'Rapor yükleniyor...')}</Text>
        </View>
      )}

      {/* Hata Durumu */}
      {error && (
        <View style={styles.errorContainer}>
          <Ionicons name="alert-circle" size={48} color="#d32f2f" />
          <Text style={styles.errorText}>{error}</Text>
          <TouchableOpacity
            style={styles.retryButton}
            onPress={() => onGenerateReport(filter)}
          >
            <Text style={styles.retryButtonText}>{t('report.retry', 'Tekrar Dene')}</Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Rapor İçeriği */}
      {!loading && !error && (
        <ScrollView style={styles.reportContent} showsVerticalScrollIndicator={false}>
          {renderReportContent()}
        </ScrollView>
      )}

      {/* Dışa Aktarma Seçenekleri Modal */}
      {showExportOptions && (
        <View style={styles.modalOverlay}>
          <View style={styles.exportModal}>
            <Text style={styles.exportModalTitle}>{t('report.export', 'Dışa Aktar')}</Text>
            <TouchableOpacity
              style={styles.exportOption}
              onPress={() => handleExport('pdf')}
            >
              <Ionicons name="document-text" size={24} color="#d32f2f" />
              <Text style={styles.exportOptionText}>PDF</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.exportOption}
              onPress={() => handleExport('excel')}
            >
              <Ionicons name="grid" size={24} color="#388e3c" />
              <Text style={styles.exportOptionText}>Excel</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.exportOption}
              onPress={() => handleExport('csv')}
            >
              <Ionicons name="document" size={24} color="#1976d2" />
              <Text style={styles.exportOptionText}>CSV</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={() => setShowExportOptions(false)}
            >
              <Text style={styles.cancelButtonText}>{t('common.cancel', 'İptal')}</Text>
            </TouchableOpacity>
          </View>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  reportHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  reportHeaderInfo: {
    flex: 1,
  },
  reportHeaderTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  reportHeaderDate: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  reportHeaderActions: {
    flexDirection: 'row',
    gap: 8,
  },
  actionButton: {
    padding: 8,
    borderRadius: 6,
    backgroundColor: '#f5f5f5',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorText: {
    marginTop: 16,
    fontSize: 16,
    color: '#d32f2f',
    textAlign: 'center',
  },
  retryButton: {
    marginTop: 16,
    paddingHorizontal: 24,
    paddingVertical: 12,
    backgroundColor: '#1976d2',
    borderRadius: 6,
  },
  retryButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  reportContent: {
    flex: 1,
  },
  reportSection: {
    backgroundColor: '#fff',
    margin: 16,
    borderRadius: 8,
    padding: 16,
    elevation: 2,
  },
  reportTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#333',
    marginBottom: 16,
  },
  summaryCards: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 16,
  },
  summaryCard: {
    flex: 1,
    minWidth: 80,
    padding: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    alignItems: 'center',
  },
  summaryCardValue: {
    fontSize: 18,
    fontWeight: '700',
    color: '#1976d2',
  },
  summaryCardLabel: {
    fontSize: 12,
    color: '#666',
    marginTop: 4,
    textAlign: 'center',
  },
  detailSection: {
    marginTop: 16,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 12,
  },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  detailLabel: {
    fontSize: 14,
    color: '#333',
  },
  detailValue: {
    fontSize: 14,
    fontWeight: '600',
    color: '#1976d2',
  },
  productCard: {
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
  },
  productHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  productName: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    flex: 1,
  },
  productCategory: {
    fontSize: 12,
    color: '#666',
  },
  productDetails: {
    flexDirection: 'row',
    gap: 16,
  },
  productDetail: {
    flex: 1,
  },
  productDetailLabel: {
    fontSize: 12,
    color: '#666',
  },
  productDetailValue: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  lowStockText: {
    color: '#d32f2f',
  },
  categoryCard: {
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
  },
  categoryHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  categoryName: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  categoryMarketShare: {
    fontSize: 12,
    color: '#1976d2',
    fontWeight: '600',
  },
  categoryDetails: {
    flexDirection: 'row',
    gap: 16,
  },
  categoryDetail: {
    flex: 1,
  },
  categoryDetailLabel: {
    fontSize: 12,
    color: '#666',
  },
  categoryDetailValue: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  stockAlertCard: {
    backgroundColor: '#fff3cd',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#ffc107',
  },
  outOfStockCard: {
    backgroundColor: '#f8d7da',
    borderLeftColor: '#d32f2f',
  },
  stockAlertHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  stockAlertProduct: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
  },
  stockAlertCategory: {
    fontSize: 12,
    color: '#666',
  },
  stockAlertDetails: {
    gap: 4,
  },
  stockAlertStatus: {
    fontSize: 12,
    fontWeight: '600',
    color: '#d32f2f',
  },
  stockAlertQuantity: {
    fontSize: 12,
    color: '#666',
  },
  stockAlertMin: {
    fontSize: 12,
    color: '#666',
  },
  noDataText: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    marginTop: 32,
  },
  modalOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  exportModal: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 24,
    width: '80%',
    alignItems: 'center',
  },
  exportModalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 16,
  },
  exportOption: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: 16,
    width: '100%',
    marginBottom: 8,
    borderRadius: 6,
    backgroundColor: '#f5f5f5',
  },
  exportOptionText: {
    fontSize: 16,
    color: '#333',
    marginLeft: 12,
  },
  cancelButton: {
    marginTop: 16,
    paddingVertical: 12,
    paddingHorizontal: 24,
    borderRadius: 6,
    backgroundColor: '#f5f5f5',
  },
  cancelButtonText: {
    fontSize: 14,
    color: '#666',
  },
});

export default ReportDisplay; 