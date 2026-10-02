import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { BarcodeScannerModal } from '../../components/BarcodeScannerModal';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../../constants/SoftTheme';
import { useVerticalFeatures } from '../../contexts/VerticalProfileContext';
import {
  redeemPosTicket,
  validatePosTicket,
  type TicketValidation,
} from '../../services/api/ticketService';

export default function TicketValidateScreen() {
  const { t } = useTranslation(['verticalProfiles', 'common']);
  const router = useRouter();
  const { posFeatures } = useVerticalFeatures();
  const [code, setCode] = useState('');
  const [scannerOpen, setScannerOpen] = useState(false);
  const [ticket, setTicket] = useState<TicketValidation | null>(null);
  const [invalid, setInvalid] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const runValidate = useCallback(async (raw: string) => {
    const trimmed = raw.trim();
    setCode(trimmed);
    setError(null);
    setInvalid(false);
    setTicket(null);
    if (!trimmed) return;
    setBusy(true);
    try {
      const result = await validatePosTicket(trimmed);
      if (!result) {
        setInvalid(true);
        return;
      }
      setTicket(result);
    } catch {
      setInvalid(true);
    } finally {
      setBusy(false);
    }
  }, []);

  const runRedeem = useCallback(async () => {
    const trimmed = code.trim();
    if (!trimmed) return;
    setBusy(true);
    setError(null);
    try {
      const result = await redeemPosTicket(trimmed);
      setTicket(result);
      setInvalid(false);
    } catch (err) {
      const codeValue =
        err && typeof err === 'object' && 'code' in err ? String((err as { code?: string }).code) : '';
      if (codeValue === 'TICKET_ALREADY_REDEEMED') {
        setError(t('verticalProfiles:screens.tickets.alreadyRedeemed'));
      } else if (codeValue === 'TICKET_EXPIRED') {
        setError(t('verticalProfiles:screens.tickets.expired'));
      } else {
        setInvalid(true);
        setTicket(null);
      }
    } finally {
      setBusy(false);
    }
  }, [code, t]);

  if (!posFeatures.ticketScan) {
    return (
      <SafeAreaView style={styles.screen}>
        <Text style={styles.title}>{t('verticalProfiles:screens.unavailable')}</Text>
        <Pressable onPress={() => router.back()} accessibilityRole="button">
          <Text style={styles.link}>{t('common:actions.back', { defaultValue: 'Zurück' })}</Text>
        </Pressable>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.screen}>
      <Text style={styles.title}>{t('verticalProfiles:screens.tickets.title')}</Text>
      <Text style={styles.subtitle}>{t('verticalProfiles:screens.tickets.subtitle')}</Text>

      <TextInput
        value={code}
        onChangeText={setCode}
        autoCapitalize="characters"
        placeholder={t('verticalProfiles:screens.tickets.codePlaceholder')}
        style={styles.input}
        accessibilityLabel={t('verticalProfiles:screens.tickets.codePlaceholder')}
      />

      <View style={styles.row}>
        <Pressable
          style={styles.button}
          onPress={() => void runValidate(code)}
          disabled={busy}
          accessibilityRole="button"
          accessibilityLabel={t('verticalProfiles:screens.tickets.validate')}>
          <Text style={styles.buttonText}>{t('verticalProfiles:screens.tickets.validate')}</Text>
        </Pressable>
        <Pressable
          style={styles.buttonSecondary}
          onPress={() => setScannerOpen(true)}
          accessibilityRole="button"
          accessibilityLabel={t('verticalProfiles:screens.tickets.scan')}>
          <Ionicons name="qr-code-outline" size={18} color={SoftColors.textPrimary} />
          <Text style={styles.buttonSecondaryText}>{t('verticalProfiles:screens.tickets.scan')}</Text>
        </Pressable>
      </View>

      {invalid ? (
        <Text style={styles.error}>{t('verticalProfiles:screens.tickets.invalid')}</Text>
      ) : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}

      {ticket ? (
        <View style={styles.card}>
          <Text style={styles.status}>
            {ticket.isValid
              ? t('verticalProfiles:screens.tickets.valid')
              : t('verticalProfiles:screens.tickets.invalid')}
          </Text>
          <Text style={styles.meta}>{ticket.displayCode}</Text>
          <Text style={styles.meta}>{ticket.status}</Text>
          {ticket.canRedeem ? (
            <Pressable
              style={styles.redeem}
              onPress={() => void runRedeem()}
              disabled={busy}
              accessibilityRole="button"
              accessibilityLabel={t('verticalProfiles:screens.tickets.redeem')}>
              <Text style={styles.redeemText}>{t('verticalProfiles:screens.tickets.redeem')}</Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}

      <BarcodeScannerModal
        visible={scannerOpen}
        title={t('verticalProfiles:screens.tickets.scan')}
        hint={t('verticalProfiles:screens.tickets.scanHint')}
        onClose={() => setScannerOpen(false)}
        onScan={(payload) => {
          setScannerOpen(false);
          void runValidate(payload);
        }}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
    padding: SoftSpacing.lg,
    gap: SoftSpacing.md,
  },
  title: {
    ...SoftTypography.h1,
    color: SoftColors.textPrimary,
  },
  subtitle: {
    ...SoftTypography.body,
    color: SoftColors.textSecondary,
  },
  input: {
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgCard,
  },
  row: {
    flexDirection: 'row',
    gap: SoftSpacing.sm,
  },
  button: {
    flex: 1,
    backgroundColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    alignItems: 'center',
  },
  buttonText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
  buttonSecondary: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    backgroundColor: SoftColors.bgCard,
  },
  buttonSecondaryText: {
    color: SoftColors.textPrimary,
  },
  error: {
    color: SoftColors.error,
  },
  card: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.md,
    padding: SoftSpacing.md,
    gap: SoftSpacing.sm,
  },
  status: {
    ...SoftTypography.h3,
    color: SoftColors.textPrimary,
  },
  meta: {
    color: SoftColors.textSecondary,
  },
  redeem: {
    marginTop: SoftSpacing.sm,
    backgroundColor: SoftColors.accentDark,
    borderRadius: SoftRadius.md,
    paddingVertical: SoftSpacing.sm,
    alignItems: 'center',
  },
  redeemText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
  },
  link: {
    color: SoftColors.accentDark,
  },
});
