import { axiosInstance } from './config';

export type TenantSessionPolicy = {
  sessionTimeoutMinutes: number;
  warningBeforeTimeoutMinutes: number;
  keepCartAfterTimeout: boolean;
};

const DEFAULT_POLICY: TenantSessionPolicy = {
  sessionTimeoutMinutes: 30,
  warningBeforeTimeoutMinutes: 5,
  keepCartAfterTimeout: true,
};

export async function fetchTenantSessionPolicy(): Promise<TenantSessionPolicy> {
  try {
    const res = await axiosInstance.get<TenantSessionPolicy>('/api/user/session-policy');
    return {
      sessionTimeoutMinutes: res.data.sessionTimeoutMinutes ?? DEFAULT_POLICY.sessionTimeoutMinutes,
      warningBeforeTimeoutMinutes:
        res.data.warningBeforeTimeoutMinutes ?? DEFAULT_POLICY.warningBeforeTimeoutMinutes,
      keepCartAfterTimeout: res.data.keepCartAfterTimeout ?? DEFAULT_POLICY.keepCartAfterTimeout,
    };
  } catch {
    return DEFAULT_POLICY;
  }
}

export async function sendSessionHeartbeat(): Promise<void> {
  try {
    await axiosInstance.post('/api/user/sessions/heartbeat');
  } catch {
    /* best effort */
  }
}
