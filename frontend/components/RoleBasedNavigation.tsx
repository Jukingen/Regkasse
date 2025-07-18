import { MaterialIcons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, ScrollView } from 'react-native';

import { PermissionHelper, UserRole } from '../shared/utils/PermissionHelper';

interface NavigationItem {
  id: string;
  title: string;
  icon: string;
  screen: string;
  roles: UserRole[];
  description: string;
}

const NAVIGATION_ITEMS: NavigationItem[] = [
  // Kasiyer menüleri
  {
    id: 'sales',
    title: 'Satış',
    icon: 'point-of-sale',
    screen: 'SalesScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Ürün satışı ve ödeme alma'
  },
  {
    id: 'products',
    title: 'Ürünler',
    icon: 'inventory',
    screen: 'ProductListScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Ürün listesi ve arama'
  },
  {
    id: 'cart',
    title: 'Sepet',
    icon: 'shopping-cart',
    screen: 'CartScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Sepet yönetimi'
  },
  {
    id: 'customers',
    title: 'Müşteriler',
    icon: 'people',
    screen: 'CustomerScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Müşteri bilgileri'
  },
  {
    id: 'barcode',
    title: 'Barkod Tarama',
    icon: 'qr-code-scanner',
    screen: 'BarcodeScannerScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Barkod/QR kod okuma'
  },
  {
    id: 'tables',
    title: 'Masalar',
    icon: 'table-restaurant',
    screen: 'TableSelectionScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'Masa seçimi ve yönetimi'
  },

  // Admin menüleri
  {
    id: 'users',
    title: 'Kullanıcı Yönetimi',
    icon: 'admin-panel-settings',
    screen: 'UserManagementScreen',
    roles: [UserRole.Admin],
    description: 'Kullanıcı hesapları yönetimi'
  },
  {
    id: 'roles',
    title: 'Rol Yönetimi',
    icon: 'security',
    screen: 'RoleManagementScreen',
    roles: [UserRole.Admin],
    description: 'Kullanıcı rolleri ve yetkileri'
  },
  {
    id: 'system',
    title: 'Sistem Ayarları',
    icon: 'settings',
    screen: 'SystemSettingsScreen',
    roles: [UserRole.Admin],
    description: 'Sistem konfigürasyonu'
  },
  {
    id: 'demo',
    title: 'Demo Kullanıcılar',
    icon: 'person-add',
    screen: 'DemoUserManagementScreen',
    roles: [UserRole.Admin],
    description: 'Demo kullanıcı yönetimi'
  },
  {
    id: 'hardware',
    title: 'Donanım Yönetimi',
    icon: 'devices',
    screen: 'HardwareManagementScreen',
    roles: [UserRole.Admin],
    description: 'Yazıcı ve TSE cihazları'
  },
  {
    id: 'inventory',
    title: 'Stok Yönetimi',
    icon: 'inventory-2',
    screen: 'InventoryManagementScreen',
    roles: [UserRole.Admin],
    description: 'Stok takibi ve güncelleme'
  },
  {
    id: 'finanzonline',
    title: 'FinanzOnline',
    icon: 'account-balance',
    screen: 'FinanzOnlineScreen',
    roles: [UserRole.Admin],
    description: 'FinanzOnline entegrasyonu'
  },
  {
    id: 'backup',
    title: 'Yedekleme',
    icon: 'backup',
    screen: 'BackupRestoreScreen',
    roles: [UserRole.Admin],
    description: 'Veri yedekleme ve geri yükleme'
  },

  // Manager menüleri
  {
    id: 'reports',
    title: 'Raporlar',
    icon: 'assessment',
    screen: 'ReportsScreen',
    roles: [UserRole.Admin, UserRole.Manager],
    description: 'Satış ve performans raporları'
  },
  {
    id: 'audit',
    title: 'Denetim Logları',
    icon: 'history',
    screen: 'AuditLogsScreen',
    roles: [UserRole.Admin, UserRole.Manager],
    description: 'Sistem aktivite logları'
  },
  {
    id: 'staff',
    title: 'Personel Yönetimi',
    icon: 'groups',
    screen: 'StaffManagementScreen',
    roles: [UserRole.Manager],
    description: 'Personel bilgileri ve vardiya'
  },
  {
    id: 'schedule',
    title: 'Vardiya Planı',
    icon: 'schedule',
    screen: 'ScheduleScreen',
    roles: [UserRole.Manager],
    description: 'Personel vardiya planlaması'
  }
];

interface RoleBasedNavigationProps {
  onNavigate: (screen: string) => void;
  currentScreen?: string;
}

export default function RoleBasedNavigation({ onNavigate, currentScreen }: RoleBasedNavigationProps) {
  const [userRole, setUserRole] = useState<UserRole | null>(null);
  const [accessibleItems, setAccessibleItems] = useState<NavigationItem[]>([]);

  useEffect(() => {
    loadUserRole();
  }, []);

  const loadUserRole = async () => {
    const role = await PermissionHelper.getUserRole();
    setUserRole(role);
    
    if (role) {
      const items = NAVIGATION_ITEMS.filter(item => item.roles.includes(role));
      setAccessibleItems(items);
    }
  };

  const handleNavigation = (item: NavigationItem) => {
    if (!PermissionHelper.hasScreenAccess(item.screen)) {
      Alert.alert(
        'Yetkisiz Erişim',
        'Bu ekrana erişim yetkiniz bulunmamaktadır.',
        [{ text: 'Tamam' }]
      );
      return;
    }

    onNavigate(item.screen);
  };

  const renderNavigationItem = (item: NavigationItem) => {
    const isActive = currentScreen === item.screen;
    const hasAccess = PermissionHelper.hasScreenAccess(item.screen);

    return (
      <TouchableOpacity
        key={item.id}
        style={[
          styles.navItem,
          isActive && styles.activeNavItem,
          !hasAccess && styles.disabledNavItem
        ]}
        onPress={() => handleNavigation(item)}
        disabled={!hasAccess}
      >
        <View style={styles.navItemContent}>
          <MaterialIcons
            name={item.icon as any}
            size={24}
            color={isActive ? '#fff' : hasAccess ? '#333' : '#ccc'}
          />
          <View style={styles.navItemText}>
            <Text style={[
              styles.navItemTitle,
              isActive && styles.activeNavItemTitle,
              !hasAccess && styles.disabledNavItemTitle
            ]}>
              {item.title}
            </Text>
            <Text style={[
              styles.navItemDescription,
              isActive && styles.activeNavItemDescription,
              !hasAccess && styles.disabledNavItemDescription
            ]}>
              {item.description}
            </Text>
          </View>
        </View>
        
        {!hasAccess && (
          <View style={styles.accessDeniedBadge}>
            <MaterialIcons name="block" size={16} color="#f44336" />
          </View>
        )}
      </TouchableOpacity>
    );
  };

  if (!userRole) {
    return (
      <View style={styles.loadingContainer}>
        <Text>Kullanıcı rolü yükleniyor...</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Menü</Text>
        <View style={styles.roleInfo}>
          <MaterialIcons 
            name={userRole === UserRole.Admin ? 'admin-panel-settings' : 'person'} 
            size={20} 
            color="#666" 
          />
          <Text style={styles.roleText}>
            {PermissionHelper.getRoleDisplayName(userRole)}
          </Text>
        </View>
      </View>

      <View style={styles.navSection}>
        <Text style={styles.sectionTitle}>Erişilebilir Ekranlar</Text>
        {accessibleItems.map(renderNavigationItem)}
      </View>

      {PermissionHelper.isDemoUser() && (
        <View style={styles.demoInfo}>
          <MaterialIcons name="info" size={16} color="#1976d2" />
          <Text style={styles.demoText}>
            Demo kullanıcısı olarak giriş yaptınız. Sadece rolünüze uygun işlemleri gerçekleştirebilirsiniz.
          </Text>
        </View>
      )}
    </ScrollView>
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
  roleInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 8,
  },
  roleText: {
    marginLeft: 8,
    fontSize: 14,
    color: '#666',
  },
  navSection: {
    padding: 16,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 12,
  },
  navItem: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 16,
    marginBottom: 8,
    elevation: 1,
  },
  activeNavItem: {
    backgroundColor: '#1976d2',
  },
  disabledNavItem: {
    backgroundColor: '#f5f5f5',
  },
  navItemContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  navItemText: {
    marginLeft: 12,
    flex: 1,
  },
  navItemTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
  },
  activeNavItemTitle: {
    color: '#fff',
  },
  disabledNavItemTitle: {
    color: '#ccc',
  },
  navItemDescription: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  activeNavItemDescription: {
    color: '#e3f2fd',
  },
  disabledNavItemDescription: {
    color: '#ccc',
  },
  accessDeniedBadge: {
    position: 'absolute',
    top: 8,
    right: 8,
  },
  demoInfo: {
    flexDirection: 'row',
    backgroundColor: '#e3f2fd',
    padding: 12,
    margin: 16,
    borderRadius: 6,
    alignItems: 'center',
  },
  demoText: {
    fontSize: 12,
    color: '#1976d2',
    marginLeft: 8,
    flex: 1,
  },
}); 