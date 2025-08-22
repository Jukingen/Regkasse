import { MaterialIcons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Alert, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';

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
    title: 'navigation.sales',
    icon: 'point-of-sale',
    screen: 'SalesScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'navigation.sales'
  },
  {
    id: 'products',
    title: 'navigation.products',
    icon: 'inventory',
    screen: 'ProductListScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'navigation.products'
  },
  {
    id: 'cart',
    title: 'navigation.cart',
    icon: 'shopping-cart',
    screen: 'CartScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'navigation.cart'
  },
  {
    id: 'customers',
    title: 'navigation.customers',
    icon: 'people',
    screen: 'CustomerScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'navigation.customers'
  },

  {
    id: 'tables',
    title: 'navigation.tables',
    icon: 'table-restaurant',
    screen: 'TableSelectionScreen',
    roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager],
    description: 'navigation.tables'
  },

  // Admin menüleri
  {
    id: 'users',
    title: 'navigation.users',
    icon: 'admin-panel-settings',
    screen: 'UserManagementScreen',
    roles: [UserRole.Admin],
    description: 'navigation.users'
  },
  {
    id: 'roles',
    title: 'navigation.roles',
    icon: 'security',
    screen: 'RoleManagementScreen',
    roles: [UserRole.Admin],
    description: 'navigation.roles'
  },
  {
    id: 'system',
    title: 'navigation.system',
    icon: 'settings',
    screen: 'SystemSettingsScreen',
    roles: [UserRole.Admin],
    description: 'navigation.system'
  },
  {
    id: 'demo',
    title: 'navigation.demo',
    icon: 'person-add',
    screen: 'DemoUserManagementScreen',
    roles: [UserRole.Admin],
    description: 'navigation.demo'
  },
  {
    id: 'hardware',
    title: 'navigation.hardware',
    icon: 'devices',
    screen: 'HardwareManagementScreen',
    roles: [UserRole.Admin],
    description: 'navigation.hardware'
  },
  {
    id: 'inventory',
    title: 'navigation.inventory',
    icon: 'inventory-2',
    screen: 'InventoryManagementScreen',
    roles: [UserRole.Admin],
    description: 'navigation.inventory'
  },
  {
    id: 'finanzonline',
    title: 'navigation.finanzonline',
    icon: 'account-balance',
    screen: 'FinanzOnlineScreen',
    roles: [UserRole.Admin],
    description: 'navigation.finanzonline'
  },
  {
    id: 'backup',
    title: 'navigation.backup',
    icon: 'backup',
    screen: 'BackupRestoreScreen',
    roles: [UserRole.Admin],
    description: 'navigation.backup'
  },

  // Manager menüleri
  {
    id: 'reports',
    title: 'navigation.reports',
    icon: 'assessment',
    screen: 'ReportsScreen',
    roles: [UserRole.Admin, UserRole.Manager],
    description: 'navigation.reports'
  },
  {
    id: 'audit',
    title: 'navigation.audit',
    icon: 'history',
    screen: 'AuditLogsScreen',
    roles: [UserRole.Admin, UserRole.Manager],
    description: 'navigation.audit'
  },
  {
    id: 'staff',
    title: 'navigation.staff',
    icon: 'groups',
    screen: 'StaffManagementScreen',
    roles: [UserRole.Manager],
    description: 'navigation.staff'
  },
  {
    id: 'schedule',
    title: 'navigation.schedule',
    icon: 'schedule',
    screen: 'ScheduleScreen',
    roles: [UserRole.Manager],
    description: 'navigation.schedule'
  }
];

interface RoleBasedNavigationProps {
  onNavigate: (screen: string) => void;
  currentScreen?: string;
}

export default function RoleBasedNavigation({ onNavigate, currentScreen }: RoleBasedNavigationProps) {
  const [userRole, setUserRole] = useState<UserRole | null>(null);
  const [accessibleItems, setAccessibleItems] = useState<NavigationItem[]>([]);
  const { t } = useTranslation();

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
        t('errors.unauthorized', 'Yetkisiz Erişim'),
        t('errors.noAccess', 'Bu ekrana erişim yetkiniz bulunmamaktadır.'),
        [{ text: t('common.ok', 'Tamam') }]
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
              {t(item.title)}
            </Text>
            <Text style={[
              styles.navItemDescription,
              isActive && styles.activeNavItemDescription,
              !hasAccess && styles.disabledNavItemDescription
            ]}>
              {t(item.description)}
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
        <Text>{t('loading.roleLoading')}</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('navigation.menu')}</Text>
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
        <Text style={styles.sectionTitle}>{t('navigation.accessibleScreens')}</Text>
        {accessibleItems.map(renderNavigationItem)}
      </View>

      {PermissionHelper.isDemoUser() && (
        <View style={styles.demoInfo}>
          <MaterialIcons name="info" size={16} color="#1976d2" />
          <Text style={styles.demoText}>
            {t('navigation.demoUserInfo')}
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