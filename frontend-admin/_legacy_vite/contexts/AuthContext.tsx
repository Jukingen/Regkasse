import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import api from '../services/api';

// Kullanıcı tipi
interface User {
  id: string;
  name: string;
  email: string;
  role: string;
}

// AuthContext tipi
interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  loading: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

// Varsayılan değerler
const defaultAuthContext: AuthContextType = {
  user: null,
  isAuthenticated: false,
  loading: true,
  error: null,
  login: async () => {},
  logout: () => {},
};

// Context oluştur
const AuthContext = createContext<AuthContextType>(defaultAuthContext);

// Provider bileşeni
export function AuthProvider({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Kullanıcı oturumunu kontrol et
  useEffect(() => {
    const checkAuth = async () => {
      const token = localStorage.getItem('token');
      if (!token) {
        setLoading(false);
        return;
      }

      try {
        const response = await api.get('/api/auth/me');
        setUser(response.data);
      } catch (err) {
        localStorage.removeItem('token');
        localStorage.removeItem('refreshToken');
      } finally {
        setLoading(false);
      }
    };

    checkAuth();
  }, []);

  // Giriş yap
  const login = async (email: string, password: string) => {
    setLoading(true);
    setError(null);

    try {
      const response = await api.post('/api/auth/login', { email, password });
      const { token, refreshToken, user } = response.data;

      localStorage.setItem('token', token);
      localStorage.setItem('refreshToken', refreshToken);
      setUser(user);
      setError(null);
      
      // Eğer admin değilse dili zorla
      if (user.role !== 'admin') {
        localStorage.setItem('language', 'de');
        import('../i18n').then(i18n => i18n.default.changeLanguage('de'));
      }
    } catch (err) {
      setError(t('auth.loginError'));
      throw err;
    } finally {
      setLoading(false);
    }
  };

  // Çıkış yap
  const logout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    setUser(null);
    setError(null);
    // Login sayfasına yönlendir
    window.location.href = '/login';
  };

  const value = {
    user,
    isAuthenticated: !!user,
    loading,
    error,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// Hook
export const useAuth = () => useContext(AuthContext);

export default AuthProvider;
