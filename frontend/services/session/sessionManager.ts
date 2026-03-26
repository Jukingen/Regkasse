import { jwtDecode } from 'jwt-decode';
import { storage } from '../../utils/storage';

export const SESSION_KEYS = {
  token: 'token',
  refreshToken: 'refreshToken',
  user: 'user',
  tokenExpiry: 'tokenExpiry',
} as const;

export interface StoredSessionUser {
  id: string;
  username?: string;
  email: string;
  role: string;
  firstName?: string;
  lastName?: string;
  roles?: string[];
  permissions?: string[];
  isDemo?: boolean;
}

export interface SessionSnapshot {
  accessToken: string | null;
  refreshToken: string | null;
  user: StoredSessionUser | null;
}

class SessionManager {
  private refreshPromise: Promise<string | null> | null = null;

  normalizeToken(token: string): string {
    const trimmed = token.trim();
    return trimmed.startsWith('Bearer ') ? trimmed.slice(7).trim() : trimmed;
  }

  async getAccessToken(): Promise<string | null> {
    const token = await storage.getItem(SESSION_KEYS.token);
    if (!token) return null;
    const cleanToken = this.normalizeToken(token);
    if (!cleanToken) {
      await storage.removeItem(SESSION_KEYS.token);
      return null;
    }
    return cleanToken;
  }

  async getRefreshToken(): Promise<string | null> {
    const refreshToken = await storage.getItem(SESSION_KEYS.refreshToken);
    if (!refreshToken) return null;
    const cleanRefreshToken = refreshToken.trim();
    if (!cleanRefreshToken) {
      await storage.removeItem(SESSION_KEYS.refreshToken);
      return null;
    }
    return cleanRefreshToken;
  }

  async getStoredUser(): Promise<StoredSessionUser | null> {
    const raw = await storage.getItem(SESSION_KEYS.user);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as StoredSessionUser;
    } catch {
      return null;
    }
  }

  async getSnapshot(): Promise<SessionSnapshot> {
    const [accessToken, refreshToken, user] = await Promise.all([
      this.getAccessToken(),
      this.getRefreshToken(),
      this.getStoredUser(),
    ]);
    return { accessToken, refreshToken, user };
  }

  isExpired(token: string, bufferSeconds = 0): boolean {
    try {
      const decoded = jwtDecode(token) as { exp?: number };
      if (!decoded.exp) return false;
      const now = Date.now() / 1000;
      return decoded.exp <= now + bufferSeconds;
    } catch {
      return true;
    }
  }

  async persistSession(input: {
    token: string;
    refreshToken?: string | null;
    user?: StoredSessionUser | null;
  }): Promise<void> {
    const cleanToken = this.normalizeToken(input.token);
    if (!cleanToken) {
      throw new Error('Cannot persist an empty access token.');
    }
    await storage.setItem(SESSION_KEYS.token, cleanToken);
    await storage.setItem(SESSION_KEYS.tokenExpiry, Date.now().toString());

    if (input.refreshToken) {
      const cleanRefreshToken = input.refreshToken.trim();
      if (cleanRefreshToken) {
        await storage.setItem(SESSION_KEYS.refreshToken, cleanRefreshToken);
      }
    }

    if (input.user) {
      await storage.setItem(SESSION_KEYS.user, JSON.stringify(input.user));
    }
  }

  async clearSession(): Promise<void> {
    await storage.multiRemove([
      SESSION_KEYS.token,
      SESSION_KEYS.refreshToken,
      SESSION_KEYS.user,
      SESSION_KEYS.tokenExpiry,
    ]);
  }

  async refreshAccessToken(
    refreshCall: (refreshToken: string) => Promise<{ token: string }>
  ): Promise<string | null> {
    if (this.refreshPromise) return this.refreshPromise;

    this.refreshPromise = (async () => {
      const refreshToken = await this.getRefreshToken();
      if (!refreshToken) return null;

      try {
        const response = await refreshCall(refreshToken);
        if (!response?.token) {
          await this.clearSession();
          return null;
        }
        const cleanToken = this.normalizeToken(response.token);
        await storage.setItem(SESSION_KEYS.token, cleanToken);
        await storage.setItem(SESSION_KEYS.tokenExpiry, Date.now().toString());
        return cleanToken;
      } catch {
        await this.clearSession();
        return null;
      } finally {
        this.refreshPromise = null;
      }
    })();

    return this.refreshPromise;
  }
}

export const sessionManager = new SessionManager();
