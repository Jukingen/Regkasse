import AsyncStorage from '@react-native-async-storage/async-storage';

/** Canonical roles – aligned with backend Roles.cs. */
export enum UserRole {
  SuperAdmin = 'SuperAdmin',
  Admin = 'Admin',
  Manager = 'Manager',
  Cashier = 'Cashier',
  Waiter = 'Waiter',
  Kitchen = 'Kitchen',
  ReportViewer = 'ReportViewer',
  Accountant = 'Accountant',
}

export interface Permission {
  resource: string;
  action: string;
  roles: UserRole[];
}

export const PERMISSIONS: Permission[] = [
  // POS – Cashier, Manager, Admin, SuperAdmin
  { resource: 'sales', action: 'create', roles: [UserRole.Cashier, UserRole.Waiter, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'sales', action: 'view', roles: [UserRole.Cashier, UserRole.Waiter, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'products', action: 'view', roles: [UserRole.Cashier, UserRole.Waiter, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'cart', action: 'manage', roles: [UserRole.Cashier, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'cart', action: 'view', roles: [UserRole.Waiter, UserRole.Cashier, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'payment', action: 'process', roles: [UserRole.Cashier, UserRole.Waiter, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'invoice', action: 'create', roles: [UserRole.Cashier, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'customers', action: 'view', roles: [UserRole.Cashier, UserRole.Waiter, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'barcode', action: 'scan', roles: [UserRole.Cashier, UserRole.Manager, UserRole.Admin, UserRole.SuperAdmin] },
  
  // Admin / SuperAdmin
  { resource: 'users', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'roles', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'system', action: 'settings', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'reports', action: 'view', roles: [UserRole.Admin, UserRole.SuperAdmin, UserRole.Manager] },
  { resource: 'audit', action: 'view', roles: [UserRole.Admin, UserRole.SuperAdmin, UserRole.Manager] },
  { resource: 'demo', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'hardware', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'inventory', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'finanzonline', action: 'manage', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  { resource: 'backup', action: 'create', roles: [UserRole.Admin, UserRole.SuperAdmin] },
  
  // Manager yetkileri
  { resource: 'staff', action: 'manage', roles: [UserRole.Manager] },
  { resource: 'schedule', action: 'view', roles: [UserRole.Manager] },
  { resource: 'schedule', action: 'update', roles: [UserRole.Manager] }
];

/**
 * Screen → required permission (resource.action). When user has permissions, use this for access.
 * Aligned with backend endpoint permission (e.g. report.view, audit.view, user.manage).
 */
export const SCREEN_REQUIRED_PERMISSION: Partial<Record<string, string>> = {
  SalesScreen: 'sale.view',
  ProductListScreen: 'product.view',
  CartScreen: 'cart.view',
  PaymentScreen: 'payment.view',
  InvoiceScreen: 'invoice.view',
  CustomerScreen: 'customer.view',
  BarcodeScannerScreen: 'product.view',
  TableSelectionScreen: 'table.view',
  UserManagementScreen: 'user.view',
  RoleManagementScreen: 'role.view',
  SystemSettingsScreen: 'settings.view',
  ReportsScreen: 'report.view',
  AuditLogsScreen: 'audit.view',
  DemoUserManagementScreen: 'user.manage',
  HardwareManagementScreen: 'settings.manage',
  InventoryManagementScreen: 'inventory.view',
  FinanzOnlineScreen: 'finanzonline.view',
  BackupRestoreScreen: 'settings.manage',
  InventoryScreen: 'inventory.view',
  StaffManagementScreen: 'user.view',
  ScheduleScreen: 'report.view',
};

/** Screen access by role – aligned with backend RolePermissionMatrix. Fallback when permissions not in token. */
export const SCREEN_ACCESS: Partial<Record<UserRole, string[]>> = {
  [UserRole.Cashier]: [
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
  ],
  [UserRole.Admin]: [
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
    'UserManagementScreen', 'RoleManagementScreen', 'SystemSettingsScreen',
    'ReportsScreen', 'AuditLogsScreen', 'DemoUserManagementScreen',
    'HardwareManagementScreen', 'InventoryManagementScreen', 'FinanzOnlineScreen', 'BackupRestoreScreen',
  ],
  [UserRole.SuperAdmin]: [
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
    'UserManagementScreen', 'RoleManagementScreen', 'SystemSettingsScreen',
    'ReportsScreen', 'AuditLogsScreen', 'DemoUserManagementScreen',
    'HardwareManagementScreen', 'InventoryManagementScreen', 'FinanzOnlineScreen', 'BackupRestoreScreen',
  ],
  [UserRole.Manager]: [
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'PaymentScreen',
    'InvoiceScreen', 'CustomerScreen', 'BarcodeScannerScreen', 'TableSelectionScreen',
    'ReportsScreen', 'AuditLogsScreen', 'InventoryScreen', 'StaffManagementScreen', 'ScheduleScreen',
  ],
  [UserRole.Waiter]: [
    'SalesScreen', 'ProductListScreen', 'CartScreen', 'CustomerScreen', 'TableSelectionScreen',
  ],
  [UserRole.Kitchen]: [],
  [UserRole.ReportViewer]: [],
  [UserRole.Accountant]: [],
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

  /** Permission-first: true if user has the permission required for this screen. */
  static hasScreenAccessByPermission(screenName: string, userPermissions: string[]): boolean {
    if (!userPermissions?.length) return false;
    const required = SCREEN_REQUIRED_PERMISSION[screenName];
    if (!required) return false;
    return userPermissions.includes(required);
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
      case UserRole.SuperAdmin: return 'SuperAdmin';
      case UserRole.Cashier: return 'Kasiyer';
      case UserRole.Admin: return 'Admin';
      case UserRole.Manager: return 'Müdür';
      case UserRole.Waiter: return 'Kellner';
      case UserRole.Kitchen: return 'Küche';
      case UserRole.ReportViewer: return 'Berichte';
      case UserRole.Accountant: return 'Buchhaltung';
      default: return role ?? 'Unbekannt';
    }
  }

  static getRoleDescription(role: UserRole): string {
    switch (role) {
      case UserRole.SuperAdmin: return 'Voller Systemzugriff inkl. systemkritischer Aktionen';
      case UserRole.Cashier: return 'Verkauf, Zahlung, Warenkorb, Katalog';
      case UserRole.Admin: return 'Backoffice, Benutzer, Katalog, Einstellungen, Berichte';
      case UserRole.Manager: return 'Betrieb, Berichte, Audit, Lager';
      case UserRole.Waiter: return 'Bestellung, Tische, Verkauf';
      case UserRole.Kitchen: return 'Küchenanzeige, Bestellstatus';
      case UserRole.ReportViewer: return 'Berichte und Audit anzeigen';
      case UserRole.Accountant: return 'Berichte, Audit, FinanzOnline anzeigen';
      default: return 'Rolle nicht definiert';
    }
  }
} 