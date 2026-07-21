// Bu ekran, kasiyerin tek tuşla gün sonu (Tagesende) raporu almasını ve eski raporları görmesini sağlar. Rol bazlı erişim uygulanır.
import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, TouchableOpacity, FlatList, Alert } from 'react-native';

import { useAuth } from '../../contexts/AuthContext';

// Örnek rapor tipi
interface DailyReport {
  id: string;
  date: string;
  cashier: string;
  totalAmount: number;
  status: 'closed' | 'open';
  downloadLinks?: {
    csv?: string;
    pdf?: string;
    json?: string;
  };
}

const ReportsScreen = () => {
  const { user } = useAuth();
  const { t } = useTranslation('reports');
  const [reports, setReports] = useState<DailyReport[]>([]);
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);

  // Demo is user flag (IsDemo), not a role name
  const isDemo = user?.isDemo === true;

  // Raporları getir (örnek veri ile simüle)
  useEffect(() => {
    setLoading(true);
    // TODO: API'den gerçek veri çekilecek
    setTimeout(() => {
      setReports([
        {
          id: '1',
          date: '2024-07-18',
          cashier: 'Kasiyer1',
          totalAmount: 1234.56,
          status: 'closed',
          downloadLinks: {
            csv: '#',
            pdf: '#',
            json: '#',
          },
        },
        {
          id: '2',
          date: '2024-07-17',
          cashier: 'Kasiyer2',
          totalAmount: 987.65,
          status: 'closed',
        },
      ]);
      setLoading(false);
    }, 800);
  }, []);

  // Gün sonu raporu başlat
  const handleCreateReport = () => {
    if (isDemo) {
      Alert.alert(t('demoMode'), t('demoUsersCannotCreate'));
      return;
    }
    setCreating(true);
    // TODO: API çağrısı ile gün sonu raporu başlat
    setTimeout(() => {
      Alert.alert(t('success'), t('reportCreated'));
      setCreating(false);
    }, 1200);
  };

  // Rapor satırı
  const renderReport = ({ item }: { item: DailyReport }) => (
    <View style={styles.reportRow}>
      <Text style={styles.reportDate}>{item.date}</Text>
      <Text style={styles.reportCashier}>{item.cashier}</Text>
      <Text style={styles.reportAmount}>{item.totalAmount.toFixed(2)} €</Text>
      {item.downloadLinks && (
        <View style={styles.downloadRow}>
          {item.downloadLinks.csv && (
            <TouchableOpacity
              style={styles.downloadBtn}
              onPress={() => {
                Alert.alert(t('csvDownload'));
              }}>
              <Text style={styles.downloadText}>{t('csv')}</Text>
            </TouchableOpacity>
          )}
          {item.downloadLinks.pdf && (
            <TouchableOpacity
              style={styles.downloadBtn}
              onPress={() => {
                Alert.alert(t('pdfDownload'));
              }}>
              <Text style={styles.downloadText}>{t('pdf')}</Text>
            </TouchableOpacity>
          )}
          {item.downloadLinks.json && (
            <TouchableOpacity
              style={styles.downloadBtn}
              onPress={() => {
                Alert.alert(t('jsonDownload'));
              }}>
              <Text style={styles.downloadText}>{t('json')}</Text>
            </TouchableOpacity>
          )}
        </View>
      )}
    </View>
  );

  // Yetki mesajı (canonical role names from backend)
  const role = user?.role ?? '';
  const isCashierLike = role === 'Cashier' || role === 'Waiter';
  const isAdminLike = role === 'SuperAdmin' || role === 'Manager';
  let infoMsg = '';
  if (isDemo) infoMsg = t('demoInfo');
  else if (isCashierLike) infoMsg = t('cashierInfo');
  else if (isAdminLike) infoMsg = t('adminInfo');

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('dailyClosingTitle')}</Text>
      <Text style={styles.infoMsg}>{infoMsg}</Text>
      {!isDemo && (
        <TouchableOpacity style={styles.createBtn} onPress={handleCreateReport} disabled={creating}>
          <Text style={styles.createBtnText}>{creating ? t('creating') : t('createBtn')}</Text>
        </TouchableOpacity>
      )}
      <FlatList
        data={reports}
        keyExtractor={(item) => item.id}
        renderItem={renderReport}
        style={styles.list}
        refreshing={loading}
        onRefresh={() => {}}
      />
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
    padding: 16,
  },
  title: {
    fontSize: 22,
    fontWeight: 'bold',
    marginBottom: 8,
    textAlign: 'center',
  },
  infoMsg: {
    color: '#1976d2',
    fontSize: 14,
    marginBottom: 12,
    textAlign: 'center',
  },
  createBtn: {
    backgroundColor: '#1976d2',
    padding: 12,
    borderRadius: 8,
    marginBottom: 16,
    alignItems: 'center',
  },
  createBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 16,
  },
  list: {
    marginTop: 8,
  },
  reportRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
    padding: 10,
    marginBottom: 8,
  },
  reportDate: {
    flex: 1,
    fontWeight: 'bold',
    fontSize: 15,
  },
  reportCashier: {
    flex: 1,
    fontSize: 14,
    color: '#555',
  },
  reportAmount: {
    flex: 1,
    fontSize: 15,
    color: '#388e3c',
    fontWeight: 'bold',
  },
  downloadRow: {
    flexDirection: 'row',
    marginLeft: 8,
  },
  downloadBtn: {
    backgroundColor: '#e0e0e0',
    borderRadius: 6,
    paddingHorizontal: 8,
    paddingVertical: 4,
    marginLeft: 4,
  },
  downloadText: {
    color: '#1976d2',
    fontWeight: 'bold',
    fontSize: 13,
  },
});

export default ReportsScreen;
