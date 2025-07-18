import AsyncStorage from '@react-native-async-storage/async-storage';

export enum UserRole {
  Cashier = 'Cashier',
  Admin = 'Admin',
  Manager = 'Manager'
}

export interface Permission {
  resource: string;
  action: string;
  roles: UserRole[];
}

export const PERMISSIONS: Permission[] = [
  // Kasiyer yetkileri
  { resource: 'sales', action: 'create', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'sales', action: 'view', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'products', action: 'view', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'cart', action: 'manage', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'payment', action: 'process', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'invoice', action: 'create', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'customers', action: 'view', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  { resource: 'barcode', action: 'scan', roles: [UserRole.Cashier, UserRole.Admin, UserRole.Manager] },
  
  // Admin yetkileri
  { resource: 'users', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'roles', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'system', action: 'settings', roles: [UserRole.Admin] },
  { resource: 'reports', action: 'view', roles: [UserRole.Admin, UserRole.Manager] },
  { resource: 'audit', action: 'view', roles: [UserRole.Admin, UserRole.Manager] },
  { resource: 'demo', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'hardware', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'inventory', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'finanzonline', action: 'manage', roles: [UserRole.Admin] },
  { resource: 'backup', action: 'create', roles: [UserRole.Admin] },
  
  // Manager yetkileri
  { resource: 'staff', action: 'manage', roles: [UserRole.Manager] },
  { resource: 'schedule', action: 'view', roles: [UserRole.Manager] },
  { resource: 'schedule', action: 'update', roles: [UserRole.Manager] }
];

export const SCREEN_ACCESS: Record<UserRole, string[]> = {
  [UserRole.Cashier]: [
    'SalesScreen',
    'ProductListScreen',
    'CartScreen', 
    'PaymentScreen',
    'InvoiceScreen',
    'CustomerScreen',
    'BarcodeScannerScreen',
    'TableSelectionScreen'
  ],
  [UserRole.Admin]: [
    // Tüm kasiyer ekranları
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
    // Admin özel ekranları
    'UserManagementScreen',
    'RoleManagementScreen',
    'SystemSettingsScreen', 
    'ReportsScreen',
    'AuditLogsScreen',
    'DemoUserManagementScreen',
    'HardwareManagementScreen',
    'InventoryManagementScreen',
    'FinanzOnlineScreen',
    'BackupRestoreScreen'
  ],
  [UserRole.Manager]: [
    // Kasiyer ekranları
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
    // Manager ekranları
    'ReportsScreen',
    'AuditLogsScreen',
    'InventoryScreen',
    'StaffManagementScreen',
    'ScheduleScreen'
  ]
};

export class PermissionHelper {
  private static currentUserRole: UserRole | null = null;

  static setUserRole(role: UserRole) {
    this.currentUserRole = role;
  }

  static async getUserRole(): Promise<UserRole | null> {
    if (this.currentUserRole) {
      return this.currentUserRole;
    }

    try {
      const role = await AsyncStorage.getItem('userRole');
      if (role) {
        this.currentUserRole = role as UserRole;
        return this.currentUserRole;
      }
    } catch (error) {
      console.error('Error getting user role:', error);
    }

    return null;
  }

  static hasPermission(resource: string, action: string): boolean {
    if (!this.currentUserRole) return false;

    const permission = PERMISSIONS.find(p => 
      p.resource === resource && p.action === action
    );

    return permission?.roles.includes(this.currentUserRole) ?? false;
  }

  static hasScreenAccess(screenName: string): boolean {
    if (!this.currentUserRole) return false;

    return SCREEN_ACCESS[this.currentUserRole]?.includes(screenName) ?? false;
  }

  static getAccessibleScreens(): string[] {
    if (!this.currentUserRole) return [];

    return SCREEN_ACCESS[this.currentUserRole] || [];
  }

  static getRolePermissions(): string[] {
    if (!this.currentUserRole) return [];

    return PERMISSIONS
      .filter(p => p.roles.includes(this.currentUserRole!))
      .map(p => `${p.resource}.${p.action}`);
  }

  static isDemoUser(): boolean {
    // Demo kullanıcı kontrolü
    return this.currentUserRole === UserRole.Cashier || this.currentUserRole === UserRole.Admin;
  }

  static getRoleDisplayName(role: UserRole): string {
    switch (role) {
      case UserRole.Cashier: return 'Kasiyer';
      case UserRole.Admin: return 'Yönetici';
      case UserRole.Manager: return 'Müdür';
      default: return 'Bilinmeyen';
    }
  }

  static getRoleDescription(role: UserRole): string {
    switch (role) {
      case UserRole.Cashier:
        return 'Satış, ödeme alma ve ürün görüntüleme işlemleri';
      case UserRole.Admin:
        return 'Tüm sistem yönetimi ve yönetici işlemleri';
      case UserRole.Manager:
        return 'Raporlama, denetim ve personel yönetimi';
      default:
        return 'Yetki tanımlanmamış';
    }
  }
} 