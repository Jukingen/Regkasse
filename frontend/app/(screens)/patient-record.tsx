import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, ScrollView, StyleSheet, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { DynamicField } from '../../components/DynamicField';
import { IfVerticalFeature } from '../../components/IfVerticalFeature';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { customerService } from '../../services/api/customerService';

export default function PatientRecordScreen() {
  const { t } = useTranslation('verticalProfiles');
  const [customerName, setCustomerName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [petName, setPetName] = useState('');
  const [petSpecies, setPetSpecies] = useState('');
  const [petBreed, setPetBreed] = useState('');
  const [petBirthDate, setPetBirthDate] = useState('');
  const [saving, setSaving] = useState(false);

  const saveCustomer = async () => {
    if (!customerName.trim() || !petName.trim()) {
      Alert.alert(t('screens.patientRecord.validationTitle'), t('screens.patientRecord.required'));
      return;
    }

    setSaving(true);
    try {
      await customerService.create({
        name: customerName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        address: '',
        petData: {
          petName: petName.trim(),
          petSpecies: petSpecies.trim() || null,
          petBreed: petBreed.trim() || null,
          petBirthDate: petBirthDate.trim() || null,
        },
      });
      Alert.alert(t('screens.patientRecord.savedTitle'), t('screens.patientRecord.saved'));
      setCustomerName('');
      setPhone('');
      setEmail('');
      setPetName('');
      setPetSpecies('');
      setPetBreed('');
      setPetBirthDate('');
    } catch {
      Alert.alert(t('screens.patientRecord.saveFailedTitle'), t('screens.patientRecord.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <IfVerticalFeature
      feature="patientRecord"
      fallback={
        <SafeAreaView style={styles.container}>
          <Text style={styles.empty}>{t('screens.unavailable')}</Text>
        </SafeAreaView>
      }
    >
      <SafeAreaView style={styles.container}>
        <ScrollView contentContainerStyle={styles.content}>
          <Text style={styles.title}>{t('screens.patientRecord.title')}</Text>
          <Text style={styles.subtitle}>{t('screens.patientRecord.subtitle')}</Text>
          <DynamicField
            name="name"
            entity="customer"
            value={customerName}
            onChangeText={setCustomerName}
          />
          <DynamicField
            name="phone"
            entity="customer"
            value={phone}
            onChangeText={setPhone}
            keyboardType="phone-pad"
          />
          <DynamicField
            name="email"
            entity="customer"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
          />
          <DynamicField
            name="petName"
            entity="customer"
            value={petName}
            onChangeText={setPetName}
          />
          <DynamicField
            name="petSpecies"
            entity="customer"
            value={petSpecies}
            onChangeText={setPetSpecies}
          />
          <DynamicField
            name="petBreed"
            entity="customer"
            value={petBreed}
            onChangeText={setPetBreed}
          />
          <DynamicField
            name="petBirthDate"
            entity="customer"
            value={petBirthDate}
            onChangeText={setPetBirthDate}
          />
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('screens.patientRecord.save')}
            disabled={saving}
            onPress={() => {
              saveCustomer().catch(() => undefined);
            }}
            style={[styles.saveButton, saving && styles.disabled]}
          >
            <Text style={styles.saveButtonText}>
              {saving ? t('screens.patientRecord.saving') : t('screens.patientRecord.save')}
            </Text>
          </Pressable>
        </ScrollView>
      </SafeAreaView>
    </IfVerticalFeature>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  content: {
    padding: SoftSpacing.lg,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: SoftColors.textPrimary,
    marginBottom: SoftSpacing.xs,
  },
  subtitle: {
    color: SoftColors.textSecondary,
    lineHeight: 20,
    marginBottom: SoftSpacing.lg,
  },
  saveButton: {
    marginTop: SoftSpacing.md,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: SoftRadius.md,
    backgroundColor: SoftColors.accent,
  },
  saveButtonText: {
    color: SoftColors.textInverse,
    fontWeight: '700',
    fontSize: 16,
  },
  disabled: {
    opacity: 0.6,
  },
  empty: {
    color: SoftColors.textMuted,
    textAlign: 'center',
    marginTop: SoftSpacing.xl,
  },
});
