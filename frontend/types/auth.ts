// Kullanıcı rolleri - Backend ile uyumlu
export type UserRole = 'Admin' | 'Cashier' | 'Manager';

// Sistem yetkileri - Backend ile uyumlu
export interface Permission {
  id: number;
  name: string;
  description: string;
  resource: string; // users, products, sales, receipts, etc.
  action: string;   // create, read, update, delete, export
  isActive: boolean;
}

// Kullanıcı bilgileri
export interface User {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  employeeNumber: string;
  role: UserRole;
  accountType: 'real' | 'demo';
  isDemo: boolean;
  isActive: boolean;
  permissions: string[]; // resource.action formatında
  createdAt: string;
}

// Kimlik doğrulama durumu
export interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

// Giriş isteği
export interface LoginRequest {
  username: string;
  password: string;
}

// Giriş yanıtı
export interface LoginResponse {
  user: User;
  token: string;
  expiresAt: string;
}

// Yetki kontrolü için hook
export interface UsePermissionReturn {
  hasPermission: (resource: string, action: string) => boolean;
  hasRole: (role: UserRole) => boolean;
  isAdmin: boolean;
  isCashier: boolean;
  isManager: boolean;
}

// Rol tabanlı erişim kontrolü için bileşen props
export interface RoleGuardProps {
  role: UserRole;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

// Yetki tabanlı erişim kontrolü için bileşen props
export interface PermissionGuardProps {
  resource: string;
  action: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
} 