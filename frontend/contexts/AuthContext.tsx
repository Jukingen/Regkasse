import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { jwtDecode } from 'jwt-decode';
import { authService } from '../services/api/authService';
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
            const storedUser = await AsyncStorage.getItem('user');

            console.log('Auth check - Tokens found:', { 
                hasToken: !!token, 
                hasRefreshToken: !!refreshToken,
                hasStoredUser: !!storedUser
            }); // Debug log

            // Token yoksa kesinlikle logout yap
            if (!token || !refreshToken) {
                console.log('No tokens found, forcing logout'); // Debug log
                setUser(null);
                setIsAuthenticated(false);
                setIsLoading(false);
                return;
            }

            // Token geçerliliğini kontrol et
            if (!checkTokenExpiration(token)) {
                console.log('Token expired, attempting refresh...'); // Debug log
                try {
                    // Token süresi dolmuşsa refresh token ile yenile
                    const response = await authService.refreshToken(refreshToken);
                    console.log('Token refresh response:', response); // Debug log

                    const { token: newToken, refreshToken: newRefreshToken } = response;

                    await AsyncStorage.setItem('token', newToken);
                    await AsyncStorage.setItem('refreshToken', newRefreshToken);

                    // Kullanıcı bilgilerini güncelle
                    const userResponse = await authService.getCurrentUser();
                    console.log('User info after refresh:', userResponse); // Debug log
                    setUser(userResponse);
                    setIsAuthenticated(true);
                    await AsyncStorage.setItem('user', JSON.stringify(userResponse));
                } catch (refreshError) {
                    console.error('Token refresh failed:', refreshError); // Debug log
                    // Refresh başarısız olursa oturumu sonlandır
                    await logout();
                }
            } else {
                console.log('Token still valid, fetching user info...'); // Debug log
                try {
                    // Token geçerliyse kullanıcı bilgilerini kontrol et
                    const userResponse = await authService.getCurrentUser();
                    console.log('User info:', userResponse); // Debug log
                    setUser(userResponse);
                    setIsAuthenticated(true);
                    await AsyncStorage.setItem('user', JSON.stringify(userResponse));
                } catch (userError) {
                    console.error('User info fetch failed:', userError); // Debug log
                    // Kullanıcı bilgisi alınamazsa oturumu sonlandır
                    await logout();
                }
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
            setJustLoggedIn(true); // Login başladığında flag'i set et
            console.log('Making login API request...'); // Debug log
            
            // Backend Email ve Password bekliyor
            const response = await authService.login({ email: username, password });
            console.log('Login API response:', response); // Debug log

            // API client response interceptor'ı response.data döndürüyor
            const { token, refreshToken, user: loggedInUser } = response;

            if (!token || !refreshToken || !loggedInUser) {
                console.error('Invalid login response:', response); // Debug log
                throw new Error('Invalid login response');
            }

            console.log('Storing tokens and user data...'); // Debug log
            await AsyncStorage.setItem('token', token);
            await AsyncStorage.setItem('refreshToken', refreshToken);
            await AsyncStorage.setItem('user', JSON.stringify(loggedInUser));

            console.log('Setting user state...'); // Debug log
            console.log('User data to set:', loggedInUser); // Debug log
            
            // State'leri birlikte set et
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
                    router.push("/(tabs)/cash-register");
                } else {
                    console.log('Redirecting to cash register...'); // Debug log
                    router.push("/(tabs)/cash-register");
                }
            } catch (navigationError) {
                console.error('Navigation error:', navigationError); // Debug log
                // Navigation başarısız olursa manuel olarak yönlendir
                setTimeout(() => {
                    if (loggedInUser.role === 'admin' || loggedInUser.role === 'manager') {
                        router.push("/(tabs)/cash-register");
                    } else {
                        router.push("/(tabs)/cash-register");
                    }
                }, 500);
            }
        } catch (error) {
            console.error('Login failed:', error); // Debug log
            setJustLoggedIn(false); // Hata durumunda flag'i temizle
            const apiError = handleAPIError(error);
            throw new Error(apiError.message);
        } finally {
            setIsLoading(false);
        }
    };

    const logout = async () => {
        console.log('Logout function called'); // Debug log
        
        try {
            // Önce API logout çağrısı yap
            const token = await AsyncStorage.getItem('token');
            if (token) {
                try {
                    await authService.logout();
                    console.log('Logout API request successful'); // Debug log
                } catch (apiError) {
                    console.error('Logout API error:', apiError); // Debug log
                    // API hatası olsa bile devam et
                }
            }
        } catch (error) {
            console.error('Logout error:', error); // Debug log
        } finally {
            // AsyncStorage'ı kesinlikle temizle
            try {
                await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
                console.log('AsyncStorage cleared successfully'); // Debug log
                
                // Temizleme işlemini doğrula
                const remainingToken = await AsyncStorage.getItem('token');
                const remainingUser = await AsyncStorage.getItem('user');
                console.log('Verification - Remaining token:', !!remainingToken, 'Remaining user:', !!remainingUser);
                
            } catch (storageError) {
                console.error('AsyncStorage clear error:', storageError); // Debug log
                // Hata durumunda tek tek silmeyi dene
                try {
                    await AsyncStorage.removeItem('token');
                    await AsyncStorage.removeItem('refreshToken');
                    await AsyncStorage.removeItem('user');
                    console.log('Individual AsyncStorage clear successful'); // Debug log
                } catch (individualError) {
                    console.error('Individual AsyncStorage clear failed:', individualError);
                }
            }
            
            // State'leri kesinlikle temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
            setIsLoading(false);
            
            console.log('Auth state cleared successfully'); // Debug log
            
            // Navigation'ı yap
            try {
                console.log('Attempting navigation to login...'); // Debug log
                router.replace('/(auth)/login');
                console.log('Navigation to login successful'); // Debug log
            } catch (navigationError) {
                console.error('Navigation error during logout:', navigationError);
                // Alternatif navigation yöntemi
                setTimeout(() => {
                    try {
                        console.log('Retrying navigation with push...'); // Debug log
                        router.push('/(auth)/login');
                    } catch (retryError) {
                        console.error('Retry navigation failed:', retryError);
                        // Son çare: window.location (web için)
                        if (typeof window !== 'undefined') {
                            window.location.href = '/(auth)/login';
                        }
                    }
                }, 100);
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