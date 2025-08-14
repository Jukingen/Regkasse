import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../contexts/AuthContext';
import {
  performDailyClosing,
  performMonthlyClosing,
  performYearlyClosing,
  canPerformClosing,
  getClosingHistory,
  getClosingStatistics,
  formatClosingDate,
  getClosingTypeDisplayName,
  getClosingStatusDisplayName,
  type DailyClosingRequest,
  type TagesabschlussResult,
  type ClosingHistoryItem,
  type ClosingStatistics
} from '../services/api/tagesabschlussService';

interface TagesabschlussModalProps {
  visible: boolean;
  onClose: () => void;
  cashRegisterId: string;
}

const TagesabschlussModal: React.FC<TagesabschlussModalProps> = ({
  visible,
  onClose,
  cashRegisterId
}) => {
  const { t } = useTranslation();
  const { user } = useAuth();
  
  const [loading, setLoading] = useState(false);
  const [canClose, setCanClose] = useState(false);
  const [lastClosingDate, setLastClosingDate] = useState<string | null>(null);
  const [closingHistory, setClosingHistory] = useState<ClosingHistoryItem[]>([]);
  const [statistics, setStatistics] = useState<ClosingStatistics | null>(null);
  const [activeTab, setActiveTab] = useState<'closing' | 'history' | 'statistics'>('closing');

  useEffect(() => {
    if (visible) {
      checkClosingStatus();
      loadClosingHistory();
      loadStatistics();
    }
  }, [visible, cashRegisterId]);

  const checkClosingStatus = async () => {
    try {
      const response = await canPerformClosing(cashRegisterId);
      setCanClose(response.canClose);
      setLastClosingDate(response.lastClosingDate || null);
    } catch (error) {
      console.error('Failed to check closing status:', error);
    }
  };

  const loadClosingHistory = async () => {
    try {
      const history = await getClosingHistory();
      setClosingHistory(history);
    } catch (error) {
      console.error('Failed to load closing history:', error);
    }
  };

  const loadStatistics = async () => {
    try {
      const stats = await getClosingStatistics();
      setStatistics(stats);
    } catch (error) {
      console.error('Failed to load statistics:', error);
    }
  };

  const handleDailyClosing = async () => {
    if (!canClose) {
      Alert.alert(
        t('tagesabschluss.alreadyClosed', 'Already Closed'),
        t('tagesabschluss.dailyAlreadyClosed', 'Daily closing has already been performed for today.')
      );
      return;
    }

    setLoading(true);
    try {
      const request: DailyClosingRequest = { cashRegisterId };
      const result = await performDailyClosing(request);

      if (result.success) {
        Alert.alert(
          t('tagesabschluss.success', 'Success'),
          t('tagesabschluss.dailyClosingSuccess', 'Daily closing completed successfully!'),
          [{ text: 'OK', onPress: () => {
            checkClosingStatus();
            loadClosingHistory();
            loadStatistics();
          }}]
        );
      } else {
        Alert.alert(
          t('tagesabschluss.error', 'Error'),
          result.errorMessage || t('tagesabschluss.dailyClosingFailed', 'Daily closing failed.')
        );
      }
    } catch (error) {
      Alert.alert(
        t('tagesabschluss.error', 'Error'),
        t('tagesabschluss.dailyClosingFailed', 'Daily closing failed.')
      );
    } finally {
      setLoading(false);
    }
  };

  const handleMonthlyClosing = async () => {
    setLoading(true);
    try {
      const request: DailyClosingRequest = { cashRegisterId };
      const result = await performMonthlyClosing(request);

      if (result.success) {
        Alert.alert(
          t('tagesabschluss.success', 'Success'),
          t('tagesabschluss.monthlyClosingSuccess', 'Monthly closing completed successfully!'),
          [{ text: 'OK', onPress: () => {
            loadClosingHistory();
            loadStatistics();
          }}]
        );
      } else {
        Alert.alert(
          t('tagesabschluss.error', 'Error'),
          result.errorMessage || t('tagesabschluss.monthlyClosingFailed', 'Monthly closing failed.')
        );
      }
    } catch (error) {
      Alert.alert(
        t('tagesabschluss.error', 'Error'),
        t('tagesabschluss.monthlyClosingFailed', 'Monthly closing failed.')
      );
    } finally {
      setLoading(false);
    }
  };

  const handleYearlyClosing = async () => {
    setLoading(true);
    try {
      const request: DailyClosingRequest = { cashRegisterId };
      const result = await performYearlyClosing(request);

      if (result.success) {
        Alert.alert(
          t('tagesabschluss.success', 'Success'),
          t('tagesabschluss.yearlyClosingSuccess', 'Yearly closing completed successfully!'),
          [{ text: 'OK', onPress: () => {
            loadClosingHistory();
            loadStatistics();
          }}]
        );
      } else {
        Alert.alert(
          t('tagesabschluss.error', 'Error'),
          result.errorMessage || t('tagesabschluss.yearlyClosingFailed', 'Yearly closing failed.')
        );
      }
    } catch (error) {
      Alert.alert(
        t('tagesabschluss.error', 'Error'),
        t('tagesabschluss.yearlyClosingFailed', 'Yearly closing failed.')
      );
    } finally {
      setLoading(false);
    }
  };

  const renderClosingTab = () => (
    <View style={styles.tabContent}>
      <View style={styles.statusSection}>
        <Text style={styles.statusTitle}>
          {t('tagesabschluss.status', 'Status')}
        </Text>
        <View style={styles.statusRow}>
          <Text style={styles.statusLabel}>
            {t('tagesabschluss.canClose', 'Can Close:')}
          </Text>
          <Text style={[styles.statusValue, { color: canClose ? '#4CAF50' : '#F44336' }]}>
            {canClose ? t('common.yes', 'Yes') : t('common.no', 'No')}
          </Text>
        </View>
        {lastClosingDate && (
          <View style={styles.statusRow}>
            <Text style={styles.statusLabel}>
              {t('tagesabschluss.lastClosing', 'Last Closing:')}
            </Text>
            <Text style={styles.statusValue}>
              {formatClosingDate(lastClosingDate)}
            </Text>
          </View>
        )}
      </View>

      <View style={styles.actionsSection}>
        <Text style={styles.sectionTitle}>
          {t('tagesabschluss.actions', 'Actions')}
        </Text>
        
        <TouchableOpacity
          style={[styles.actionButton, !canClose && styles.actionButtonDisabled]}
          onPress={handleDailyClosing}
          disabled={!canClose || loading}
        >
          <Text style={styles.actionButtonText}>
            {t('tagesabschluss.performDaily', 'Perform Daily Closing')}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.actionButton, styles.actionButtonSecondary]}
          onPress={handleMonthlyClosing}
          disabled={loading}
        >
          <Text style={styles.actionButtonText}>
            {t('tagesabschluss.performMonthly', 'Perform Monthly Closing')}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.actionButton, styles.actionButtonSecondary]}
          onPress={handleYearlyClosing}
          disabled={loading}
        >
          <Text style={styles.actionButtonText}>
            {t('tagesabschluss.performYearly', 'Perform Yearly Closing')}
          </Text>
        </TouchableOpacity>
      </View>

      {loading && (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#007AFF" />
          <Text style={styles.loadingText}>
            {t('tagesabschluss.processing', 'Processing...')}
          </Text>
        </View>
      )}
    </View>
  );

  const renderHistoryTab = () => (
    <View style={styles.tabContent}>
      <Text style={styles.sectionTitle}>
        {t('tagesabschluss.closingHistory', 'Closing History')}
      </Text>
      
      {closingHistory.length === 0 ? (
        <Text style={styles.noDataText}>
          {t('tagesabschluss.noHistory', 'No closing history available.')}
        </Text>
      ) : (
        <ScrollView style={styles.historyList}>
          {closingHistory.map((item, index) => (
            <View key={index} style={styles.historyItem}>
              <View style={styles.historyHeader}>
                <Text style={styles.historyType}>
                  {getClosingTypeDisplayName(item.closingType)}
                </Text>
                <Text style={[styles.historyStatus, { color: item.status === 'Completed' ? '#4CAF50' : '#F44336' }]}>
                  {getClosingStatusDisplayName(item.status)}
                </Text>
              </View>
              
              <Text style={styles.historyDate}>
                {formatClosingDate(item.closingDate)}
              </Text>
              
              <View style={styles.historyDetails}>
                <Text style={styles.historyDetail}>
                  {t('tagesabschluss.totalAmount', 'Total:')} €{item.totalAmount.toFixed(2)}
                </Text>
                <Text style={styles.historyDetail}>
                  {t('tagesabschluss.transactions', 'Transactions:')} {item.transactionCount}
                </Text>
              </View>
              
              {item.finanzOnlineStatus && (
                <Text style={styles.finanzOnlineStatus}>
                  FinanzOnline: {item.finanzOnlineStatus}
                </Text>
              )}
            </View>
          ))}
        </ScrollView>
      )}
    </View>
  );

  const renderStatisticsTab = () => (
    <View style={styles.tabContent}>
      <Text style={styles.sectionTitle}>
        {t('tagesabschluss.statistics', 'Statistics')}
      </Text>
      
      {statistics ? (
        <View style={styles.statisticsContainer}>
          <View style={styles.statRow}>
            <Text style={styles.statLabel}>
              {t('tagesabschluss.totalClosings', 'Total Closings:')}
            </Text>
            <Text style={styles.statValue}>{statistics.totalClosings}</Text>
          </View>
          
          <View style={styles.statRow}>
            <Text style={styles.statLabel}>
              {t('tagesabschluss.totalAmount', 'Total Amount:')}
            </Text>
            <Text style={styles.statValue}>€{statistics.totalAmount.toFixed(2)}</Text>
          </View>
          
          <View style={styles.statRow}>
            <Text style={styles.statLabel}>
              {t('tagesabschluss.totalTax', 'Total Tax:')}
            </Text>
            <Text style={styles.statValue}>€{statistics.totalTaxAmount.toFixed(2)}</Text>
          </View>
          
          <View style={styles.statRow}>
            <Text style={styles.statLabel}>
              {t('tagesabschluss.totalTransactions', 'Total Transactions:')}
            </Text>
            <Text style={styles.statValue}>{statistics.totalTransactions}</Text>
          </View>
          
          <View style={styles.statRow}>
            <Text style={styles.statLabel}>
              {t('tagesabschluss.averageDaily', 'Average Daily:')}
            </Text>
            <Text style={styles.statValue}>€{statistics.averageDailyAmount.toFixed(2)}</Text>
          </View>
          
          {statistics.lastClosingDate && (
            <View style={styles.statRow}>
              <Text style={styles.statLabel}>
                {t('tagesabschluss.lastClosing', 'Last Closing:')}
              </Text>
              <Text style={styles.statValue}>
                {formatClosingDate(statistics.lastClosingDate)}
              </Text>
            </View>
          )}
        </View>
      ) : (
        <Text style={styles.noDataText}>
          {t('tagesabschluss.noStatistics', 'No statistics available.')}
        </Text>
      )}
    </View>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.title}>
            {t('tagesabschluss.title', 'Tagesabschluss')}
          </Text>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Text style={styles.closeButtonText}>✕</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.tabBar}>
          <TouchableOpacity
            style={[styles.tab, activeTab === 'closing' && styles.activeTab]}
            onPress={() => setActiveTab('closing')}
          >
            <Text style={[styles.tabText, activeTab === 'closing' && styles.activeTabText]}>
              {t('tagesabschluss.closing', 'Closing')}
            </Text>
          </TouchableOpacity>
          
          <TouchableOpacity
            style={[styles.tab, activeTab === 'history' && styles.activeTab]}
            onPress={() => setActiveTab('history')}
          >
            <Text style={[styles.tabText, activeTab === 'history' && styles.activeTabText]}>
              {t('tagesabschluss.history', 'History')}
            </Text>
          </TouchableOpacity>
          
          <TouchableOpacity
            style={[styles.tab, activeTab === 'statistics' && styles.activeTab]}
            onPress={() => setActiveTab('statistics')}
          >
            <Text style={[styles.tabText, activeTab === 'statistics' && styles.activeTabText]}>
              {t('tagesabschluss.statistics', 'Statistics')}
            </Text>
          </TouchableOpacity>
        </View>

        {activeTab === 'closing' && renderClosingTab()}
        {activeTab === 'history' && renderHistoryTab()}
        {activeTab === 'statistics' && renderStatisticsTab()}
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
  },
  closeButton: {
    width: 30,
    height: 30,
    justifyContent: 'center',
    alignItems: 'center',
  },
  closeButtonText: {
    fontSize: 20,
    color: '#666',
  },
  tabBar: {
    flexDirection: 'row',
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  tab: {
    flex: 1,
    paddingVertical: 15,
    alignItems: 'center',
  },
  activeTab: {
    borderBottomWidth: 2,
    borderBottomColor: '#007AFF',
  },
  tabText: {
    fontSize: 16,
    color: '#666',
  },
  activeTabText: {
    color: '#007AFF',
    fontWeight: 'bold',
  },
  tabContent: {
    flex: 1,
    padding: 20,
  },
  statusSection: {
    backgroundColor: '#fff',
    padding: 20,
    borderRadius: 10,
    marginBottom: 20,
  },
  statusTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
    color: '#333',
  },
  statusRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  statusLabel: {
    fontSize: 16,
    color: '#666',
  },
  statusValue: {
    fontSize: 16,
    fontWeight: 'bold',
  },
  actionsSection: {
    backgroundColor: '#fff',
    padding: 20,
    borderRadius: 10,
    marginBottom: 20,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
    color: '#333',
  },
  actionButton: {
    backgroundColor: '#007AFF',
    paddingVertical: 15,
    paddingHorizontal: 20,
    borderRadius: 8,
    marginBottom: 15,
    alignItems: 'center',
  },
  actionButtonSecondary: {
    backgroundColor: '#34C759',
  },
  actionButtonDisabled: {
    backgroundColor: '#ccc',
  },
  actionButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: 'bold',
  },
  loadingContainer: {
    alignItems: 'center',
    padding: 20,
  },
  loadingText: {
    marginTop: 10,
    fontSize: 16,
    color: '#666',
  },
  historyList: {
    flex: 1,
  },
  historyItem: {
    backgroundColor: '#fff',
    padding: 15,
    borderRadius: 8,
    marginBottom: 10,
  },
  historyHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  historyType: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  historyStatus: {
    fontSize: 14,
    fontWeight: 'bold',
  },
  historyDate: {
    fontSize: 14,
    color: '#666',
    marginBottom: 10,
  },
  historyDetails: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 5,
  },
  historyDetail: {
    fontSize: 14,
    color: '#666',
  },
  finanzOnlineStatus: {
    fontSize: 12,
    color: '#999',
    fontStyle: 'italic',
  },
  noDataText: {
    textAlign: 'center',
    fontSize: 16,
    color: '#666',
    marginTop: 50,
  },
  statisticsContainer: {
    backgroundColor: '#fff',
    padding: 20,
    borderRadius: 10,
  },
  statRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  statLabel: {
    fontSize: 16,
    color: '#666',
  },
  statValue: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
});

export default TagesabschlussModal;
