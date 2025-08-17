import AsyncStorage from '@react-native-async-storage/async-storage';
import { router, useRouter } from 'expo-router';
import { jwtDecode } from 'jwt-decode';
import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

import * as authService from '../services/api/authService';
import { handleAPIError } from '../services/errorService';
import { getUserSettings, getDefaultUserSettings } from '../services/api/userSettingsService';
import i18n from '../i18n';
import { useTranslation } from 'react-i18next';

// Cart cache temizleme için event listener
const CART_CLEAR_EVENT = 'logout-clear-cache';

interface User {
    id: string; // Required field
    username?: string;
    email: string;
    role: string;
    firstName?: string;
    lastName?: string;
    roles?: string[];
    token?: string; // Token field'ı eklendi
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

export { AuthContext };

export function AuthProvider({ children }: { children: React.ReactNode }) {
    const [user, setUser] = useState<User | null>(null);
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [justLoggedIn, setJustLoggedIn] = useState(false);
    const [lastActivity, setLastActivity] = useState<number>(Date.now());
    const [inactivityTimer, setInactivityTimer] = useState<ReturnType<typeof setTimeout> | null>(null);
    
    // Inactivity timeout (30 dakika)
    const INACTIVITY_TIMEOUT = 30 * 60 * 1000; // 30 dakika

    // 🧹 Cart cache temizleme fonksiyonu
    const clearCartCache = async () => {
        try {
            console.log('🧹 Cart cache temizleniyor...');
            
            // AsyncStorage'dan cart verilerini temizle
            const cartKeys = [
                'currentCartId',
                'tableCarts',
                'cartData',
                'cartItems',
                'cartState'
            ];
            
            for (const key of cartKeys) {
                await AsyncStorage.removeItem(key);
            }
            
            // Local storage'dan cart verilerini temizle (web için)
            if (typeof window !== 'undefined') {
                const localStorageKeys = Object.keys(localStorage).filter(key => 
                    key.includes('cart') || key.includes('Cart') || key.includes('table')
                );
                
                localStorageKeys.forEach(key => {
                    localStorage.removeItem(key);
                    console.log(`🗑️ LocalStorage key removed: ${key}`);
                });
            }
            
            console.log('✅ Cart cache temizlendi');
        } catch (error) {
            console.error('❌ Cart cache temizleme hatası:', error);
        }
    };

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

    // Kullanıcı aktivitesini kaydet
    const updateActivity = () => {
        setLastActivity(Date.now());
    };

    // Inactivity timer'ı başlat
    const startInactivityTimer = () => {
        if (inactivityTimer) {
            clearTimeout(inactivityTimer);
        }

        const timer = setTimeout(() => {
            console.log('User inactive for 30 minutes, logging out...');
            logout();
        }, INACTIVITY_TIMEOUT);

        setInactivityTimer(timer);
    };

    // Inactivity timer'ı durdur
    const stopInactivityTimer = () => {
        if (inactivityTimer) {
            clearTimeout(inactivityTimer);
            setInactivityTimer(null);
        }
    };

    // 🧹 Logout event listener - Cart cache temizleme için
    useEffect(() => {
        const handleLogoutEvent = () => {
            console.log('📡 Logout event received, clearing cart cache...');
            clearCartCache();
        };

        // Event listener ekle - Platform-aware
        if (typeof window !== 'undefined' && window.addEventListener) {
            try {
                window.addEventListener(CART_CLEAR_EVENT, handleLogoutEvent);
                console.log('✅ Web platform: cart clear event listener added');
                
                // Cleanup
                return () => {
                    if (typeof window !== 'undefined' && window.removeEventListener) {
                        window.removeEventListener(CART_CLEAR_EVENT, handleLogoutEvent);
                        console.log('✅ Web platform: cart clear event listener removed');
                    }
                };
            } catch (error) {
                console.warn('⚠️ Failed to add window event listener:', error);
            }
        } else {
            console.log('📱 Mobile platform: window events not available, using direct method');
            // Mobile platformda direkt çağrı kullanılabilir (gerekirse)
        }
    }, []);

    const checkAuthStatus = async () => {
        // Eğer yeni login olduysa auth check'i atla
        if (justLoggedIn) {
            console.log('Skipping auth check - just logged in'); // Debug log
            return;
        }
        
        console.log('Checking auth status...'); // Debug log
        try {
            const token = await AsyncStorage.getItem('token');
            const storedUser = await AsyncStorage.getItem('user');

            console.log('Auth check - Tokens found:', { 
                hasToken: !!token, 
                hasStoredUser: !!storedUser
            }); // Debug log

            // Token yoksa kesinlikle logout yap
            if (!token) {
                console.log('No token found, forcing logout'); // Debug log
                setUser(null);
                setIsAuthenticated(false);
                setIsLoading(false);
                return;
            }

            // Token'ı temizle (Bearer prefix olmadan)
            const cleanToken = token.startsWith('Bearer ') ? token.replace('Bearer ', '') : token;
            console.log('Token cleaned:', cleanToken.substring(0, 20) + '...');

            // Token geçerliliğini kontrol et
            if (!checkTokenExpiration(cleanToken)) {
                console.log('Token expired, checking for refresh token...'); // Debug log
                
                // RefreshToken var mı kontrol et
                const refreshToken = await AsyncStorage.getItem('refreshToken');
                if (refreshToken) {
                    try {
                        // Token süresi dolmuşsa refresh token ile yenile
                        const newToken = await authService.refreshToken();
                        console.log('Token refresh response:', newToken); // Debug log

                        if (newToken) {
                            await AsyncStorage.setItem('token', newToken);
                            // Kullanıcı bilgilerini güncelle
                            const userResponse = await authService.getCurrentUser();
                            console.log('User info after refresh:', userResponse); // Debug log
                            setUser(userResponse);
                            setIsAuthenticated(true);
                            await AsyncStorage.setItem('user', JSON.stringify(userResponse));
                        } else {
                            throw new Error('No new token received');
                        }
                    } catch (refreshError) {
                        console.error('Token refresh failed:', refreshError); // Debug log
                        // Refresh başarısız olursa oturumu sonlandır
                        await logout();
                    }
                } else {
                    console.log('No refresh token available, forcing logout'); // Debug log
                    // RefreshToken yoksa oturumu sonlandır
                    await logout();
                }
            } else {
                console.log('Token still valid, fetching user info...'); // Debug log
                try {
                    // Token geçerliyse kullanıcı bilgilerini kontrol et
                    const userResponse = await authService.getCurrentUser();
                    console.log('User info:', userResponse); // Debug log
                    
                    // userResponse'da id field'ının olduğunu kontrol et
                    if (!userResponse || !userResponse.id) {
                        throw new Error('Invalid user response - missing ID');
                    }
                    
                    // Token'ı temizle (Bearer prefix olmadan)
                    const userWithToken: User = {
                        ...userResponse,
                        token: cleanToken // cleanToken'ı ekle (Bearer prefix olmadan)
                    };
                    
                    setUser(userWithToken);
                    setIsAuthenticated(true);
                    await AsyncStorage.setItem('user', JSON.stringify(userWithToken));
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

    // Inactivity tracking
    useEffect(() => {
        if (isAuthenticated && user) {
            // Timer'ı başlat
            startInactivityTimer();

            // Global event listener'ları ekle
            const handleActivity = () => {
                updateActivity();
                startInactivityTimer(); // Timer'ı yeniden başlat
            };

            // Touch, scroll, key press event'lerini dinle
            // Platform-aware event listeners - document only exists in web
            if (typeof document !== 'undefined') {
                document?.addEventListener?.('touchstart', handleActivity);
                document?.addEventListener?.('scroll', handleActivity);
                document?.addEventListener?.('keydown', handleActivity);
                document?.addEventListener?.('mousedown', handleActivity);
            }

            return () => {
                stopInactivityTimer();
                // Platform-aware cleanup - document only exists in web
                if (typeof document !== 'undefined') {
                    document?.removeEventListener?.('touchstart', handleActivity);
                    document?.removeEventListener?.('scroll', handleActivity);
                    document?.removeEventListener?.('keydown', handleActivity);
                    document?.removeEventListener?.('mousedown', handleActivity);
                }
            };
        } else {
            stopInactivityTimer();
        }
    }, [isAuthenticated, user]);

    // Login sonrası flag'i temizle ve navigation yap
    useEffect(() => {
        if (justLoggedIn && isAuthenticated && user) {
            console.log('Login successful, attempting navigation...'); // Debug log
            
            // Navigation'ı dene
            const attemptNavigation = async () => {
                try {
                    if (router && typeof router.push === 'function') {
                        console.log('Navigating to cash-register...'); // Debug log
                        await router.push("/(tabs)/cash-register");
                        console.log('Navigation successful!'); // Debug log
                    } else {
                        console.error('Router not available for navigation'); // Debug log
                    }
                } catch (error) {
                    console.error('Navigation failed:', error); // Debug log
                }
            };
            
            // Kısa bir gecikme ile navigation'ı dene
            setTimeout(attemptNavigation, 100);
            
            // 2 saniye sonra flag'i temizle
            const timer = setTimeout(() => {
                setJustLoggedIn(false);
            }, 2000);
            
            return () => clearTimeout(timer);
        }
    }, [justLoggedIn, isAuthenticated, user]);

    // Cart reset will be handled by the component that uses AuthContext

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
            const { token, user: loggedInUser, refreshToken } = response;

            if (!token || !loggedInUser) {
                console.error('Invalid login response:', response); // Debug log
                throw new Error('Invalid login response');
            }

            console.log('Storing token and user data...'); // Debug log
            
            // Token'ı Bearer prefix olmadan kaydet
            const cleanToken = token.startsWith('Bearer ') ? token.replace('Bearer ', '') : token;
            await AsyncStorage.setItem('token', cleanToken);
            console.log('Token stored without Bearer prefix:', cleanToken);
            
            await AsyncStorage.setItem('user', JSON.stringify(loggedInUser));
            
            // Eğer refreshToken varsa onu da kaydet
            if (refreshToken) {
                await AsyncStorage.setItem('refreshToken', refreshToken);
                console.log('Refresh token stored:', !!refreshToken);
            }

            // --- CART TEMİZLİĞİ ---
            console.log('🧹 Login sonrası cart cache temizleniyor...');
            await AsyncStorage.removeItem('currentCartId');
            
            // Cart cache temizleme event'ini tetikle - Platform-aware
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    const clearCartEvent = new CustomEvent(CART_CLEAR_EVENT);
                    window.dispatchEvent(clearCartEvent);
                    console.log('✅ Web platform: Cart clear event dispatched');
                } catch (error) {
                    console.warn('⚠️ Failed to dispatch cart clear event:', error);
                }
            } else {
                console.log('📱 Mobile platform: Direct cart clear called');
                // Mobile platformda direkt clearCartCache çağır
                clearCartCache();
            }
            
            // Local storage'dan cart verilerini temizle
            const cartKeys = [
                'currentCartId',
                'tableCarts',
                'cartData',
                'cartItems',
                'cartState'
            ];
            
            for (const key of cartKeys) {
                await AsyncStorage.removeItem(key);
            }
            
            console.log('✅ Cart cache temizlendi');
            // --- CART TEMİZLİĞİ SONU ---

            console.log('Setting user state...'); // Debug log
            console.log('User data to set:', loggedInUser); // Debug log
            
            // State'leri birlikte set et - önce user, sonra authentication
            const userWithToken = {
                ...loggedInUser,
                token: cleanToken // cleanToken'ı user state'ine ekle (Bearer prefix olmadan)
            };
            setUser(userWithToken);
            console.log('User state set to:', userWithToken); // Debug log
            
            // Kısa bir gecikme ile authentication state'ini set et
            setTimeout(() => {
                setIsAuthenticated(true);
                console.log('Authentication state set to true'); // Debug log
            }, 100);
            
            // Kullanıcı ayarlarını backend'den çek
            try {
                console.log('Fetching user settings after login...');
                
                // Token'ın doğru şekilde kaydedildiğini kontrol et
                const savedToken = await AsyncStorage.getItem('token');
                console.log('Saved token before user settings request:', !!savedToken, 'length:', savedToken?.length);
                
                const userSettings = await getUserSettings();
                console.log('User settings fetched successfully:', userSettings);
                
                if (userSettings?.language) {
                    await i18n.changeLanguage(userSettings.language);
                    console.log('Language changed to:', userSettings.language);
                } else {
                    // Varsayılan dil olarak de-DE kullan (Avusturya Almancası)
                    await i18n.changeLanguage('de-DE');
                    console.log('Default language set: de-DE');
                }
            } catch (err) {
                console.warn('Kullanıcı ayarları backendden alınamadı, varsayılan dil kullanılıyor:', err);
                // Varsayılan dil olarak de-DE kullan
                await i18n.changeLanguage('de-DE');
            }

            // State güncellemesinin tamamlanmasını bekle
            await new Promise(resolve => setTimeout(resolve, 100));

            // State'lerin doğru set edildiğini kontrol et
            console.log('State set, checking...'); // Debug log
            console.log('Current state values:', { isAuthenticated, user }); // Debug log
            console.log('Login process completed, navigation will be handled by useEffect'); // Debug log
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
            // 🧹 ÖNCE CART CACHE'İ TEMİZLE
            await clearCartCache();
            
            // Backend logout API çağrısı yap
            const token = await AsyncStorage.getItem('token');
            if (token) {
                try {
                    await authService.logout();
                    console.log('Logout API request successful'); // Debug log
                    
                    // 🧹 BACKEND LOGOUT API - Kullanıcı sepetlerini temizle
                    try {
                        console.log('🧹 Backend logout API çağrılıyor...');
                        
                        // Token'ı temizle (Bearer prefix olmadan)
                        const cleanToken = token.replace('Bearer ', '');
                        
                        const logoutResponse = await fetch('http://localhost:5183/api/auth/logout', {
                            method: 'POST',
                            headers: {
                                'Authorization': `Bearer ${cleanToken}`,
                                'Content-Type': 'application/json'
                            }
                        });
                        
                        if (logoutResponse.ok) {
                            const result = await logoutResponse.json();
                            console.log('✅ Backend logout başarılı:', result.message);
                        } else {
                            // Backend'den hata mesajını al
                            let errorMessage = 'Backend logout failed';
                            try {
                                const errorData = await logoutResponse.json();
                                errorMessage = errorData.message || errorMessage;
                            } catch {
                                errorMessage = `Backend logout failed: ${logoutResponse.status} ${logoutResponse.statusText}`;
                            }
                            console.warn('⚠️ Backend logout hatası:', errorMessage);
                            
                            // Kullanıcıya hata mesajını göster (toast ile)
                            if (typeof window !== 'undefined' && window.dispatchEvent) {
                                try {
                                    // Web için toast göster
                                    const event = new CustomEvent('show-toast', {
                                        detail: { type: 'warning', message: errorMessage, duration: 5000 }
                                    });
                                    window.dispatchEvent(event);
                                    console.log('✅ Web platform: Warning toast dispatched');
                                } catch (error) {
                                    console.warn('⚠️ Failed to dispatch warning toast:', error);
                                }
                            }
                        }
                    } catch (logoutError) {
                        console.warn('Backend logout API hatası:', logoutError);
                        // Kullanıcıya hata mesajını göster
                        if (typeof window !== 'undefined' && window.dispatchEvent) {
                            try {
                                const event = new CustomEvent('show-toast', {
                                    detail: { type: 'warning', message: 'Backend logout failed, but local cleanup completed', duration: 5000 }
                                });
                                window.dispatchEvent(event);
                                console.log('✅ Web platform: Backend logout warning toast dispatched');
                            } catch (error) {
                                console.warn('⚠️ Failed to dispatch backend logout warning toast:', error);
                            }
                        }
                    }
                    
                } catch (apiError) {
                    console.error('Logout API error:', apiError); // Debug log
                    // API hatası olsa bile devam et
                }
            }
        } catch (error) {
            console.error('Logout error:', error); // Debug log
            // Kullanıcıya hata mesajını göster
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    const event = new CustomEvent('show-toast', {
                        detail: { type: 'error', message: 'Logout error occurred, but cleanup will continue', duration: 5000 }
                    });
                    window.dispatchEvent(event);
                    console.log('✅ Web platform: Logout error toast dispatched');
                } catch (error) {
                    console.warn('⚠️ Failed to dispatch logout error toast:', error);
                }
            }
        } finally {
            // AsyncStorage'ı kesinlikle temizle
            try {
                await AsyncStorage.multiRemove(['token', 'user', 'refreshToken']);
                console.log('AsyncStorage cleared successfully'); // Debug log
                
                // Temizleme işlemini doğrula
                const remainingToken = await AsyncStorage.getItem('token');
                const remainingUser = await AsyncStorage.getItem('user');
                const remainingRefreshToken = await AsyncStorage.getItem('refreshToken');
                console.log('Verification - Remaining token:', !!remainingToken, 'Remaining user:', !!remainingUser, 'Remaining refreshToken:', !!remainingRefreshToken);
                
            } catch (storageError) {
                console.error('AsyncStorage clear error:', storageError); // Debug log
                // Hata durumunda tek tek silmeyi dene
                try {
                    await AsyncStorage.removeItem('token');
                    await AsyncStorage.removeItem('user');
                    await AsyncStorage.removeItem('refreshToken');
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
            
            // LOGOUT EVENT YAYINLA - Cache temizleme için - Platform-aware
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    window.dispatchEvent(new CustomEvent(CART_CLEAR_EVENT));
                    console.log('✅ Web platform: Logout cache clear event dispatched');
                } catch (error) {
                    console.warn('⚠️ Failed to dispatch logout cache clear event:', error);
                }
            } else {
                console.log('📱 Mobile platform: Direct logout cache clear called');
                // Mobile platformda direkt clearCartCache çağır
                clearCartCache();
            }
            
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
                        // Son çare: window.location (web için) - Platform-aware
                        if (typeof window !== 'undefined' && window.location) {
                            try {
                                window.location.href = '/(auth)/login';
                                console.log('✅ Web platform: Navigation via window.location');
                            } catch (error) {
                                console.warn('⚠️ Failed to navigate via window.location:', error);
                            }
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