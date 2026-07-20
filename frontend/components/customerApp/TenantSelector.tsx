import React, { useCallback, useState } from 'react';
import {
  ActivityIndicator,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { parseTenantSlugFromPayload } from '../../services/customerApp/customerTenantStorage';

type Props = {
  onSelect: (slug: string) => void;
  isLoading?: boolean;
  errorKey?: string | null;
};

/**
 * Shared customer app: pick restaurant by slug or paste QR / deep link.
 * UI: German (de-DE).
 */
export function TenantSelector({ onSelect, isLoading, errorKey }: Props) {
  const { t } = useTranslation('orders');
  const [input, setInput] = useState('');

  const onConfirm = useCallback(() => {
    const slug = parseTenantSlugFromPayload(input) ?? input.trim().toLowerCase();
    if (!slug) return;
    onSelect(slug);
  }, [input, onSelect]);

  return (
    <View style={styles.wrap}>
      <Text style={styles.title}>{t('customerApp.selectTitle')}</Text>
      <Text style={styles.subtitle}>{t('customerApp.selectSubtitle')}</Text>

      <TextInput
        style={styles.input}
        placeholder={t('customerApp.slugPlaceholder')}
        placeholderTextColor="#94a3b8"
        autoCapitalize="none"
        autoCorrect={false}
        value={input}
        onChangeText={setInput}
        onSubmitEditing={onConfirm}
        returnKeyType="go"
        editable={!isLoading}
      />

      <TouchableOpacity
        style={[styles.button, isLoading && styles.buttonDisabled]}
        onPress={onConfirm}
        disabled={isLoading}
      >
        {isLoading ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.buttonText}>{t('customerApp.continue')}</Text>
        )}
      </TouchableOpacity>

      {errorKey === 'not_found' ? (
        <Text style={styles.error}>{t('customerApp.notFound')}</Text>
      ) : null}
      {errorKey === 'load_failed' ? (
        <Text style={styles.error}>{t('customerApp.loadFailed')}</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flex: 1,
    padding: 20,
    justifyContent: 'center',
    backgroundColor: '#f8fafc',
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: '#0f172a',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 14,
    color: '#64748b',
    marginBottom: 20,
    lineHeight: 20,
  },
  input: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: '#0f172a',
    marginBottom: 12,
  },
  button: {
    backgroundColor: '#2563eb',
    borderRadius: 10,
    paddingVertical: 14,
    alignItems: 'center',
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  buttonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  error: {
    color: '#b91c1c',
    marginTop: 12,
  },
});
