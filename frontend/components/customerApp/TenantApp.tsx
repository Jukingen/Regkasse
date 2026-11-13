import { useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';

import type { CustomerTenantProfile, CustomerTenantMenu } from '../../services/customerApp/publicTenantApi';
import { loadTenantMenu } from '../../services/customerApp/publicTenantApi';

type Props = {
  tenant: CustomerTenantProfile;
  onChangeTenant: () => void;
};

function money(value: number, currency: string): string {
  try {
    return new Intl.NumberFormat('de-AT', { style: 'currency', currency }).format(value);
  } catch {
    return `€ ${value.toFixed(2)}`;
  }
}

/**
 * Shared customer app shell for one selected tenant (menu + portal links).
 * UI: German (de-DE).
 */
export function TenantApp({ tenant, onChangeTenant }: Props) {
  const { t } = useTranslation('orders');
  const router = useRouter();
  const [menu, setMenu] = useState<CustomerTenantMenu | null>(null);
  const [loadingMenu, setLoadingMenu] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoadingMenu(true);
    void loadTenantMenu(tenant.slug)
      .then((m) => {
        if (!cancelled) setMenu(m);
      })
      .catch(() => {
        if (!cancelled) setMenu({ slug: tenant.slug, currency: 'EUR', items: [] });
      })
      .finally(() => {
        if (!cancelled) setLoadingMenu(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tenant.slug]);

  const openTracker = useCallback(() => {
    router.push('/order-tracker');
  }, [router]);

  const openDashboard = useCallback(() => {
    router.push('/customer/dashboard');
  }, [router]);

  return (
    <ScrollView contentContainerStyle={styles.container}>
      <View style={[styles.hero, { backgroundColor: tenant.primaryColor }]}>
        <Text style={styles.heroTitle}>{tenant.displayName}</Text>
        {tenant.description ? <Text style={styles.heroDesc}>{tenant.description}</Text> : null}
      </View>

      <View style={styles.actions}>
        <TouchableOpacity style={styles.actionBtn} onPress={openTracker}>
          <Text style={styles.actionText}>{t('customerApp.orderStatus')}</Text>
        </TouchableOpacity>
        <TouchableOpacity style={styles.actionBtnSecondary} onPress={openDashboard}>
          <Text style={styles.actionTextSecondary}>{t('customerApp.myPortal')}</Text>
        </TouchableOpacity>
        <TouchableOpacity onPress={onChangeTenant}>
          <Text style={styles.changeLink}>{t('customerApp.changeRestaurant')}</Text>
        </TouchableOpacity>
      </View>

      <Text style={[styles.sectionTitle, { color: tenant.accentColor }]}>
        {t('customerApp.menu')}
      </Text>

      {loadingMenu ? (
        <ActivityIndicator color={tenant.primaryColor} style={{ marginTop: 16 }} />
      ) : menu && menu.items.length > 0 ? (
        menu.items.map((item) => (
          <View key={item.id || item.name} style={styles.menuRow}>
            <View style={styles.menuMain}>
              <Text style={styles.menuName}>{item.name}</Text>
              {item.categoryName ? <Text style={styles.menuCat}>{item.categoryName}</Text> : null}
            </View>
            <Text style={styles.menuPrice}>{money(item.price, menu.currency)}</Text>
          </View>
        ))
      ) : (
        <Text style={styles.empty}>{t('customerApp.menuEmpty')}</Text>
      )}

      {(tenant.address || tenant.phone) && (
        <View style={styles.contact}>
          {tenant.address ? <Text style={styles.contactLine}>{tenant.address}</Text> : null}
          {tenant.phone ? <Text style={styles.contactLine}>{tenant.phone}</Text> : null}
        </View>
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingBottom: 40,
    backgroundColor: '#f8fafc',
    flexGrow: 1,
  },
  hero: {
    paddingHorizontal: 20,
    paddingVertical: 28,
  },
  heroTitle: {
    fontSize: 26,
    fontWeight: '700',
    color: '#fff',
  },
  heroDesc: {
    marginTop: 8,
    fontSize: 14,
    color: 'rgba(255,255,255,0.9)',
    lineHeight: 20,
  },
  actions: {
    padding: 16,
    gap: 10,
  },
  actionBtn: {
    backgroundColor: '#2563eb',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
  },
  actionBtnSecondary: {
    backgroundColor: '#fff',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#e2e8f0',
  },
  actionText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 15,
  },
  actionTextSecondary: {
    color: '#0f172a',
    fontWeight: '600',
    fontSize: 15,
  },
  changeLink: {
    textAlign: 'center',
    color: '#2563eb',
    marginTop: 4,
    fontSize: 14,
  },
  sectionTitle: {
    paddingHorizontal: 16,
    fontSize: 16,
    fontWeight: '700',
    marginBottom: 8,
  },
  menuRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#e2e8f0',
    backgroundColor: '#fff',
  },
  menuMain: {
    flex: 1,
    paddingRight: 12,
  },
  menuName: {
    fontSize: 15,
    fontWeight: '600',
    color: '#0f172a',
  },
  menuCat: {
    fontSize: 12,
    color: '#64748b',
    marginTop: 2,
  },
  menuPrice: {
    fontSize: 15,
    fontWeight: '700',
    color: '#0f172a',
  },
  empty: {
    padding: 16,
    color: '#64748b',
  },
  contact: {
    marginTop: 24,
    paddingHorizontal: 16,
  },
  contactLine: {
    fontSize: 13,
    color: '#64748b',
    marginBottom: 4,
  },
});
