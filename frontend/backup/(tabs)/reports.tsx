import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { reportService, ReportPeriod, SalesReport, CashReport } from '../../services/api/reportService';

export default function ReportsScreen() {
  const [selectedPeriod, setSelectedPeriod] = useState<ReportPeriod>('daily');
  const [salesReport, setSalesReport] = useState<SalesReport | null>(null);
  const [cashReport, setCashReport] = useState<CashReport | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const fetchReports = async () => {
    try {
      setIsLoading(true);
      const [sales, cash] = await Promise.all([
        reportService.getSalesReport(selectedPeriod),
        reportService.getCashReport(selectedPeriod)
      ]);
      setSalesReport(sales);
      setCashReport(cash);
    } catch (error) {
      Alert.alert('Hata', 'Raporlar yüklenirken bir hata oluştu');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchReports();
  }, [selectedPeriod]);

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat('de-DE', {
      style: 'currency',
      currency: 'EUR'
    }).format(amount);
  };

  const PeriodSelector = () => (
    <View style={styles.periodSelector}>
      <TouchableOpacity
        style={[
          styles.periodButton,
          selectedPeriod === 'daily' && styles.selectedPeriod,
        ]}
        onPress={() => setSelectedPeriod('daily')}
      >
        <Text
          style={[
            styles.periodButtonText,
            selectedPeriod === 'daily' && styles.selectedPeriodText,
          ]}
        >
          Bugün
        </Text>
      </TouchableOpacity>
      <TouchableOpacity
        style={[
          styles.periodButton,
          selectedPeriod === 'weekly' && styles.selectedPeriod,
        ]}
        onPress={() => setSelectedPeriod('weekly')}
      >
        <Text
          style={[
            styles.periodButtonText,
            selectedPeriod === 'weekly' && styles.selectedPeriodText,
          ]}
        >
          Bu Hafta
        </Text>
      </TouchableOpacity>
      <TouchableOpacity
        style={[
          styles.periodButton,
          selectedPeriod === 'monthly' && styles.selectedPeriod,
        ]}
        onPress={() => setSelectedPeriod('monthly')}
      >
        <Text
          style={[
            styles.periodButtonText,
            selectedPeriod === 'monthly' && styles.selectedPeriodText,
          ]}
        >
          Bu Ay
        </Text>
      </TouchableOpacity>
    </View>
  );

  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <PeriodSelector />

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Satış Özeti</Text>
        <View style={styles.statRow}>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Toplam Satış</Text>
            <Text style={styles.statValue}>
              {formatCurrency(salesReport?.totalSales || 0)}
            </Text>
          </View>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Fatura Sayısı</Text>
            <Text style={styles.statValue}>{salesReport?.totalInvoices}</Text>
          </View>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Ortalama Sepet</Text>
            <Text style={styles.statValue}>
              {formatCurrency(salesReport?.averageTicket || 0)}
            </Text>
          </View>
        </View>
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Kasa Durumu</Text>
        <View style={styles.statRow}>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Açılış</Text>
            <Text style={styles.statValue}>
              {formatCurrency(cashReport?.openingBalance || 0)}
            </Text>
          </View>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Kapanış</Text>
            <Text style={styles.statValue}>
              {formatCurrency(cashReport?.closingBalance || 0)}
            </Text>
          </View>
          <View style={styles.stat}>
            <Text style={styles.statLabel}>Fark</Text>
            <Text
              style={[
                styles.statValue,
                { color: cashReport?.difference === 0 ? 'green' : 'red' },
              ]}
            >
              {formatCurrency(cashReport?.difference || 0)}
            </Text>
          </View>
        </View>
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>En Çok Satan Ürünler</Text>
        {salesReport?.topProducts.map((product, index) => (
          <View key={index} style={styles.productRow}>
            <Text style={styles.productName}>{product.name}</Text>
            <View style={styles.productStats}>
              <Text style={styles.productQuantity}>{product.quantity} adet</Text>
              <Text style={styles.productRevenue}>
                {formatCurrency(product.revenue)}
              </Text>
            </View>
          </View>
        ))}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
    padding: 10,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  periodSelector: {
    flexDirection: 'row',
    marginBottom: 15,
    backgroundColor: 'white',
    borderRadius: 8,
    padding: 4,
  },
  periodButton: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 6,
  },
  selectedPeriod: {
    backgroundColor: '#007AFF',
  },
  periodButtonText: {
    color: '#666',
    fontWeight: '500',
  },
  selectedPeriodText: {
    color: 'white',
  },
  card: {
    backgroundColor: 'white',
    borderRadius: 8,
    padding: 15,
    marginBottom: 15,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
  },
  statRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  stat: {
    flex: 1,
    alignItems: 'center',
  },
  statLabel: {
    color: '#666',
    fontSize: 12,
    marginBottom: 5,
  },
  statValue: {
    fontSize: 16,
    fontWeight: 'bold',
  },
  productRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  productName: {
    flex: 1,
    fontSize: 14,
  },
  productStats: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  productQuantity: {
    color: '#666',
    marginRight: 15,
  },
  productRevenue: {
    fontWeight: 'bold',
    color: '#007AFF',
  },
}); 