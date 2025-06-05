import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { jwtDecode } from 'jwt-decode';
import { api } from '../services/api';
import { router } from 'expo-router';
import { handleAPIError } from '../services/errorService';

interface User {
    id: string;
    username: string;
    role: 'admin' | 'manager' | 'cashier';
    email: string;
}

interface AuthResponse {
    token: string;
    refreshToken: string;
    user: User;
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
        } catch (error) {
            console.error('Token expiration check failed:', error);
            return false;
        }
    };

    const checkAuthStatus = async () => {
        console.log('Checking auth status...'); // Debug log
        try {
            const token = await AsyncStorage.getItem('token');
            const refreshToken = await AsyncStorage.getItem('refreshToken');

            console.log('Tokens found:', { 
                hasToken: !!token, 
                hasRefreshToken: !!refreshToken 
            }); // Debug log

            if (!token || !refreshToken) {
                throw new Error('No tokens found');
            }

            // Token geçerliliğini kontrol et
            if (!checkTokenExpiration(token)) {
                console.log('Token expired, attempting refresh...'); // Debug log
                // Token süresi dolmuşsa refresh token ile yenile
                const response = await api.client.post<AuthResponse>('/auth/refresh', { refreshToken });
                console.log('Token refresh response:', response); // Debug log

                const { token: newToken, refreshToken: newRefreshToken, user: refreshedUser } = response.data;

                await AsyncStorage.setItem('token', newToken);
                await AsyncStorage.setItem('refreshToken', newRefreshToken);

                // Kullanıcı bilgilerini güncelle
                const userResponse = await api.client.get<User>('/auth/me');
                console.log('User info after refresh:', userResponse); // Debug log
                setUser(userResponse.data);
                setIsAuthenticated(true);
            } else {
                console.log('Token still valid, fetching user info...'); // Debug log
                // Token geçerliyse kullanıcı bilgilerini kontrol et
                const userResponse = await api.client.get<User>('/auth/me');
                console.log('User info:', userResponse); // Debug log
                setUser(userResponse.data);
                setIsAuthenticated(true);
            }
        } catch (error) {
            console.error('Auth status check failed:', error); // Debug log
            // Hata durumunda oturumu sonlandır
            await logout();
            router.replace('/(auth)/login');
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        console.log('AuthProvider mounted, checking initial auth status...'); // Debug log
        checkAuthStatus();
    }, []);

    const login = async (username: string, password: string) => {
        console.log('Login function called with username:', username); // Debug log
        try {
            setIsLoading(true);
            console.log('Making login API request...'); // Debug log
            
            // Backend Email ve Password bekliyor
            const response = await api.client.post<AuthResponse>('/auth/login', { 
                Email: username, 
                Password: password, 
                RememberMe: false 
            });
            console.log('Login API response:', response); // Debug log

            const { token, refreshToken, user: loggedInUser } = response.data;

            if (!token || !refreshToken || !loggedInUser) {
                console.error('Invalid login response:', response); // Debug log
                throw new Error('Invalid login response');
            }

            console.log('Storing tokens...'); // Debug log
            await AsyncStorage.setItem('token', token);
            await AsyncStorage.setItem('refreshToken', refreshToken);

            console.log('Setting user state...'); // Debug log
            setUser(loggedInUser);
            setIsAuthenticated(true);

            // Kullanıcı rolüne göre yönlendirme
            console.log('User role:', loggedInUser.role); // Debug log
            if (loggedInUser.role === 'admin' || loggedInUser.role === 'manager') {
                console.log('Redirecting to tabs...'); // Debug log
                router.replace("/(tabs)");
            } else {
                console.log('Redirecting to cash register...'); // Debug log
                router.replace("/(tabs)/cash-register");
            }
        } catch (error) {
            console.error('Login failed:', error); // Debug log
            const apiError = handleAPIError(error);
            throw new Error(apiError.message);
        } finally {
            setIsLoading(false);
        }
    };

    const logout = async () => {
        console.log('Logout function called'); // Debug log
        try {
            const token = await AsyncStorage.getItem('token');
            if (token) {
                console.log('Making logout API request...'); // Debug log
                await api.client.post('/auth/logout', {});
            }
        } catch (error) {
            console.error('Logout error:', error); // Debug log
        } finally {
            console.log('Clearing auth data...'); // Debug log
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