import type { MessageInstance } from 'antd/es/message/interface';
import type { HookAPI } from 'antd/es/modal/useModal';

type AntdAppApis = {
  message: MessageInstance;
  modal: HookAPI;
};

let apis: AntdAppApis | null = null;
const pendingErrors: string[] = [];

export function registerAntdApp(next: AntdAppApis): void {
  apis = next;
  for (const content of pendingErrors) {
    next.message.error(content);
  }
  pendingErrors.length = 0;
}

export function unregisterAntdApp(): void {
  apis = null;
}

export function showAntdError(content: string): void {
  if (apis?.message) {
    apis.message.error(content);
    return;
  }
  pendingErrors.push(content);
}

function requireMessage(): MessageInstance {
  if (!apis?.message) {
    throw new Error('Ant Design App message API is not registered yet.');
  }
  return apis.message;
}

function requireModal(): HookAPI {
  if (!apis?.modal) {
    throw new Error('Ant Design App modal API is not registered yet.');
  }
  return apis.modal;
}

/** Context-aware message API (use instead of `import { message } from 'antd'`). */
export const appMessage: MessageInstance = new Proxy({} as MessageInstance, {
  get(_target, prop) {
    const api = requireMessage();
    const value = api[prop as keyof MessageInstance];
    return typeof value === 'function' ? value.bind(api) : value;
  },
});

/** Context-aware modal static API (use instead of `Modal.confirm`, etc.). */
export const appModal: HookAPI = new Proxy({} as HookAPI, {
  get(_target, prop) {
    const api = requireModal();
    const value = api[prop as keyof HookAPI];
    return typeof value === 'function' ? value.bind(api) : value;
  },
});
