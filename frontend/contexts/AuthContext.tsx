import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { jwtDecode } from 'jwt-decode';
import { api } from '../services/api';
import { router } from 'expo-router';

interface User {
    id: string;
    username: string;
    role: 'admin' | 'manager' | 'cashier';
    email: string;
}

interface AuthContextType {
    user: User | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    login: (username: string, password: string) => Promise<void>;
    logout: () => Promise<void>;
    checkAuthStatus: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [isLoading, setIsLoading] = useState(true);

    const checkTokenExpiration = (token: string): boolean => {
        try {
            const decoded = jwtDecode(token);
            const currentTime = Date.now() / 1000;
            return decoded.exp ? decoded.exp > currentTime : false;
        } catch {
            return false;
        }
    };

    const checkAuthStatus = async () => {
        try {
            const token = await AsyncStorage.getItem('token');
            const refreshToken = await AsyncStorage.getItem('refreshToken');

            if (!token || !refreshToken) {
                throw new Error('No tokens found');
            }

            // Token geçerliliğini kontrol et
            if (!checkTokenExpiration(token)) {
                // Token süresi dolmuşsa refresh token ile yenile
                const response = await api.post('/auth/refresh', { refreshToken });
                const { token: newToken, refreshToken: newRefreshToken } = response.data;

                await AsyncStorage.setItem('token', newToken);
                await AsyncStorage.setItem('refreshToken', newRefreshToken);

                // Kullanıcı bilgilerini güncelle
                const userResponse = await api.get('/auth/me', {
                    headers: { Authorization: `Bearer ${newToken}` }
                });
                setUser(userResponse.data);
                setIsAuthenticated(true);
            } else {
                // Token geçerliyse kullanıcı bilgilerini kontrol et
                const userResponse = await api.get('/auth/me', {
                    headers: { Authorization: `Bearer ${token}` }
                });
                setUser(userResponse.data);
                setIsAuthenticated(true);
            }
        } catch (error) {
            // Hata durumunda oturumu sonlandır
            await logout();
            router.replace('/(auth)/login');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        checkAuthStatus();
    }, []);

    const login = async (username: string, password: string) => {
        try {
            setIsLoading(true);
            const response = await api.post('/auth/login', { username, password });
            const { token, refreshToken, user } = response.data;

            await AsyncStorage.setItem('token', token);
            await AsyncStorage.setItem('refreshToken', refreshToken);

            setUser(user);
            setIsAuthenticated(true);

            // Kullanıcı rolüne göre yönlendirme
            if (user.role === 'admin' || user.role === 'manager') {
                router.replace('/(tabs)');
            } else {
                router.replace('/(tabs)/cash-register');
            }
        } catch (error) {
            throw new Error('Login failed');
        } finally {
            setIsLoading(false);
        }
    };

    const logout = async () => {
        try {
            const token = await AsyncStorage.getItem('token');
            if (token) {
                await api.post('/auth/logout', {}, {
                    headers: { Authorization: `Bearer ${token}` }
                });
            }
        } catch (error) {
            console.error('Logout error:', error);
        } finally {
            await AsyncStorage.multiRemove(['token', 'refreshToken']);
            setUser(null);
            setIsAuthenticated(false);
            router.replace('/(auth)/login');
        }
    };

    return (
        <AuthContext.Provider value={{
            user,
            isAuthenticated,
            isLoading,
            login,
            logout,
            checkAuthStatus
        }}>
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const context = useContext(AuthContext);
    if (context === undefined) {
        throw new Error('useAuth must be used within an AuthProvider');
    }
    return context;
} 