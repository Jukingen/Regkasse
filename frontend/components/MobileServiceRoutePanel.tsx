import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import {
  formatServiceAddress,
  toAddressPayload,
  useMobileServiceJob,
  type ServiceAddress,
} from '../contexts/MobileServiceJobContext';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';
import { customerService } from '../services/api/customerService';

export function MobileServiceRoutePanel(props: {
  customerName?: string;
  customerPhone?: string;
  customerId?: string | null;
}) {
  const { posFeatures } = useVerticalFeatures();
  if (posFeatures.routeTracking !== true) {
    return null;
  }
  return <MobileServiceRouteFields {...props} />;
}

function MobileServiceRouteFields({
  customerName,
  customerPhone,
  customerId,
}: {
  customerName?: string;
  customerPhone?: string;
  customerId?: string | null;
}) {
  const { t } = useTranslation('verticalProfiles');
  const job = useMobileServiceJob();
  const [saving, setSaving] = useState(false);

  const saveCustomerAddress = async () => {
    const name = customerName?.trim();
    if (!name) {
      Alert.alert(t('screens.mobileServices.validationTitle'), t('screens.mobileServices.nameRequired'));
      return;
    }

    setSaving(true);
    try {
      const payload = toAddressPayload(job.customerAddress);
      if (customerId) {
        await customerService.updateAddress(customerId, {
          address: formatServiceAddress(job.customerAddress),
          addressData: payload,
        });
      } else {
        await customerService.create({
          name,
          email: '',
          phone: customerPhone?.trim() ?? '',
          address: formatServiceAddress(job.customerAddress),
          addressData: payload,
        });
      }
      Alert.alert(t('screens.mobileServices.savedTitle'), t('screens.mobileServices.saved'));
    } catch {
      Alert.alert(t('screens.mobileServices.saveFailedTitle'), t('screens.mobileServices.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <View style={styles.wrap}>
      <Text style={styles.title}>{t('screens.mobileServices.title')}</Text>
      <Text style={styles.subtitle}>{t('screens.mobileServices.subtitle')}</Text>

      <Text style={styles.section}>{t('screens.mobileServices.customerAddress')}</Text>
      <AddressFields
        value={job.customerAddress}
        onChange={job.setCustomerAddress}
        prefix="customer"
      />
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('screens.mobileServices.saveAddress')}
        disabled={saving}
        onPress={() => {
          saveCustomerAddress().catch(() => undefined);
        }}
        style={[styles.saveButton, saving && styles.disabled]}
      >
        <Text style={styles.saveButtonText}>
          {saving ? t('screens.mobileServices.saving') : t('screens.mobileServices.saveAddress')}
        </Text>
      </Pressable>

      <Text style={styles.section}>{t('screens.mobileServices.jobLocation')}</Text>
      <AddressFields value={job.jobLocation} onChange={job.setJobLocation} prefix="job" />
    </View>
  );
}

function AddressFields({
  value,
  onChange,
  prefix,
}: {
  value: ServiceAddress;
  onChange: (next: ServiceAddress) => void;
  prefix: 'customer' | 'job';
}) {
  const { t } = useTranslation('verticalProfiles');
  const set = (key: keyof ServiceAddress) => (text: string) => onChange({ ...value, [key]: text });

  return (
    <View>
      <Text style={styles.label}>{t('fields.street')}</Text>
      <TextInput
        accessibilityLabel={`${prefix}-${t('fields.street')}`}
        value={value.street}
        onChangeText={set('street')}
        placeholder={t('placeholders.street')}
        style={styles.input}
      />
      <Text style={styles.label}>{t('fields.postalCode')}</Text>
      <TextInput
        accessibilityLabel={`${prefix}-${t('fields.postalCode')}`}
        value={value.postalCode}
        onChangeText={set('postalCode')}
        placeholder={t('placeholders.postalCode')}
        style={styles.input}
      />
      <Text style={styles.label}>{t('fields.city')}</Text>
      <TextInput
        accessibilityLabel={`${prefix}-${t('fields.city')}`}
        value={value.city}
        onChangeText={set('city')}
        placeholder={t('placeholders.city')}
        style={styles.input}
      />
      <Text style={styles.label}>{t('fields.locationNotes')}</Text>
      <TextInput
        accessibilityLabel={`${prefix}-${t('fields.locationNotes')}`}
        value={value.notes}
        onChangeText={set('notes')}
        placeholder={t('placeholders.locationNotes')}
        style={styles.input}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    marginBottom: SoftSpacing.lg,
    padding: SoftSpacing.md,
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.bgPrimary,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.md,
  },
  section: {
    fontSize: 14,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginTop: SoftSpacing.sm,
    marginBottom: SoftSpacing.xs,
  },
  label: {
    color: SoftColors.textSecondary,
    marginBottom: 4,
  },
  input: {
    minHeight: 44,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgPrimary,
  },
  saveButton: {
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.accent,
    marginBottom: SoftSpacing.sm,
  },
  saveButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
  },
  disabled: {
    opacity: 0.6,
  },
});
