import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Pressable, ScrollView, StyleSheet, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { DynamicField } from '../../components/DynamicField';
import { IfVerticalFeature } from '../../components/IfVerticalFeature';
import { SoftColors, SoftRadius, SoftSpacing } from '../../constants/SoftTheme';
import { useVerticalFeatures } from '../../contexts/VerticalProfileContext';
import { customerService } from '../../services/api/customerService';

export default function PatientRecordScreen() {
  const { t } = useTranslation('verticalProfiles');
  const { profileId } = useVerticalFeatures();
  const isDoctor = profileId === 'doctor';
  const [customerName, setCustomerName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [petName, setPetName] = useState('');
  const [petSpecies, setPetSpecies] = useState('');
  const [petBreed, setPetBreed] = useState('');
  const [petBirthDate, setPetBirthDate] = useState('');
  const [vaccines, setVaccines] = useState('');
  const [birthDate, setBirthDate] = useState('');
  const [allergies, setAllergies] = useState('');
  const [chronicConditions, setChronicConditions] = useState('');
  const [insurance, setInsurance] = useState('');
  const [saving, setSaving] = useState(false);

  const saveCustomer = async () => {
    if (!customerName.trim() || (!isDoctor && !petName.trim())) {
      Alert.alert(
        t('screens.patientRecord.validationTitle'),
        t(isDoctor ? 'screens.patientRecord.doctorRequired' : 'screens.patientRecord.required')
      );
      return;
    }

    setSaving(true);
    try {
      await customerService.create({
        name: customerName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        address: '',
        petData: isDoctor
          ? {
              birthDate: birthDate.trim() || null,
              allergies: allergies.trim() || null,
              chronicConditions: chronicConditions.trim() || null,
              insurance: insurance.trim() || null,
            }
          : {
              petName: petName.trim(),
              petSpecies: petSpecies.trim() || null,
              petBreed: petBreed.trim() || null,
              petBirthDate: petBirthDate.trim() || null,
              vaccines: vaccines.trim() || null,
            },
      });
      Alert.alert(
        t('screens.patientRecord.savedTitle'),
        t(isDoctor ? 'screens.patientRecord.doctorSaved' : 'screens.patientRecord.saved')
      );
      setCustomerName('');
      setPhone('');
      setEmail('');
      setPetName('');
      setPetSpecies('');
      setPetBreed('');
      setPetBirthDate('');
      setVaccines('');
      setBirthDate('');
      setAllergies('');
      setChronicConditions('');
      setInsurance('');
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
          <Text style={styles.title}>
            {t(isDoctor ? 'screens.patientRecord.doctorTitle' : 'screens.patientRecord.vetTitle')}
          </Text>
          <Text style={styles.subtitle}>
            {t(isDoctor ? 'screens.patientRecord.doctorSubtitle' : 'screens.patientRecord.subtitle')}
          </Text>
          <DynamicField
            name="name"
            entity="customer"
            label={isDoctor ? t('fields.patientName') : undefined}
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
          {isDoctor ? (
            <>
              <DynamicField
                name="birthDate"
                entity="customer"
                value={birthDate}
                onChangeText={setBirthDate}
              />
              <DynamicField
                name="allergies"
                entity="customer"
                value={allergies}
                onChangeText={setAllergies}
              />
              <DynamicField
                name="chronicConditions"
                entity="customer"
                value={chronicConditions}
                onChangeText={setChronicConditions}
              />
              <DynamicField
                name="insurance"
                entity="customer"
                value={insurance}
                onChangeText={setInsurance}
              />
            </>
          ) : (
            <>
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
              <DynamicField
                name="vaccines"
                entity="customer"
                value={vaccines}
                onChangeText={setVaccines}
              />
            </>
          )}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t(
              isDoctor ? 'screens.patientRecord.doctorSave' : 'screens.patientRecord.save'
            )}
            disabled={saving}
            onPress={() => {
              saveCustomer().catch(() => undefined);
            }}
            style={[styles.saveButton, saving && styles.disabled]}
          >
            <Text style={styles.saveButtonText}>
              {saving
                ? t('screens.patientRecord.saving')
                : t(isDoctor ? 'screens.patientRecord.doctorSave' : 'screens.patientRecord.save')}
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
