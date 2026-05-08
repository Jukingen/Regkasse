import { Alert } from 'react-native';

type ApiMessageCarrier = {
  message?: unknown;
  data?: {
    message?: unknown;
  };
};

export function extractApiMessage(payload: unknown, fallback: string): string {
  const source = payload as ApiMessageCarrier | null | undefined;
  const nested = source?.data?.message;
  if (typeof nested === 'string' && nested.trim().length > 0) {
    return nested.trim();
  }
  const direct = source?.message;
  if (typeof direct === 'string' && direct.trim().length > 0) {
    return direct.trim();
  }
  return fallback;
}

export function showToast(title: string, message: string): void {
  const safeTitle = title.trim().length > 0 ? title : 'Hinweis';
  const safeMessage = message.trim().length > 0 ? message : 'Vorgang abgeschlossen.';
  Alert.alert(safeTitle, safeMessage);
}
