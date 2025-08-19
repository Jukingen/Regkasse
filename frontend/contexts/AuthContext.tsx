import AsyncStorage from '@react-native-async-storage/async-storage';
import { router, useRouter } from 'expo-router';
import { jwtDecode } from 'jwt-decode';
import React, { createContext, useContext, useState, useEffect, ReactNode, useCallback } from 'react';

import i18n from '../i18n';
import * as authService from '../services/api/authService';
import { handleAPIError } from '../services/errorService';
import { getUserSettings, getDefaultUserSettings } from '../services/api/userSettingsService';
// CRITICAL FIX: useTranslation hook'unu kaldırdık - infinite loop'a neden oluyordu

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
    const clearCartCache = useCallback(async () => {
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
    }, []);

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
    const updateActivity = useCallback(() => {
        setLastActivity(Date.now());
    }, []);

    // Inactivity timer'ı başlat
    const startInactivityTimer = useCallback(() => {
        if (inactivityTimer) {
            clearTimeout(inactivityTimer);
        }

        const timer = setTimeout(() => {
            console.log('User inactive for 30 minutes, logging out...');
            // CRITICAL FIX: Circular dependency'yi önlemek için logout'u direkt çağırmıyoruz
            // Bunun yerine state'i temizliyoruz
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
            // AsyncStorage temizliği
            AsyncStorage.multiRemove(['token', 'refreshToken', 'user', 'tokenExpiry']);
        }, INACTIVITY_TIMEOUT);

        setInactivityTimer(timer);
    }, [inactivityTimer]); // logout dependency'sini kaldırdık

    // Inactivity timer'ı durdur
    const stopInactivityTimer = useCallback(() => {
        if (inactivityTimer) {
            clearTimeout(inactivityTimer);
            setInactivityTimer(null);
        }
    }, [inactivityTimer]);

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
    }, [clearCartCache]);

    const checkAuthStatus = useCallback(async () => {
        const startTime = Date.now();
        console.log(`🔍 [${new Date().toISOString()}] Auth status check başlatıldı...`); // Debug log
        console.log(`📊 [${new Date().toISOString()}] Current state:`, { 
            justLoggedIn, 
            isAuthenticated, 
            hasUser: !!user,
            userId: user?.id 
        }); // Debug log
        
        try {
            // Eğer yeni login olduysa auth check'i atla
            if (justLoggedIn) {
                console.log(`🆕 [${new Date().toISOString()}] Yeni login, auth check atlanıyor...`); // Debug log
                return;
            }

            // Eğer zaten authenticated değilse ve user yoksa, auth check yapma
            if (!isAuthenticated && !user) {
                console.log(`🚫 [${new Date().toISOString()}] Zaten authenticated değil, auth check atlanıyor...`); // Debug log
                return;
            }

            console.log(`✅ [${new Date().toISOString()}] Auth check koşulları geçildi, devam ediliyor...`); // Debug log

            // Timeout ekle - 10 saniye sonra otomatik çıkış
            const timeoutPromise = new Promise((_, reject) => {
                setTimeout(() => {
                    reject(new Error('Auth check timeout - 10 seconds exceeded'));
                }, 10000);
            });

            // Race condition ile timeout ekle
            await Promise.race([
                (async () => {
                    console.log(`🔍 [${new Date().toISOString()}] AsyncStorage'dan token alınıyor...`); // Debug log
                    
                    // Token'ı AsyncStorage'dan al
                    const token = await AsyncStorage.getItem('token');
                    console.log(`🔑 [${new Date().toISOString()}] Token check:`, { 
                        hasToken: !!token, 
                        tokenLength: token?.length,
                        tokenPreview: token ? `${token.substring(0, 20)}...` : 'none'
                    }); // Debug log

                    if (!token) {
                        console.log(`❌ [${new Date().toISOString()}] Token bulunamadı, logout gerekli`); // Debug log
                        // Token yoksa oturumu sonlandır
                        setUser(null);
                        setIsAuthenticated(false);
                        await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
                        console.log(`✅ [${new Date().toISOString()}] Token yok, logout tamamlandı`); // Debug log
                        return;
                    }

                    console.log(`✅ [${new Date().toISOString()}] Token bulundu, temizleniyor...`); // Debug log

                    // Token'ı temizle (Bearer prefix olmadan)
                    const cleanToken = token.startsWith('Bearer ') ? token.replace('Bearer ', '') : token;

                    console.log(`🔍 [${new Date().toISOString()}] Token süresi kontrol ediliyor...`); // Debug log

                    // Token süresini kontrol et
                    const isTokenExpired = checkTokenExpiration(cleanToken);
                    console.log(`⏰ [${new Date().toISOString()}] Token expiration check:`, { 
                        isExpired: isTokenExpired,
                        cleanTokenLength: cleanToken.length 
                    }); // Debug log

                    if (isTokenExpired) {
                        console.log(`⏰ [${new Date().toISOString()}] Token süresi dolmuş, refresh deneniyor...`); // Debug log
                        
                        // Refresh token ile yenileme dene
                        const refreshToken = await AsyncStorage.getItem('refreshToken');
                        console.log(`🔄 [${new Date().toISOString()}] Refresh token check:`, { 
                            hasRefreshToken: !!refreshToken,
                            refreshTokenLength: refreshToken?.length 
                        }); // Debug log
                        
                        if (refreshToken) {
                            try {
                                console.log(`🔄 [${new Date().toISOString()}] Token refresh API çağrılıyor...`); // Debug log
                                
                                // Token süresi dolmuşsa refresh token ile yenile
                                const newToken = await authService.refreshToken();
                                console.log(`🆕 [${new Date().toISOString()}] Token refresh response:`, { 
                                    hasNewToken: !!newToken,
                                    newTokenLength: newToken?.length 
                                }); // Debug log

                                if (newToken) {
                                    console.log(`✅ [${new Date().toISOString()}] Yeni token alındı, user bilgisi güncelleniyor...`); // Debug log
                                    
                                    await AsyncStorage.setItem('token', newToken);
                                    // Kullanıcı bilgilerini güncelle
                                    const userResponse = await authService.getCurrentUser();
                                    console.log(`👤 [${new Date().toISOString()}] User info after refresh:`, { 
                                        hasUser: !!userResponse, 
                                        userId: userResponse?.id,
                                        userName: userResponse?.username 
                                    }); // Debug log
                                    
                                    if (userResponse && userResponse.id) {
                                        const userWithToken: User = {
                                            ...userResponse,
                                            token: newToken
                                        };
                                        setUser(userWithToken);
                                        setIsAuthenticated(true);
                                        await AsyncStorage.setItem('user', JSON.stringify(userWithToken));
                                        console.log(`✅ [${new Date().toISOString()}] Token refresh başarılı, user güncellendi`); // Debug log
                                        return; // Başarılı refresh sonrası çık
                                    } else {
                                        throw new Error('Invalid user response after refresh');
                                    }
                                } else {
                                    throw new Error('No new token received');
                                }
                            } catch (refreshError) {
                                console.error(`❌ [${new Date().toISOString()}] Token refresh failed:`, refreshError); // Debug log
                                // Refresh başarısız olursa oturumu sonlandır
                                console.log(`❌ [${new Date().toISOString()}] Token refresh başarısız, logout yapılıyor...`); // Debug log
                                setUser(null);
                                setIsAuthenticated(false);
                                await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
                                console.log(`✅ [${new Date().toISOString()}] Refresh başarısız, logout tamamlandı`); // Debug log
                                return;
                            }
                        } else {
                            console.log(`❌ [${new Date().toISOString()}] Refresh token bulunamadı, logout yapılıyor...`); // Debug log
                            // RefreshToken yoksa oturumu sonlandır
                            setUser(null);
                            setIsAuthenticated(false);
                            await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
                            console.log(`✅ [${new Date().toISOString()}] Refresh token yok, logout tamamlandı`); // Debug log
                            return;
                        }
                    } else {
                        console.log(`✅ [${new Date().toISOString()}] Token geçerli, user bilgisi kontrol ediliyor...`); // Debug log
                        
                        // Token geçerliyse kullanıcı bilgilerini kontrol et
                        try {
                            console.log(`🔄 [${new Date().toISOString()}] User bilgisi API'den alınıyor...`); // Debug log
                            
                            const userResponse = await authService.getCurrentUser();
                            console.log(`👤 [${new Date().toISOString()}] User info check:`, { 
                                hasUser: !!userResponse, 
                                userId: userResponse?.id,
                                userName: userResponse?.username 
                            }); // Debug log
                            
                            // userResponse'da id field'ının olduğunu kontrol et
                            if (!userResponse || !userResponse.id) {
                                throw new Error('Invalid user response - missing ID');
                            }
                            
                            // Mevcut user state ile karşılaştır
                            if (user && user.id === userResponse.id) {
                                console.log(`✅ [${new Date().toISOString()}] User bilgisi güncel, değişiklik gerekmiyor`); // Debug log
                                return; // User zaten güncel, değişiklik yapma
                            }
                            
                            console.log(`🔄 [${new Date().toISOString()}] User bilgisi güncelleniyor...`); // Debug log
                            
                            // Token'ı temizle (Bearer prefix olmadan)
                            const userWithToken: User = {
                                ...userResponse,
                                token: cleanToken
                            };
                            
                            setUser(userWithToken);
                            setIsAuthenticated(true);
                            await AsyncStorage.setItem('user', JSON.stringify(userWithToken));
                            console.log(`✅ [${new Date().toISOString()}] User bilgisi güncellendi`); // Debug log
                        } catch (userError) {
                            console.error(`❌ [${new Date().toISOString()}] User info fetch failed:`, userError); // Debug log
                            // Kullanıcı bilgisi alınamazsa oturumu sonlandır
                            console.log(`❌ [${new Date().toISOString()}] User bilgisi alınamadı, logout yapılıyor...`); // Debug log
                            setUser(null);
                            setIsAuthenticated(false);
                            await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
                            console.log(`✅ [${new Date().toISOString()}] User bilgisi alınamadı, logout tamamlandı`); // Debug log
                            return;
                        }
                    }
                })(),
                timeoutPromise
            ]);
        } catch (error) {
            console.error(`❌ [${new Date().toISOString()}] Auth status check failed:`, error); // Debug log
            // Hata durumunda oturumu sonlandır
            console.log(`❌ [${new Date().toISOString()}] Auth check hatası, logout yapılıyor...`); // Debug log
            setUser(null);
            setIsAuthenticated(false);
            await AsyncStorage.multiRemove(['token', 'refreshToken', 'user']);
            console.log(`✅ [${new Date().toISOString()}] Auth check hatası, logout tamamlandı`); // Debug log
        } finally {
            const endTime = Date.now();
            const duration = endTime - startTime;
            console.log(`🔍 [${new Date().toISOString()}] Auth status check tamamlandı (${duration}ms)`); // Debug log
            
            // CRITICAL FIX: Auth check tamamlandıktan sonra loading state'i false yap
            setIsLoading(false);
            console.log(`✅ [${new Date().toISOString()}] Loading state false yapıldı`); // Debug log
        }
    }, [justLoggedIn]); // CRITICAL FIX: user ve isAuthenticated dependency'lerini kaldırdık - infinite loop'a neden oluyordu

    // CRITICAL FIX: checkAuthStatus'u sadece mount olduğunda bir kez çağır
    useEffect(() => {
        console.log('AuthProvider mounted, checking initial auth status...'); // Debug log
        checkAuthStatus();
    }, []); // CRITICAL FIX: checkAuthStatus dependency'sini kaldırdık - sadece mount olduğunda bir kez çalışsın

    // CRITICAL FIX: Inactivity tracking'i optimize et
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
    }, [isAuthenticated, user, startInactivityTimer, stopInactivityTimer, updateActivity]);

    // CRITICAL FIX: Login sonrası navigation'ı optimize et
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
                    // CRITICAL FIX: Dil değiştirme işlemini optimize et
                    const currentLang = i18n.language;
                    if (currentLang !== userSettings.language) {
                        await i18n.changeLanguage(userSettings.language);
                        console.log('Language changed to:', userSettings.language);
                    }
                } else {
                    // Varsayılan dil olarak de-DE kullan (Avusturya Almancası)
                    const currentLang = i18n.language;
                    if (currentLang !== 'de-DE') {
                        await i18n.changeLanguage('de-DE');
                        console.log('Default language set: de-DE');
                    }
                }
            } catch (err) {
                console.warn('Kullanıcı ayarları backendden alınamadı, varsayılan dil kullanılıyor:', err);
                // Varsayılan dil olarak de-DE kullan
                const currentLang = i18n.language;
                if (currentLang !== 'de-DE') {
                    await i18n.changeLanguage('de-DE');
                }
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

    const logout = useCallback(async () => {
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
                            console.warn('⚠️ Backend logout warning:', errorMessage);
                        }
                    } catch (backendError) {
                        console.warn('⚠️ Backend logout API hatası (non-critical):', backendError);
                    }
                } catch (apiError) {
                    console.warn('⚠️ Auth service logout hatası (non-critical):', apiError);
                }
            }
            
            // 🧹 LOCAL STATE VE STORAGE TEMİZLİĞİ
            console.log('🧹 Local state ve storage temizleniyor...');
            
            // State'leri temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
            
            // AsyncStorage'dan tüm auth verilerini temizle
            await AsyncStorage.multiRemove([
                'token',
                'refreshToken',
                'user',
                'tokenExpiry'
            ]);
            
            // Local storage'dan auth verilerini temizle (web için)
            if (typeof window !== 'undefined' && window.localStorage) {
                const authKeys = Object.keys(localStorage).filter(key => 
                    key.includes('token') || key.includes('user') || key.includes('auth')
                );
                authKeys.forEach(key => {
                    localStorage.removeItem(key);
                    console.log(`🗑️ LocalStorage auth key removed: ${key}`);
                });
            }
            
            console.log('✅ Local state ve storage temizlendi');
            
            // 🧹 CART CACHE TEMİZLİĞİ - Event ile
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    const clearCartEvent = new CustomEvent(CART_CLEAR_EVENT);
                    window.dispatchEvent(clearCartEvent);
                    console.log('✅ Web platform: Cart clear event dispatched during logout');
                } catch (error) {
                    console.warn('⚠️ Failed to dispatch cart clear event during logout:', error);
                }
            }
            
            // 🧹 INACTIVITY TIMER TEMİZLİĞİ
            stopInactivityTimer();
            
            console.log('✅ Logout completed successfully');
            
            // Login sayfasına yönlendir
            if (router && typeof router.push === 'function') {
                try {
                    await router.push("/(auth)/login");
                    console.log('✅ Navigation to login page successful');
                } catch (navigationError) {
                    console.error('❌ Navigation to login page failed:', navigationError);
                    // Fallback: window.location kullan (web için)
                    if (typeof window !== 'undefined') {
                        window.location.href = '/(auth)/login';
                    }
                }
            } else {
                console.warn('⚠️ Router not available for logout navigation');
                // Fallback: window.location kullan (web için)
                if (typeof window !== 'undefined') {
                    window.location.href = '/(auth)/login';
                }
            }
        } catch (error) {
            console.error('❌ Logout error:', error);
            // Hata durumunda bile state'i temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
            
            // Login sayfasına yönlendir
            if (router && typeof router.push === 'function') {
                try {
                    await router.push("/(auth)/login");
                } catch (navigationError) {
                    console.error('Navigation failed after logout error:', navigationError);
                }
            }
        }
    }, [clearCartCache, stopInactivityTimer, router]); // CRITICAL FIX: Dependency array'i optimize ettik

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