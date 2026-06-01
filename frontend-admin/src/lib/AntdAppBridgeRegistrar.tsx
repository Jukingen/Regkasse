'use client';

import { App } from 'antd';
import { useEffect } from 'react';
import { registerAntdApp, unregisterAntdApp } from '@/lib/antdAppBridge';

/** Registers App.useApp() message + modal for axios interceptors and query error handling. */
export function AntdAppBridgeRegistrar() {
  const { message, modal } = App.useApp();

  registerAntdApp({ message, modal });

  useEffect(() => () => unregisterAntdApp(), []);

  return null;
}
