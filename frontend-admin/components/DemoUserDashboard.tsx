import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Alert } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

interface DemoUserInfo {
  username: string;
  role: string;
  permissions: string[];
  accessibleScreens: string[];
  description: string;
  restrictions: string[];
}

const DEMO_USERS: DemoUserInfo[] = [
  {
    username: 'demo.cashier1',
    role: 'Cashier',
    permissions: [
      'Satış oluşturma',
      'Ürün görüntüleme',
      'Sepet yönetimi',
      'Ödeme alma',
      'Fatura oluşturma',
      'Müşteri bilgileri',
      'Barkod tarama',
      'Masa seçimi'
    ],
    accessibleScreens: [
      'SalesScreen',
      'ProductListScreen',
      'CartScreen',
      'PaymentScreen',
      'InvoiceScreen',
      'CustomerScreen',
      'BarcodeScannerScreen',
      'TableSelectionScreen'
    ],
    description: 'Sadece satış, ödeme alma ve ürün görüntüleme işlemleri',
    restrictions: [
      'Kullanıcı yönetimi',
      'Rapor görüntüleme',
      'Sistem ayarları',
      'Demo kullanıcı yönetimi',
      'Donanım yönetimi',
      'Stok yönetimi'
    ]
  },
  {
    username: 'demo.cashier2',
    role: 'Cashier',
    permissions: [
      'Satış oluşturma',
      'Ürün görüntüleme',
      'Sepet yönetimi',
      'Ödeme alma',
      'Fatura oluşturma',
      'Müşteri bilgileri',
      'Barkod tarama',
      'Masa seçimi'
    ],
    accessibleScreens: [
      'SalesScreen',
      'ProductListScreen',
      'CartScreen',
      'PaymentScreen',
      'InvoiceScreen',
      'CustomerScreen',
      'BarcodeScannerScreen',
      'TableSelectionScreen'
    ],
    description: 'Sadece satış, ödeme alma ve ürün görüntüleme işlemleri',
    restrictions: [
      'Kullanıcı yönetimi',
      'Rapor görüntüleme',
      'Sistem ayarları',
      'Demo kullanıcı yönetimi',
      'Donanım yönetimi',
      'Stok yönetimi'
    ]
  },
  {
    username: 'demo.admin1',
    role: 'Admin',
    permissions: [
      'Tüm kasiyer yetkileri',
      'Kullanıcı yönetimi',
      'Rol yönetimi',
      'Sistem ayarları',
      'Rapor görüntüleme',
      'Denetim logları',
      'Demo kullanıcı yönetimi',
      'Donanım yönetimi',
      'Stok yönetimi',
      'FinanzOnline yönetimi',
      'Yedekleme işlemleri'
    ],
    accessibleScreens: [
      'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
      'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
      'UserManagementScreen', 'RoleManagementScreen', 'SystemSettingsScreen',
      'ReportsScreen', 'AuditLogsScreen', 'DemoUserManagementScreen',
      'HardwareManagementScreen', 'InventoryManagementScreen', 'FinanzOnlineScreen',
      'BackupRestoreScreen'
    ],
    description: 'Tüm sistem yönetimi ve yönetici işlemleri',
    restrictions: ['Yok']
  },
  {
    username: 'demo.admin2',
    role: 'Admin',
    permissions: [
      'Tüm kasiyer yetkileri',
      'Kullanıcı yönetimi',
      'Rol yönetimi',
      'Sistem ayarları',
      'Rapor görüntüleme',
      'Denetim logları',
      'Demo kullanıcı yönetimi',
      'Donanım yönetimi',
      'Stok yönetimi',
      'FinanzOnline yönetimi',
      'Yedekleme işlemleri'
    ],
    accessibleScreens: [
      'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
      'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
      'UserManagementScreen', 'RoleManagementScreen', 'SystemSettingsScreen',
      'ReportsScreen', 'AuditLogsScreen', 'DemoUserManagementScreen',
      'HardwareManagementScreen', 'InventoryManagementScreen', 'FinanzOnlineScreen',
      'BackupRestoreScreen'
    ],
    description: 'Tüm sistem yönetimi ve yönetici işlemleri',
    restrictions: ['Yok']
  }
];

export default function DemoUserDashboard() {
  const [selectedUser, setSelectedUser] = useState<DemoUserInfo | null>(null);

  const renderUserCard = (user: DemoUserInfo) => (
    <TouchableOpacity
      key={user.username}
      style={[
        styles.userCard,
        selectedUser?.username === user.username && styles.selectedUserCard
      ]}
      onPress={() => setSelectedUser(user)}
    >
      <View style={styles.userHeader}>
        <MaterialIcons
          name={user.role === 'Admin' ? 'admin-panel-settings' : 'person'}
          size={32}
          color={user.role === 'Admin' ? '#1976d2' : '#7b1fa2'}
        />
        <View style={styles.userInfo}>
          <Text style={styles.userName}>{user.username}</Text>
          <Text style={styles.userRole}>Rol: {user.role}</Text>
          <Text style={styles.userDescription}>{user.description}</Text>
        </View>
      </View>
    </TouchableOpacity>
  );

  const renderUserDetails = (user: DemoUserInfo) => (
    <View style={styles.detailsContainer}>
      <View style={styles.detailsHeader}>
        <Text style={styles.detailsTitle}>{user.username} - Detaylar</Text>
        <TouchableOpacity onPress={() => setSelectedUser(null)}>
          <MaterialIcons name="close" size={24} color="#666" />
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.detailsContent}>
        {/* Yetkiler */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>
            <MaterialIcons name="check-circle" size={20} color="#4caf50" />
            Yetkiler
          </Text>
          {user.permissions.map((permission, index) => (
            <View key={index} style={styles.permissionItem}>
              <MaterialIcons name="check" size={16} color="#4caf50" />
              <Text style={styles.permissionText}>{permission}</Text>
            </View>
          ))}
        </View>

        {/* Erişilebilir Ekranlar */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>
            <MaterialIcons name="screen-share" size={20} color="#2196f3" />
            Erişilebilir Ekranlar
          </Text>
          <View style={styles.screenGrid}>
            {user.accessibleScreens.map((screen, index) => (
              <View key={index} style={styles.screenItem}>
                <Text style={styles.screenText}>{screen}</Text>
              </View>
            ))}
          </View>
        </View>

        {/* Kısıtlamalar */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>
            <MaterialIcons name="block" size={20} color="#f44336" />
            Erişim Kısıtlamaları
          </Text>
          {user.restrictions.map((restriction, index) => (
            <View key={index} style={styles.restrictionItem}>
              <MaterialIcons name="block" size={16} color="#f44336" />
              <Text style={styles.restrictionText}>{restriction}</Text>
            </View>
          ))}
        </View>

        {/* Test Butonları */}
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>
            <MaterialIcons name="bug-report" size={20} color="#ff9800" />
            Test Senaryoları
          </Text>
          <TouchableOpacity
            style={styles.testButton}
            onPress={() => Alert.alert('Test', `${user.username} ile giriş yapıp yetkilerini test edin`)}
          >
            <MaterialIcons name="login" size={20} color="#fff" />
            <Text style={styles.testButtonText}>Bu Kullanıcı ile Test Et</Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </View>
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Demo Kullanıcı Dashboard</Text>
        <Text style={styles.subtitle}>Demo kullanıcıların yetkilerini ve kısıtlamalarını görüntüleyin</Text>
      </View>

      <View style={styles.content}>
        {!selectedUser ? (
          <ScrollView>
            <View style={styles.userGrid}>
              {DEMO_USERS.map(renderUserCard)}
            </View>
          </ScrollView>
        ) : (
          renderUserDetails(selectedUser)
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    padding: 16,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  content: {
    flex: 1,
  },
  userGrid: {
    padding: 16,
  },
  userCard: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 16,
    marginBottom: 12,
    elevation: 2,
  },
  selectedUserCard: {
    borderColor: '#1976d2',
    borderWidth: 2,
  },
  userHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  userInfo: {
    marginLeft: 12,
    flex: 1,
  },
  userName: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  userRole: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  userDescription: {
    fontSize: 12,
    color: '#999',
    marginTop: 4,
  },
  detailsContainer: {
    flex: 1,
    backgroundColor: '#fff',
    margin: 16,
    borderRadius: 8,
    elevation: 2,
  },
  detailsHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  detailsTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  detailsContent: {
    flex: 1,
    padding: 16,
  },
  section: {
    marginBottom: 24,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
    flexDirection: 'row',
    alignItems: 'center',
  },
  permissionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  permissionText: {
    marginLeft: 8,
    fontSize: 14,
    color: '#333',
  },
  screenGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
  },
  screenItem: {
    backgroundColor: '#e3f2fd',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
    marginRight: 8,
    marginBottom: 8,
  },
  screenText: {
    fontSize: 12,
    color: '#1976d2',
  },
  restrictionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  restrictionText: {
    marginLeft: 8,
    fontSize: 14,
    color: '#f44336',
  },
  testButton: {
    backgroundColor: '#ff9800',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 12,
    borderRadius: 6,
  },
  testButtonText: {
    color: '#fff',
    fontWeight: 'bold',
    marginLeft: 8,
  },
}); 