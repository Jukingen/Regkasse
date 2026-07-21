// Bu hook, daha sağlıklı session persistence stratejisi sağlar
// Backend-first yaklaşım benimser, minimal local storage kullanır

import { useState, useEffect, useCallback } from 'react';

import { useAuth } from '../contexts/AuthContext';

interface SessionMetadata {
  lastActivityTime: number;
  sessionStartTime: number;
  userId: string;
  userRole: string;
}

/**
 * Sağlıklı session persistence stratejisi:
 * 1. Minimal local storage - sadece kritik olmayan metadata
 * 2. Backend-first data approach - her zaman fresh data
 * 3. Güvenlik odaklı - hassas veriler local'da tutulmaz
 */
export const useSessionPersistence = () => {
  const { user } = useAuth();
  const [sessionMetadata, setSessionMetadata] = useState<SessionMetadata | null>(null);

  /**
   * Session metadata'sını güncelle
   * SADECE güvenlik açısından kritik olmayan bilgiler
   */
  const updateSessionActivity = useCallback(() => {
    if (!user) return;

    const metadata: SessionMetadata = {
      lastActivityTime: Date.now(),
      sessionStartTime: sessionMetadata?.sessionStartTime ?? Date.now(),
      userId: user.id,
      userRole: user.role,
    };

    setSessionMetadata(metadata);

    // Session activity log - backend'e gönderebiliriz
    console.log('📊 Session activity updated:', {
      userId: user.id,
      lastActivity: new Date(metadata.lastActivityTime).toISOString(),
      sessionDuration:
        Math.round((Date.now() - metadata.sessionStartTime) / 1000 / 60) + ' minutes',
    });
  }, [user, sessionMetadata?.sessionStartTime]);

  /**
   * Session sona erdikten sonra temizlik
   */
  const clearSession = useCallback(() => {
    setSessionMetadata(null);
    console.log('🧹 Session metadata cleared');
  }, []);

  /**
   * Session süresi kontrolü
   */
  const isSessionActive = useCallback((): boolean => {
    if (!sessionMetadata || !user) return false;

    // 8 saatlik session timeout
    const SESSION_TIMEOUT = 8 * 60 * 60 * 1000; // 8 hours
    const timeSinceLastActivity = Date.now() - sessionMetadata.lastActivityTime;

    return timeSinceLastActivity < SESSION_TIMEOUT;
  }, [sessionMetadata, user]);

  /**
   * Session başlatma
   */
  useEffect(() => {
    if (user && !sessionMetadata) {
      const newSession: SessionMetadata = {
        lastActivityTime: Date.now(),
        sessionStartTime: Date.now(),
        userId: user.id,
        userRole: user.role,
      };
      setSessionMetadata(newSession);
      console.log('🚀 New session started for user:', user.id);
    }
  }, [user, sessionMetadata]);

  /**
   * User değiştiğinde session temizleme
   */
  useEffect(() => {
    if (!user && sessionMetadata) {
      clearSession();
    }
  }, [user, sessionMetadata, clearSession]);

  return {
    // Session state
    sessionMetadata,
    isSessionActive: isSessionActive(),
    sessionDuration: sessionMetadata
      ? Math.round((Date.now() - sessionMetadata.sessionStartTime) / 1000 / 60)
      : 0,

    // Actions
    updateSessionActivity,
    clearSession,

    // Recommendations
    recommendations: {
      // Masa siparişleri için
      tableOrders: {
        strategy: 'BACKEND_ONLY',
        reason: 'Always fetch fresh data from backend for accuracy and security',
        caching: 'Memory only, no persistence',
      },

      // User preferences için
      userPreferences: {
        strategy: 'HYBRID',
        reason: 'Non-sensitive UI preferences can be cached locally',
        caching: 'Local storage for theme, language, etc.',
      },

      // Authentication için
      authentication: {
        strategy: 'TOKEN_ONLY',
        reason: 'Store only JWT token, fetch user data from backend',
        caching: 'Secure token storage, user data from API',
      },
    },
  };
};
