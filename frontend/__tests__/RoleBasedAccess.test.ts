import { PermissionHelper, UserRole } from '../shared/utils/PermissionHelper';

// Mock AsyncStorage
jest.mock('@react-native-async-storage/async-storage', () => ({
  getItem: jest.fn(),
  setItem: jest.fn(),
  removeItem: jest.fn(),
}));

describe('RoleBasedAccess Tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    PermissionHelper.setUserRole(null);
  });

  describe('Demo Cashier User Tests', () => {
    beforeEach(() => {
      PermissionHelper.setUserRole(UserRole.Cashier);
    });

    test('Kasiyer demo kullanıcısı satış ekranına erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('SalesScreen')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı ürün listesine erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('ProductListScreen')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı sepet ekranına erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('CartScreen')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı ödeme ekranına erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('PaymentScreen')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı admin paneline erişememeli', () => {
      expect(PermissionHelper.hasScreenAccess('UserManagementScreen')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı rol yönetimi ekranına erişememeli', () => {
      expect(PermissionHelper.hasScreenAccess('RoleManagementScreen')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı sistem ayarlarına erişememeli', () => {
      expect(PermissionHelper.hasScreenAccess('SystemSettingsScreen')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı demo kullanıcı yönetimine erişememeli', () => {
      expect(PermissionHelper.hasScreenAccess('DemoUserManagementScreen')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı satış işlemi yapabilmeli', () => {
      expect(PermissionHelper.hasPermission('sales', 'create')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı ürün görüntüleyebilmeli', () => {
      expect(PermissionHelper.hasPermission('products', 'view')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı sepet yönetebilmeli', () => {
      expect(PermissionHelper.hasPermission('cart', 'manage')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı ödeme işlemi yapabilmeli', () => {
      expect(PermissionHelper.hasPermission('payment', 'process')).toBe(true);
    });

    test('Kasiyer demo kullanıcısı kullanıcı yönetimi yapamamalı', () => {
      expect(PermissionHelper.hasPermission('users', 'manage')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı demo kullanıcı yönetimi yapamamalı', () => {
      expect(PermissionHelper.hasPermission('demo', 'manage')).toBe(false);
    });

    test('Kasiyer demo kullanıcısı demo kullanıcı olarak tanınmalı', () => {
      expect(PermissionHelper.isDemoUser()).toBe(true);
    });
  });

  describe('Demo Admin User Tests', () => {
    beforeEach(() => {
      PermissionHelper.setUserRole(UserRole.Admin);
    });

    test('Admin demo kullanıcısı tüm ekranlara erişebilmeli', () => {
      const allScreens = [
        'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
        'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
        'UserManagementScreen', 'RoleManagementScreen', 'SystemSettingsScreen',
        'ReportsScreen', 'AuditLogsScreen', 'DemoUserManagementScreen',
        'HardwareManagementScreen', 'InventoryManagementScreen', 'FinanzOnlineScreen',
        'BackupRestoreScreen'
      ];

      allScreens.forEach(screen => {
        expect(PermissionHelper.hasScreenAccess(screen)).toBe(true);
      });
    });

    test('Admin demo kullanıcısı tüm işlemleri yapabilmeli', () => {
      const allPermissions = [
        'sales.create', 'sales.view', 'sales.update', 'sales.delete',
        'users.manage', 'roles.manage', 'system.settings', 'demo.manage',
        'reports.view', 'audit.view', 'hardware.manage', 'inventory.manage'
      ];

      allPermissions.forEach(permission => {
        const [resource, action] = permission.split('.');
        expect(PermissionHelper.hasPermission(resource, action)).toBe(true);
      });
    });

    test('Admin demo kullanıcısı kullanıcı yönetimi yapabilmeli', () => {
      expect(PermissionHelper.hasPermission('users', 'manage')).toBe(true);
    });

    test('Admin demo kullanıcısı demo kullanıcı yönetimi yapabilmeli', () => {
      expect(PermissionHelper.hasPermission('demo', 'manage')).toBe(true);
    });

    test('Admin demo kullanıcısı sistem ayarlarına erişebilmeli', () => {
      expect(PermissionHelper.hasPermission('system', 'settings')).toBe(true);
    });

    test('Admin demo kullanıcısı demo kullanıcı olarak tanınmalı', () => {
      expect(PermissionHelper.isDemoUser()).toBe(true);
    });
  });

  describe('Manager User Tests', () => {
    beforeEach(() => {
      PermissionHelper.setUserRole(UserRole.Manager);
    });

    test('Manager kasiyer ekranlarına erişebilmeli', () => {
      const cashierScreens = [
        'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
        'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen'
      ];

      cashierScreens.forEach(screen => {
        expect(PermissionHelper.hasScreenAccess(screen)).toBe(true);
      });
    });

    test('Manager rapor ekranlarına erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('ReportsScreen')).toBe(true);
      expect(PermissionHelper.hasScreenAccess('AuditLogsScreen')).toBe(true);
    });

    test('Manager personel yönetimi ekranlarına erişebilmeli', () => {
      expect(PermissionHelper.hasScreenAccess('StaffManagementScreen')).toBe(true);
      expect(PermissionHelper.hasScreenAccess('ScheduleScreen')).toBe(true);
    });

    test('Manager admin ekranlarına erişememeli', () => {
      expect(PermissionHelper.hasScreenAccess('UserManagementScreen')).toBe(false);
      expect(PermissionHelper.hasScreenAccess('RoleManagementScreen')).toBe(false);
      expect(PermissionHelper.hasScreenAccess('SystemSettingsScreen')).toBe(false);
    });

    test('Manager personel yönetimi yapabilmeli', () => {
      expect(PermissionHelper.hasPermission('staff', 'manage')).toBe(true);
    });

    test('Manager vardiya planı güncelleyebilmeli', () => {
      expect(PermissionHelper.hasPermission('schedule', 'update')).toBe(true);
    });

    test('Manager kullanıcı yönetimi yapamamalı', () => {
      expect(PermissionHelper.hasPermission('users', 'manage')).toBe(false);
    });
  });

  describe('Unauthorized Access Tests', () => {
    test('Rol tanımlanmamış kullanıcı hiçbir ekrana erişememeli', () => {
      PermissionHelper.setUserRole(null);
      
      const allScreens = [
        'SalesScreen', 'UserManagementScreen', 'ReportsScreen'
      ];

      allScreens.forEach(screen => {
        expect(PermissionHelper.hasScreenAccess(screen)).toBe(false);
      });
    });

    test('Rol tanımlanmamış kullanıcı hiçbir işlem yapamamalı', () => {
      PermissionHelper.setUserRole(null);
      
      const allPermissions = [
        'sales.create', 'users.manage', 'reports.view'
      ];

      allPermissions.forEach(permission => {
        const [resource, action] = permission.split('.');
        expect(PermissionHelper.hasPermission(resource, action)).toBe(false);
      });
    });
  });

  describe('Role Display Tests', () => {
    test('Rol görüntüleme isimleri doğru olmalı', () => {
      expect(PermissionHelper.getRoleDisplayName(UserRole.Cashier)).toBe('Kasiyer');
      expect(PermissionHelper.getRoleDisplayName(UserRole.Admin)).toBe('Yönetici');
      expect(PermissionHelper.getRoleDisplayName(UserRole.Manager)).toBe('Müdür');
    });

    test('Rol açıklamaları doğru olmalı', () => {
      expect(PermissionHelper.getRoleDescription(UserRole.Cashier))
        .toContain('Satış, ödeme alma');
      expect(PermissionHelper.getRoleDescription(UserRole.Admin))
        .toContain('Tüm sistem yönetimi');
      expect(PermissionHelper.getRoleDescription(UserRole.Manager))
        .toContain('Raporlama, denetim');
    });
  });

  describe('Accessible Screens Tests', () => {
    test('Kasiyer erişilebilir ekranları doğru olmalı', () => {
      PermissionHelper.setUserRole(UserRole.Cashier);
      const screens = PermissionHelper.getAccessibleScreens();
      
      expect(screens).toContain('SalesScreen');
      expect(screens).toContain('ProductListScreen');
      expect(screens).toContain('CartScreen');
      expect(screens).not.toContain('UserManagementScreen');
    });

    test('Admin erişilebilir ekranları doğru olmalı', () => {
      PermissionHelper.setUserRole(UserRole.Admin);
      const screens = PermissionHelper.getAccessibleScreens();
      
      expect(screens).toContain('SalesScreen');
      expect(screens).toContain('UserManagementScreen');
      expect(screens).toContain('DemoUserManagementScreen');
    });
  });
}); 