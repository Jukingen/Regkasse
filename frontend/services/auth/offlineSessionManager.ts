import { jwtDecode } from 'jwt-decode';

import { OfflineConfigService } from '@/services/config/offlineConfigService';
import { secureStorage } from '@/services/secureStorage';
import type { StoredSessionUser } from '@/services/session/sessionManager';

const OFFLINE_SESSION_STORAGE_KEY = 'offline_session';

export interface OfflineSession {
  token: string;
  refreshToken?: string;
  user: StoredSessionUser;
  tenantId: string;
  tenantSlug: string;
  expiresAt: number;
  lastActivityAt: number;
}

export interface OfflineSessionInput {
  token: string;
  refreshToken?: string;
  user: StoredSessionUser;
  tenantId: string;
  tenantSlug: string;
}

type JwtPayloadWithExp = {
  exp?: number;
};

function normalizeToken(token: string): string {
  const trimmed = token.trim();
  return trimmed.startsWith('Bearer ') ? trimmed.slice(7).trim() : trimmed;
}

function isOfflineSession(value: unknown): value is OfflineSession {
  if (value == null || typeof value !== 'object') return false;
  const row = value as Record<string, unknown>;
  return (
    typeof row.token === 'string' &&
    typeof row.tenantId === 'string' &&
    typeof row.tenantSlug === 'string' &&
    typeof row.expiresAt === 'number' &&
    typeof row.lastActivityAt === 'number' &&
    row.user != null &&
    typeof row.user === 'object'
  );
}

export class OfflineSessionManager {
  private static instance: OfflineSessionManager;
  private readonly config: OfflineConfigService;
  private session: OfflineSession | null = null;

  private constructor() {
    this.config = OfflineConfigService.getInstance();
    void this.loadSession();
  }

  static getInstance(): OfflineSessionManager {
    if (!OfflineSessionManager.instance) {
      OfflineSessionManager.instance = new OfflineSessionManager();
    }
    return OfflineSessionManager.instance;
  }

  /** Persist session after successful online login */
  async saveSession(sessionData: OfflineSessionInput): Promise<void> {
    const token = normalizeToken(sessionData.token);
    const decoded = jwtDecode<JwtPayloadWithExp>(token);
    const expiresAt = decoded.exp
      ? decoded.exp * 1000
      : Date.now() + this.config.getTokenExpiryMs();

    this.session = {
      token,
      refreshToken: sessionData.refreshToken,
      user: sessionData.user,
      tenantId: sessionData.tenantId,
      tenantSlug: sessionData.tenantSlug,
      expiresAt,
      lastActivityAt: Date.now(),
    };

    await secureStorage.setItem(OFFLINE_SESSION_STORAGE_KEY, JSON.stringify(this.session));
  }

  /** Active session for offline API usage */
  getSession(): OfflineSession | null {
    if (!this.session) return null;
    if (this.isTokenExpired()) return null;
    return this.session;
  }

  /** Bearer token when session is still valid */
  getToken(): string | null {
    if (!this.session || this.isTokenExpired()) return null;
    return this.session.token;
  }

  isTokenExpired(): boolean {
    if (!this.session) return true;
    return Date.now() > this.session.expiresAt;
  }

  isTokenExpiringSoon(): boolean {
    if (!this.session) return true;
    const thresholdMs = this.config.get('TOKEN_REFRESH_THRESHOLD_HOURS') * 60 * 60 * 1000;
    return Date.now() > this.session.expiresAt - thresholdMs;
  }

  updateActivity(): void {
    if (!this.session) return;
    this.session.lastActivityAt = Date.now();
    void secureStorage.setItem(OFFLINE_SESSION_STORAGE_KEY, JSON.stringify(this.session));
  }

  async clearSession(): Promise<void> {
    this.session = null;
    await secureStorage.removeItem(OFFLINE_SESSION_STORAGE_KEY);
  }

  canWorkOffline(): boolean {
    if (!this.session) return false;
    return !this.isTokenExpired();
  }

  getRemainingOfflineHours(): number {
    if (!this.session) return 0;
    const remaining = (this.session.expiresAt - Date.now()) / (1000 * 60 * 60);
    return Math.max(0, Math.floor(remaining));
  }

  private async loadSession(): Promise<void> {
    try {
      const data = await secureStorage.getItem(OFFLINE_SESSION_STORAGE_KEY);
      if (!data) return;

      const parsed: unknown = JSON.parse(data);
      if (!isOfflineSession(parsed)) {
        await secureStorage.removeItem(OFFLINE_SESSION_STORAGE_KEY);
        return;
      }

      this.session = {
        ...parsed,
        token: normalizeToken(parsed.token),
      };
    } catch (error) {
      console.warn('Failed to load session:', error);
    }
  }
}
