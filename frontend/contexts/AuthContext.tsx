import { storage } from '../utils/storage';
import { isPosAllowedRole } from '../utils/posRoleGuard';
import { router } from 'expo-router';
import { jwtDecode } from 'jwt-decode';
import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';

import i18n from '../i18n';
import { changeLanguage as persistAndChangeLanguage } from '../i18n';
import { DEFAULT_TEXT_LOCALE, normalizeTextLocale } from '../i18n/localeUtils';
import * as authService from '../services/api/authService';
import { sessionManager } from '../services/session/sessionManager';
import { handleAPIError } from '../services/errorService';
import { isAuthError, AuthAppError } from '../features/auth/authErrors';
import { getUserSettingsAfterLogin } from '../services/api/userSettingsService';
import { authTrace } from '../utils/authTrace';
// CRITICAL FIX: useTranslation hook'unu kaldırdık - infinite loop'a neden oluyordu
const isDev = __DEV__;

function authDevLog(...args: unknown[]) {
    if (isDev) {
        // eslint-disable-next-line no-console
        console.log(...args);
    }
}
function authDevWarn(...args: unknown[]) {
    if (isDev) {
        // eslint-disable-next-line no-console
        console.warn(...args);
    }
}
function authDevError(...args: unknown[]) {
    if (isDev) {
        // eslint-disable-next-line no-console
        console.error(...args);
    }
}

// Cart cache temizleme için event listener
const CART_CLEAR_EVENT = 'logout-clear-cache';

// 🔐 BACKEND AUTH CHECK - F5 refresh'te backend'den kullanıcı durumunu kontrol eder
const checkBackendAuth = async (): Promise<{ isAuthenticated: boolean; user: any }> => {
    // 🚀 F5 REFRESH FIX: Debouncing için static flag
    if ((checkBackendAuth as any).isChecking) {
        return { isAuthenticated: false, user: null };
    }

    // 🚀 F5 REFRESH FIX: Debouncing için timestamp kontrol
    const currentTime = Date.now();
    const lastCallTime = (checkBackendAuth as any).lastCallTime || 0;
    const DEBOUNCE_MS = 1500; // 1.5 saniye debounce

    if (currentTime - lastCallTime < DEBOUNCE_MS) {
        return { isAuthenticated: false, user: null };
    }

    // Flag'leri set et
    (checkBackendAuth as any).isChecking = true;
    (checkBackendAuth as any).lastCallTime = currentTime;

    try {
        // Token'ı AsyncStorage'dan al
        const token = await sessionManager.getAccessToken();
        if (!token) {
            return { isAuthenticated: false, user: null };
        }

        // Backend'den token validation yap
        const user = await authService.validateToken();
        if (user?.id) {
            return { isAuthenticated: true, user };
        } else {
            return { isAuthenticated: false, user: null };
        }
    } catch (error) {
        authDevError('❌ Backend auth check hatası:', error);
        return { isAuthenticated: false, user: null };
    } finally {
        // Flag'i temizle
        (checkBackendAuth as any).isChecking = false;
    }
};

interface User {
    id: string;
    username?: string;
    email: string;
    role: string;
    firstName?: string;
    lastName?: string;
    roles?: string[];
    /** Backend permission claims (resource.action). Used for permission-first UI. */
    permissions?: string[];
    /** Demo user flag from backend ApplicationUser.IsDemo. Use this for restrictions; do not infer from role. */
    isDemo?: boolean;
    token?: string;
}



interface AuthContextType {
    user: User | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    isAuthReady: boolean; // ✅ Added
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
    const [isAuthReady, setIsAuthReady] = useState(false); // ✅ Added
    const [justLoggedIn, setJustLoggedIn] = useState(false);
    const [, setLastActivity] = useState<number>(Date.now());

    const inactivityTimerRef = React.useRef<NodeJS.Timeout | null>(null); // ✅ FIX: State yerine Ref kullan

    // 🚀 F5 REFRESH FIX: Use refs for auth check status to prevent re-renders
    const isAuthCheckInProgressRef = React.useRef(false);
    const lastAuthCheckTimeRef = React.useRef(0);
    const hasInitialAuthCheckRef = React.useRef(false);
    const AUTH_CHECK_DEBOUNCE_MS = 2000;

    // Inactivity timeout (30 dakika)
    const INACTIVITY_TIMEOUT = 30 * 60 * 1000; // 30 dakika

    // 🧹 Cart cache temizleme fonksiyonu
    const clearCartCache = useCallback(async () => {
        try {
            authDevLog('🧹 Clearing cart cache...');
            // Remove exact keys (Universal)
            const cartKeys = [
                'currentCartId',
                'tableCarts',
                'cartData',
                'cartItems',
                'cartState'
            ];
            await storage.multiRemove(cartKeys);

            // Clear any partial matches for web cleanup or persistent fragments
            await storage.clearByPartialKey(['cart', 'Cart', 'table']);

            authDevLog('✅ Cart cache cleared successfully');

        } catch (error) {
            authDevError('❌ Cart cache temizleme hatası:', error);
        }
    }, []);

    // Inactivity timer'ı durdur
    const stopInactivityTimer = useCallback(() => {
        if (inactivityTimerRef.current) {
            clearTimeout(inactivityTimerRef.current);
            inactivityTimerRef.current = null;
            authDevLog('[AUTH] inactivity timer cleared');
        }
    }, []);

    const checkTokenExpiration = (token: string): boolean => {
        try {
            const decoded = jwtDecode(token);
            const currentTime = Date.now() / 1000;

            // 🔧 CRITICAL FIX: Token expiration logic düzeltildi
            // decoded.exp > currentTime = token henüz geçerli
            // decoded.exp <= currentTime = token expired
            if (decoded.exp) {
                // 🔧 5 dakika buffer ekle - token henüz expired değilse kullan
                const BUFFER_MINUTES = 5;
                const bufferTime = BUFFER_MINUTES * 60; // 5 dakika = 300 saniye

                const isExpired = decoded.exp <= (currentTime + bufferTime);
                const timeLeftMinutes = Math.round((decoded.exp - currentTime) / 60);

                // Reduce log noise
                if (isExpired || timeLeftMinutes < 60) {
                    authDevLog('🔍 TOKEN CHECK:', {
                        timeLeft: timeLeftMinutes + ' minutes',
                        isExpired
                    });
                }

                return isExpired;
            }

            authDevWarn('⚠️ TOKEN CHECK: No expiration time found in token');
            return false; // Expiration yoksa güvenlik için false döndür
        } catch (error) {
            authDevError('❌ TOKEN CHECK: Token expiration check failed:', error);
            return false;
        }
    };

    // Kullanıcı aktivitesini kaydet
    const updateActivity = useCallback(() => {
        setLastActivity(Date.now());
    }, []);

    // Inactivity timer'ı başlat
    const startInactivityTimer = useCallback(() => {
        if (inactivityTimerRef.current) {
            clearTimeout(inactivityTimerRef.current);
        }

        inactivityTimerRef.current = setTimeout(() => {
            authDevLog('User inactive for 30 minutes, logging out...');
            // CRITICAL FIX: Circular dependency'yi önlemek için logout'u direkt çağırmıyoruz
            // Bunun yerine state'i temizliyoruz
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
            // AsyncStorage temizliği
            sessionManager.clearSession();
        }, INACTIVITY_TIMEOUT);

        // console.log('[AUTH] inactivity timer started'); // Reduced log noise
    }, [INACTIVITY_TIMEOUT]);

    // 🚀 F5 REFRESH FIX: Logout ve login sayfasına yönlendirme
    const handleLogoutAndRedirect = useCallback(async () => {
        try {
            // State'leri temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);

            await sessionManager.clearSession();

            // Cart cache temizle
            await clearCartCache();

            // Inactivity timer'ı durdur
            stopInactivityTimer();

            authDevLog('✅ Logout tamamlandı, login sayfasına yönlendiriliyor...');

            // Login sayfasına yönlendir
            if (router && typeof router.push === 'function') {
                try {
                    await router.push("/(auth)/login");
                } catch (navigationError) {
                    authDevError('❌ Navigation to login page failed:', navigationError);
                    // Fallback: window.location kullan (web için)
                    if (typeof window !== 'undefined') {
                        window.location.href = '/(auth)/login';
                    }
                }
            } else {
                authDevWarn('⚠️ Router not available for logout navigation');
                // Fallback: window.location kullan (web için)
                if (typeof window !== 'undefined') {
                    window.location.href = '/(auth)/login';
                }
            }
        } catch (error) {
            authDevError('❌ handleLogoutAndRedirect error:', error);
            // Hata durumunda bile state'i temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);
        }
    }, [clearCartCache, stopInactivityTimer]);

    // 🧹 Logout event listener - Cart cache temizleme ve AUTH_SESSION_EXPIRED için
    useEffect(() => {
        const handleLogoutEvent = () => {
            authDevLog('📡 Logout event received, clearing cart cache...');
            clearCartCache();
        };

        const handleAuthExpiredEvent = () => {
            authDevLog('📡 AUTH_SESSION_EXPIRED received. Logging out...');
            handleLogoutAndRedirect();
        };

        // Event listener ekle - Platform-aware
        if (typeof window !== 'undefined' && window.addEventListener) {
            try {
                window.addEventListener(CART_CLEAR_EVENT, handleLogoutEvent);
                window.addEventListener('AUTH_SESSION_EXPIRED', handleAuthExpiredEvent);
                // console.log('✅ [AUTH] Web platform: auth event listeners added');

                // Cleanup
                return () => {
                    if (typeof window !== 'undefined' && window.removeEventListener) {
                        window.removeEventListener(CART_CLEAR_EVENT, handleLogoutEvent);
                        window.removeEventListener('AUTH_SESSION_EXPIRED', handleAuthExpiredEvent);
                    }
                };
            } catch (error) {
                authDevWarn('⚠️ Failed to add window event listener:', error);
            }
        }
    }, [clearCartCache, handleLogoutAndRedirect]);


    // 🚀 F5 REFRESH FIX: Sadeleştirilmiş auth check fonksiyonu
    const stableCheckAuthStatus = useCallback(async () => {
        // 🚫 Temel kontroller - öncelikle bunları geç
        if (justLoggedIn) {
            return;
        }

        if (isAuthCheckInProgressRef.current) {
            return;
        }

        // 🚫 Debouncing kontrolü
        const currentTime = Date.now();
        if (currentTime - lastAuthCheckTimeRef.current < AUTH_CHECK_DEBOUNCE_MS) {
            return;
        }

        // 🚫 Eğer user zaten varsa auth check'i atla
        if (hasInitialAuthCheckRef.current && user?.id && isAuthenticated) {
            setIsLoading(false); // Loading'i false yap
            return;
        }

        // Auth check flag'lerini set et
        isAuthCheckInProgressRef.current = true;
        lastAuthCheckTimeRef.current = currentTime;

        try {
            // 🔑 Token kontrolü yap
            const token = await sessionManager.getAccessToken();
            if (!token) {
                await handleLogoutAndRedirect();
                return;
            }

            // 🔑 Token süresini kontrol et
            const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
            const isTokenExpired = checkTokenExpiration(cleanToken);

            if (isTokenExpired) {
                await handleLogoutAndRedirect();
                return;
            }

            // 👤 User state kontrolü
            if (user?.id) {
                hasInitialAuthCheckRef.current = true;
                if (typeof sessionStorage !== 'undefined') {
                    sessionStorage.setItem('hasInitialAuthCheck', 'true');
                }
                authDevLog('✅ AUTH CHECK: User state already exists, skipping further checks');
                return;
            }

            // 💾 Storage'dan user bilgisini al
            const userStr = await storage.getItem('user');
            if (userStr) {
                try {
                    const storedUser = JSON.parse(userStr);
                    if (storedUser?.id) {
                        const userWithToken: User = {
                            ...storedUser,
                            token: cleanToken
                        };

                        // FIX: Only update if user actually changed to reference loop
                        // Deep compare basic properties to avoid loop
                        const shouldUpdate = !user || user.id !== userWithToken.id || user.email !== userWithToken.email;

                        if (shouldUpdate) {
                            setUser(userWithToken);
                            authDevLog('✅ [AUTH CHECK] User state updated from storage');
                        }

                        setIsAuthenticated(true);
                        hasInitialAuthCheckRef.current = true;

                        if (typeof sessionStorage !== 'undefined') {
                            sessionStorage.setItem('hasInitialAuthCheck', 'true');
                        }

                        return;
                    }
                } catch (parseError) {
                    authDevError('❌ [F5 FIX] User parse hatası:', parseError);
                }
            }

            // 🔄 Son çare: Backend auth check
            try {
                const result = await checkBackendAuth();
                if (result.isAuthenticated && result.user?.id) {
                    // FIX: Only update if user actually changed
                    const shouldUpdate = !user || user.id !== result.user.id;

                    if (shouldUpdate) {
                        setUser(result.user);
                        authDevLog('✅ [AUTH CHECK] User state updated from backend');
                    }
                    setIsAuthenticated(true);
                    hasInitialAuthCheckRef.current = true;

                    if (typeof sessionStorage !== 'undefined') {
                        sessionStorage.setItem('hasInitialAuthCheck', 'true');
                    }

                    return;
                }
            } catch (backendError) {
                authDevWarn('⚠️ [F5 FIX] Backend auth check hatası:', backendError);
            }

            // Hiçbir user bilgisi bulunamazsa logout yap
            await handleLogoutAndRedirect();

        } catch (error) {
            authDevError('❌ [F5 FIX] Auth check hatası:', error);
            await handleLogoutAndRedirect();
        } finally {
            setIsLoading(false);
            setIsAuthReady(true); // ✅ Ready
            isAuthCheckInProgressRef.current = false;
        }
    }, [justLoggedIn, user, isAuthenticated, handleLogoutAndRedirect]); // Dependencies are cleaner now

    // Single auth bootstrap on mount (storage restore first, then stableCheckAuthStatus fallback).
    useEffect(() => {
        authTrace('AuthProvider mount — single bootstrap starting');

        // 🚀 F5 REFRESH FIX: Her zaman önce storage'dan restore etmeye çalış
        const initializeAuth = async () => {
            try {
                authDevLog('🔍 AUTH INIT: Checking storage for existing auth...');

                const snapshot = await sessionManager.getSnapshot();
                const token = snapshot.accessToken;
                const userStr = snapshot.user ? JSON.stringify(snapshot.user) : null;

                if (token && userStr) {
                    const storedUser = JSON.parse(userStr);
                    if (storedUser?.id) {
                        const cleanToken = token.startsWith('Bearer ') ? token.replace('Bearer ', '') : token;

                        // Token süresini kontrol et
                        const isTokenExpired = checkTokenExpiration(cleanToken);

                        if (isTokenExpired) {
                            authDevLog('⏰ AUTH INIT: Token expired, clearing storage and redirecting to login');
                            await sessionManager.clearSession();
                            if (typeof sessionStorage !== 'undefined') {
                                sessionStorage.removeItem('hasInitialAuthCheck');
                            }
                            setIsLoading(false);
                            setIsAuthReady(true);
                            return;
                        }

                        // Token geçerliyse user state'i restore et
                        const userWithToken = {
                            ...storedUser,
                            token: cleanToken
                        };

                        authDevLog('✅ AUTH INIT: Restoring user state from storage');
                        setUser(userWithToken);
                        setIsAuthenticated(true);
                        hasInitialAuthCheckRef.current = true;

                        if (typeof sessionStorage !== 'undefined') {
                            sessionStorage.setItem('hasInitialAuthCheck', 'true');
                        }

                        setIsLoading(false);
                        setIsAuthReady(true); // ✅ Ready
                        return;
                    }
                }

                // Storage'da geçerli auth bulunamazsa normal auth check yap
                authDevLog('❌ AUTH INIT: No valid auth in storage, performing full auth check');
                await stableCheckAuthStatus(); // Await added
                setIsAuthReady(true); // ✅ Ready even if failed

            } catch (error) {
                authDevError('❌ AUTH INIT: Error during initialization:', error);
                await stableCheckAuthStatus(); // Await added
                setIsAuthReady(true); // ✅ Ready even if failed
            }
        };

        initializeAuth();

        return () => {
            authTrace('AuthProvider unmount');
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []); // Only run once on mount

    // CRITICAL FIX: Inactivity tracking'i optimize et
    useEffect(() => {
        if (isAuthenticated && user) {
            // Timer'ı başlat
            startInactivityTimer();

            // Global event listener'ları ekle
            const handleActivity = () => {
                // Optimization: debounce activity updates in logic or just trust simple restart
                updateActivity();
                startInactivityTimer(); // Timer'ı yeniden başlat
            };

            // Touch, scroll, key press event'lerini dinle
            // Platform-aware event listeners - document only exists in web
            if (typeof document !== 'undefined') {
                document?.addEventListener?.('touchstart', handleActivity, { passive: true });
                document?.addEventListener?.('scroll', handleActivity, { passive: true });
                document?.addEventListener?.('keydown', handleActivity, { passive: true });
                document?.addEventListener?.('mousedown', handleActivity, { passive: true });
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
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthenticated]); // Removed user from dependency to avoid loop if user object ref changes but id is same

    // CRITICAL FIX: Login sonrası navigation'ı optimize et
    useEffect(() => {
        if (justLoggedIn && isAuthenticated && user) {
            authDevLog('🚀 Login successful, attempting navigation...'); // Debug log
            authDevLog('🚀 Navigation state:', { justLoggedIn, isAuthenticated, hasUser: !!user, userEmail: user?.email }); // Debug log

            // Navigation'ı dene
            const attemptNavigation = async () => {
                try {
                    if (router && typeof router.push === 'function') {
                        authDevLog('🧭 Navigating to cash-register...'); // Debug log
                        await router.push("/(tabs)/cash-register");
                        authDevLog('✅ Navigation successful!'); // Debug log
                    } else {
                        authDevError('❌ Router not available for navigation'); // Debug log
                    }
                } catch (error) {
                    authDevError('❌ Navigation failed:', error); // Debug log
                }
            };

            // Daha uzun bir gecikme ile navigation'ı dene (state'lerin set olması için)
            setTimeout(attemptNavigation, 500);

            // 3 saniye sonra flag'i temizle
            const timer = setTimeout(() => {
                setJustLoggedIn(false);
                authDevLog('🔄 justLoggedIn flag cleared'); // Debug log
            }, 3000);

            return () => clearTimeout(timer);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [justLoggedIn]); // ✅ YENİ: Minimal dependency - sadece justLoggedIn (isAuthenticated ve user infinite loop'a neden oluyor)

    // Cart reset will be handled by the component that uses AuthContext

    const login = useCallback(async (username: string, password: string) => {
        authDevLog('Login function called with username:', username); // Debug log
        try {
            setIsLoading(true);
            setJustLoggedIn(true); // Login başladığında flag'i set et
            authDevLog('Making login API request...'); // Debug log

            const response = await authService.login({ email: username, password, clientApp: 'pos' });
            if (isDev) {
                authDevLog('Login API response received'); // Debug log
            }

            // API client response interceptor'ı response.data döndürüyor
            const { token, user: loggedInUser, refreshToken } = response;

            if (!token || !loggedInUser) {
                authDevError('Invalid login response:', response); // Debug log
                throw new Error('Invalid login response');
            }

            // POS rol kontrolü: sadece Cashier ve SuperAdmin girebilir
            if (!isPosAllowedRole(loggedInUser.role, loggedInUser.roles)) {
                authDevWarn('POS role denied for user:', loggedInUser.email, 'role:', loggedInUser.role);
                setJustLoggedIn(false);
                throw new AuthAppError('POS_UNAUTHORIZED_USER');
            }

            authDevLog('Storing token and user data...'); // Debug log

            // Token'ı JWT olarak kaydet (Bearer prefix olmadan)
            const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;

            // 🚀 F5 REFRESH FIX: Platform-aware storage kullan
            await sessionManager.persistSession({
                token: cleanToken,
                refreshToken: refreshToken ?? null,
                user: loggedInUser,
            });
            if (isDev) {
                authDevLog('Session tokens stored');
            }

            // 🔐 AUTH STATE PERSISTENCE - F5 refresh'te korunması için
            // await persistAuthState(loggedInUser, cleanToken); // Removed as per new_code

            // --- CART TEMİZLİĞİ ---
            authDevLog('🧹 Login sonrası cart cache temizleniyor...');
            await storage.removeItem('currentCartId');

            // Cart cache temizleme event'ini tetikle - Platform-aware
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    const clearCartEvent = new CustomEvent(CART_CLEAR_EVENT);
                    window.dispatchEvent(clearCartEvent);
                    authDevLog('✅ Web platform: Cart clear event dispatched');
                } catch (error) {
                    authDevWarn('⚠️ Failed to dispatch cart clear event:', error);
                }
            } else {
                authDevLog('📱 Mobile platform: Direct cart clear called');
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
                await storage.removeItem(key);
            }

            authDevLog('✅ Cart cache temizlendi');
            // --- CART TEMİZLİĞİ SONU ---

            authDevLog('Setting user state...'); // Debug log
            if (isDev) {
                authDevLog('User data prepared for state update'); // Debug log
            }

            // State'leri birlikte set et - önce user, sonra authentication
            const userWithToken = {
                ...loggedInUser,
                token: cleanToken // cleanToken'ı user state'ine ekle (JWT only)
            };
            setUser(userWithToken);
            authDevLog('User state set to:', userWithToken); // Debug log

            // Kısa bir gecikme ile authentication state'ini set et
            setTimeout(() => {
                setIsAuthenticated(true);
                authDevLog('Authentication state set to true'); // Debug log
                if (isDev) {
                    authDevLog('Auth state updated after login'); // Debug log
                }
            }, 100);

            // Kullanıcı ayarlarını backend'den çek
            try {
                authDevLog('Fetching user settings after login...');

                // Token'ın doğru şekilde kaydedildiğini kontrol et
                const savedToken = await sessionManager.getAccessToken();
                authDevLog('Saved token before user settings request:', !!savedToken, 'length:', savedToken?.length);

                const userSettings = await getUserSettingsAfterLogin();
                authDevLog('User settings loaded after login (bootstrap or GET fallback):', userSettings);

                if (userSettings?.language) {
                    // Map API language (e.g. de-DE) to i18n text locale (de | en | tr)
                    const next = normalizeTextLocale(userSettings.language);
                    const currentLang = i18n.language;
                    if (normalizeTextLocale(currentLang) !== next) {
                        await persistAndChangeLanguage(next);
                        authDevLog('Language changed to:', next);
                    }
                } else {
                    const currentLang = i18n.language;
                    if (currentLang !== DEFAULT_TEXT_LOCALE) {
                        await persistAndChangeLanguage(DEFAULT_TEXT_LOCALE);
                        authDevLog('Default language set:', DEFAULT_TEXT_LOCALE);
                    }
                }
            } catch (err) {
                authDevWarn('Kullanıcı ayarları backendden alınamadı, varsayılan dil kullanılıyor:', err);
                const currentLang = i18n.language;
                if (currentLang !== DEFAULT_TEXT_LOCALE) {
                    await persistAndChangeLanguage(DEFAULT_TEXT_LOCALE);
                }
            }

            // State güncellemesinin tamamlanmasını bekle
            await new Promise(resolve => setTimeout(resolve, 100));

            // State'lerin doğru set edildiğini kontrol et
            authDevLog('State set, checking...'); // Debug log
            authDevLog('Current state values:', { isAuthenticated, user }); // Debug log
            authDevLog('Login process completed, navigation will be handled by useEffect'); // Debug log
        } catch (error: unknown) {
            authDevError('Login failed:', error);
            setJustLoggedIn(false);

            if (isAuthError(error)) {
                throw error;
            }

            const apiError = handleAPIError(error);
            throw new Error(apiError.message);
        } finally {
            setIsLoading(false);
        }
    }, [clearCartCache]);

    const logout = useCallback(async () => {
        authDevLog('Logout function called'); // Debug log

        try {
            // 🧹 ÖNCE CART CACHE'İ TEMİZLE
            await clearCartCache();

            // Backend logout + storage cleanup via authService
            await authService.logout();

            // 🧹 LOCAL STATE VE STORAGE TEMİZLİĞİ
            authDevLog('🧹 Local state ve storage temizleniyor...');

            // State'leri temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);

            await sessionManager.clearSession();

            // 🧹 CART CACHE TEMİZLİĞİ - Event ile
            if (typeof window !== 'undefined' && window.dispatchEvent) {
                try {
                    const clearCartEvent = new CustomEvent(CART_CLEAR_EVENT);
                    window.dispatchEvent(clearCartEvent);
                    authDevLog('✅ Web platform: Cart clear event dispatched during logout');
                } catch (error) {
                    authDevWarn('⚠️ Failed to dispatch cart clear event during logout:', error);
                }
            }

            // 🧹 INACTIVITY TIMER TEMİZLİĞİ
            stopInactivityTimer();

            authDevLog('✅ Logout completed successfully');

            // Login sayfasına yönlendir
            if (router && typeof router.push === 'function') {
                try {
                    await router.push("/(auth)/login");
                    authDevLog('✅ Navigation to login page successful');
                } catch (navigationError) {
                    authDevError('❌ Navigation to login page failed:', navigationError);
                    // Fallback: window.location kullan (web için)
                    if (typeof window !== 'undefined') {
                        window.location.href = '/(auth)/login';
                    }
                }
            } else {
                authDevWarn('⚠️ Router not available for logout navigation');
                // Fallback: window.location kullan (web için)
                if (typeof window !== 'undefined') {
                    window.location.href = '/(auth)/login';
                }
            }
        } catch (error) {
            authDevError('❌ Logout error:', error);
            // Hata durumunda bile state'i temizle
            setUser(null);
            setIsAuthenticated(false);
            setJustLoggedIn(false);

            // Login sayfasına yönlendir
            if (router && typeof router.push === 'function') {
                try {
                    await router.push("/(auth)/login");
                } catch (navigationError) {
                    authDevError('Navigation failed after logout error:', navigationError);
                }
            }
        }
    }, [clearCartCache, stopInactivityTimer]);

    // 🚀 STABLE CONTEXT VALUE to prevent infinite loops
    const contextValue = React.useMemo(() => ({
        user,
        isAuthenticated,
        isLoading,
        isAuthReady,
        login,
        logout,
        checkAuthStatus: stableCheckAuthStatus
    }), [user, isAuthenticated, isLoading, isAuthReady, login, logout, stableCheckAuthStatus]);

    return (
        <AuthContext.Provider value={contextValue}>
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