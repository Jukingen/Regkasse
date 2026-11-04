import { Redirect } from 'expo-router';
import React from 'react';

/**
 * Retired floor tab. Open tables are chosen on the cash register.
 * Kept so an old /orders link does not 404; the tab bar hides this screen.
 */
export default function OrdersRedirect() {
  return <Redirect href="/(tabs)/cash-register" />;
}
