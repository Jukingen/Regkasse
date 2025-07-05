import React, { createContext, useContext, useState, useEffect } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { jwtDecode } from 'jwt-decode';
import { api } from '../services/api';
import { router } from 'expo-router';
import { handleAPIError } from '../services/errorService';

interface User {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    role: string;
    employeeNumber: string;
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
    const [justLoggedIn, setJustLoggedIn] = useState(false);

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
        // Eğer yeni login olduysa auth check'i atla
        if (justLoggedIn) {
            console.log('Skipping auth check - just logged in'); // Debug log
            return;
        }
        
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
                const response = await api.client.post<AuthResponse>('/auth/refresh', { token: refreshToken });
                console.log('Token refresh response:', response); // Debug log

                const { token: newToken, refreshToken: newRefreshToken } = response;

                await AsyncStorage.setItem('token', newToken);
                await AsyncStorage.setItem('refreshToken', newRefreshToken);

                // Kullanıcı bilgilerini güncelle
                const userResponse = await api.client.get<User>('/auth/me');
                console.log('User info after refresh:', userResponse); // Debug log
                setUser(userResponse);
                setIsAuthenticated(true);
            } else {
                console.log('Token still valid, fetching user info...'); // Debug log
                // Token geçerliyse kullanıcı bilgilerini kontrol et
                const userResponse = await api.client.get<User>('/auth/me');
                console.log('User info:', userResponse); // Debug log
                setUser(userResponse);
                setIsAuthenticated(true);
            }
        } catch (error) {
            console.error('Auth status check failed:', error); // Debug log
            // Hata durumunda oturumu sonlandır
            await logout();
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        console.log('AuthProvider mounted, checking initial auth status...'); // Debug log
        checkAuthStatus();
    }, []);

    // Login sonrası flag'i temizle
    useEffect(() => {
        if (justLoggedIn) {
            const timer = setTimeout(() => {
                setJustLoggedIn(false);
            }, 2000); // 2 saniye sonra flag'i temizle
            
            return () => clearTimeout(timer);
        }
    }, [justLoggedIn]);

    const login = async (username: string, password: string) => {
        console.log('Login function called with username:', username); // Debug log
        try {
            setIsLoading(true);
            console.log('Making login API request...'); // Debug log
            
            // Backend Email ve Password bekliyor
            const response = await api.client.post<AuthResponse>('/auth/login', { 
                email: username, 
                password: password, 
                rememberMe: false 
            });
            console.log('Login API response:', response); // Debug log

            // API client response interceptor'ı response.data döndürüyor
            const { token, refreshToken, user: loggedInUser } = response;

            if (!token || !refreshToken || !loggedInUser) {
                console.error('Invalid login response:', response); // Debug log
                throw new Error('Invalid login response');
            }

            console.log('Storing tokens...'); // Debug log
            await AsyncStorage.setItem('token', token);
            await AsyncStorage.setItem('refreshToken', refreshToken);

            console.log('Setting user state...'); // Debug log
            console.log('User data to set:', loggedInUser); // Debug log
            
            // State'leri birlikte set et - callback kullanarak
            setUser(loggedInUser);
            setIsAuthenticated(true);
            
            // State güncellemesinin tamamlanmasını bekle
            await new Promise(resolve => setTimeout(resolve, 200));

            // State'lerin doğru set edildiğini kontrol et
            console.log('State set, checking...'); // Debug log

            // Kullanıcı rolüne göre yönlendirme
            console.log('User role:', loggedInUser.role); // Debug log
            console.log('Current authentication state:', { isAuthenticated: true, user: loggedInUser }); // Debug log
            
            try {
                if (loggedInUser.role === 'admin' || loggedInUser.role === 'manager') {
                    console.log('Redirecting to tabs...'); // Debug log
                    router.push("/(tabs)");
                } else {
                    console.log('Redirecting to cash register...'); // Debug log
                    router.push("/(tabs)/cash-register");
                }
            } catch (navigationError) {
                console.error('Navigation error:', navigationError); // Debug log
                // Navigation başarısız olursa manuel olarak yönlendir
                setTimeout(() => {
                    if (loggedInUser.role === 'admin' || loggedInUser.role === 'manager') {
                        router.push("/(tabs)");
                    } else {
                        router.push("/(tabs)/cash-register");
                    }
                }, 500);
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
            
            // State güncellemesinin tamamlanmasını bekle
            await new Promise(resolve => setTimeout(resolve, 100));
            
            try {
                router.push('/(auth)/login');
            } catch (navigationError) {
                console.error('Navigation error during logout:', navigationError);
                setTimeout(() => {
                    router.push('/(auth)/login');
                }, 500);
            }
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