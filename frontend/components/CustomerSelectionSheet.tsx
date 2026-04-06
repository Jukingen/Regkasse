import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  FlatList,
  ActivityIndicator,
  Alert,
  Pressable,
} from 'react-native';
import { customerService, type Customer } from '../services/api/customerService';
import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';

interface CustomerSelectionSheetProps {
  visible: boolean;
  onClose: () => void;
  onSelect: (customer: Customer) => void;
}

export default function CustomerSelectionSheet({
  visible,
  onClose,
  onSelect,
}: CustomerSelectionSheetProps) {
  const [customerNumber, setCustomerNumber] = useState('');
  const [lookupLoading, setLookupLoading] = useState(false);
  const [showList, setShowList] = useState(false);
  const [listCustomers, setListCustomers] = useState<Customer[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listFilter, setListFilter] = useState('');

  const loadList = useCallback(async () => {
    setListLoading(true);
    try {
      const arr = await customerService.getAll();
      setListCustomers(arr);
    } catch (e) {
      console.warn('[CustomerSelectionSheet] Failed to load customer list:', e);
      Alert.alert('Hinweis', 'Kundenliste konnte nicht geladen werden.');
      setListCustomers([]);
    } finally {
      setListLoading(false);
    }
  }, []);

  useEffect(() => {
    if (visible && showList && listCustomers.length === 0 && !listLoading) {
      loadList();
    }
  }, [visible, showList, listLoading, listCustomers.length, loadList]);

  const handleSearchByNumber = useCallback(async () => {
    const trimmed = customerNumber.trim();
    if (!trimmed) return;
    setLookupLoading(true);
    try {
      const customer = await customerService.getByCustomerNumber(trimmed);
      if (customer) {
        onSelect(customer);
        onClose();
      } else {
        Alert.alert('Nicht gefunden', 'Kein Kunde mit dieser Nummer gefunden.');
      }
    } catch (e) {
      console.warn('[CustomerSelectionSheet] Lookup failed:', e);
      Alert.alert('Fehler', 'Suche fehlgeschlagen. Bitte erneut versuchen.');
    } finally {
      setLookupLoading(false);
    }
  }, [customerNumber, onSelect, onClose]);

  const filteredList = listFilter.trim()
    ? listCustomers.filter(
        (e) =>
          e.name.toLowerCase().includes(listFilter.toLowerCase()) ||
          (e.customerNumber && e.customerNumber.includes(listFilter))
      )
    : listCustomers;

  const handleSelectCustomer = useCallback(
    (customer: Customer) => {
      onSelect(customer);
      onClose();
    },
    [onSelect, onClose]
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <TouchableOpacity onPress={onClose} style={styles.closeBtn} accessibilityLabel="Schließen">
            <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
          </TouchableOpacity>
          <Text style={styles.title}>Kunde</Text>
          <View style={styles.placeholder} />
        </View>

        <View style={styles.section}>
          <Text style={styles.label}>Kunden-Nr.</Text>
          <View style={styles.row}>
            <TextInput
              style={styles.input}
              placeholder="Nummer eingeben"
              placeholderTextColor={SoftColors.textMuted}
              value={customerNumber}
              onChangeText={setCustomerNumber}
              editable={!lookupLoading}
              autoCapitalize="none"
              autoCorrect={false}
            />
            <Pressable
              style={[styles.primaryBtn, lookupLoading && styles.btnDisabled]}
              onPress={handleSearchByNumber}
              disabled={lookupLoading || !customerNumber.trim()}
            >
              {lookupLoading ? (
                <ActivityIndicator size="small" color={SoftColors.textInverse} />
              ) : (
                <Text style={styles.primaryBtnText}>Suchen</Text>
              )}
            </Pressable>
          </View>
          <Text style={styles.hint}>Vorteile werden bei Zahlung angewendet.</Text>
        </View>

        <View style={styles.divider}>
          <View style={styles.line} />
          <Text style={styles.dividerText}>oder</Text>
          <View style={styles.line} />
        </View>

        {!showList ? (
          <Pressable style={styles.secondaryBtn} onPress={() => setShowList(true)}>
            <Ionicons name="list" size={20} color={SoftColors.accent} />
            <Text style={styles.secondaryBtnText}>Aus Liste wählen</Text>
          </Pressable>
        ) : (
          <View style={styles.listSection}>
            <View style={styles.listHeader}>
              <TextInput
                style={styles.filterInput}
                placeholder="Name oder Nr. filtern"
                placeholderTextColor={SoftColors.textMuted}
                value={listFilter}
                onChangeText={setListFilter}
              />
              <TouchableOpacity onPress={() => setShowList(false)} style={styles.backToListBtn}>
                <Text style={styles.backToListText}>Zurück</Text>
              </TouchableOpacity>
            </View>
            {listLoading ? (
              <View style={styles.loadingBox}>
                <ActivityIndicator size="large" color={SoftColors.accent} />
                <Text style={styles.loadingText}>Lade Liste…</Text>
              </View>
            ) : (
              <FlatList
                data={filteredList}
                keyExtractor={(item) => item.id}
                style={styles.flatList}
                renderItem={({ item }) => (
                  <Pressable
                    style={styles.listItem}
                    onPress={() => handleSelectCustomer(item)}
                  >
                    <Text style={styles.listItemName}>{item.name}</Text>
                    {item.customerNumber ? (
                      <Text style={styles.listItemNumber}>Nr. {item.customerNumber}</Text>
                    ) : null}
                  </Pressable>
                )}
                ListEmptyComponent={
                  <View style={styles.emptyBox}>
                    <Text style={styles.emptyText}>Keine Einträge</Text>
                  </View>
                }
              />
            )}
          </View>
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: SoftColors.bgPrimary,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.border,
  },
  closeBtn: { padding: SoftSpacing.xs },
  title: {
    ...SoftTypography.h2,
    color: SoftColors.textPrimary,
  },
  placeholder: { width: 40 },
  section: {
    padding: SoftSpacing.md,
  },
  label: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginBottom: SoftSpacing.xs,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
  },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    fontSize: 16,
    color: SoftColors.textPrimary,
  },
  primaryBtn: {
    backgroundColor: SoftColors.accent,
    paddingHorizontal: SoftSpacing.lg,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    minWidth: 88,
    alignItems: 'center',
  },
  btnDisabled: { opacity: 0.6 },
  primaryBtnText: {
    color: SoftColors.textInverse,
    fontWeight: '600',
    fontSize: 15,
  },
  hint: {
    ...SoftTypography.caption,
    color: SoftColors.textMuted,
    marginTop: SoftSpacing.sm,
  },
  divider: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    marginVertical: SoftSpacing.sm,
  },
  line: { flex: 1, height: 1, backgroundColor: SoftColors.border },
  dividerText: {
    paddingHorizontal: SoftSpacing.md,
    color: SoftColors.textMuted,
    fontSize: 13,
  },
  secondaryBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.sm,
    padding: SoftSpacing.md,
    marginHorizontal: SoftSpacing.md,
    borderWidth: 1,
    borderColor: SoftColors.accent,
    borderRadius: SoftRadius.md,
  },
  secondaryBtnText: {
    color: SoftColors.accent,
    fontWeight: '600',
    fontSize: 15,
  },
  listSection: { flex: 1, paddingHorizontal: SoftSpacing.md },
  listHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    marginBottom: SoftSpacing.sm,
  },
  filterInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    fontSize: 15,
    color: SoftColors.textPrimary,
  },
  backToListBtn: { padding: SoftSpacing.sm },
  backToListText: { color: SoftColors.accent, fontWeight: '600' },
  loadingBox: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: SoftSpacing.xl,
  },
  loadingText: { marginTop: SoftSpacing.sm, color: SoftColors.textMuted },
  flatList: { flex: 1 },
  listItem: {
    padding: SoftSpacing.md,
    borderBottomWidth: 1,
    borderBottomColor: SoftColors.border,
  },
  listItemName: { fontSize: 16, color: SoftColors.textPrimary, fontWeight: '500' },
  listItemNumber: { fontSize: 13, color: SoftColors.textMuted, marginTop: 2 },
  emptyBox: { padding: SoftSpacing.xl, alignItems: 'center' },
  emptyText: { color: SoftColors.textMuted },
});
