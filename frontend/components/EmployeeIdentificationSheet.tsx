/**
 * POS: Dedicated employee identification flow for attaching a customer (employee)
 * to the current sale. Separate from normal customer selection; used for staff benefits.
 * Lookup by EmployeeNumber and list from GET /api/Employee/list (explicit employee/customer mapping).
 */
import { Ionicons } from '@expo/vector-icons';
import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  FlatList,
  Alert,
  Pressable,
} from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { type Customer } from '../services/api/customerService';
import { employeeService, type EmployeeSummary } from '../services/api/employeeService';
import { WaveLoader } from '../src/components/common/WaveLoader';

interface EmployeeIdentificationSheetProps {
  visible: boolean;
  onClose: () => void;
  onSelect: (customer: Customer) => void;
}

export default function EmployeeIdentificationSheet({
  visible,
  onClose,
  onSelect,
}: EmployeeIdentificationSheetProps) {
  const { t } = useTranslation(['employees']);
  const [employeeNumber, setEmployeeNumber] = useState('');
  const [lookupLoading, setLookupLoading] = useState(false);
  const [showList, setShowList] = useState(false);
  const [listEmployees, setListEmployees] = useState<EmployeeSummary[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listFilter, setListFilter] = useState('');

  const loadList = useCallback(async () => {
    setListLoading(true);
    try {
      const arr = await employeeService.getAllEmployees();
      setListEmployees(arr);
    } catch (e) {
      console.warn('[EmployeeIdentificationSheet] Failed to load employee list:', e);
      Alert.alert(
        t('employees:selectionSheet.alertListFailedTitle'),
        t('employees:selectionSheet.alertListFailedMessage')
      );
      setListEmployees([]);
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (visible && showList && listEmployees.length === 0 && !listLoading) {
      loadList();
    }
  }, [visible, showList, listLoading, listEmployees.length, loadList]);

  const handleSearchByNumber = useCallback(async () => {
    const trimmed = employeeNumber.trim();
    if (!trimmed) return;
    setLookupLoading(true);
    try {
      const customer = await employeeService.getByEmployeeNumber(trimmed);
      if (customer) {
        onSelect(customer);
        onClose();
      } else {
        Alert.alert(
          t('employees:selectionSheet.alertNotFoundTitle'),
          t('employees:selectionSheet.alertNotFoundMessage')
        );
      }
    } catch (e) {
      console.warn('[EmployeeIdentificationSheet] Lookup failed:', e);
      Alert.alert(
        t('employees:selectionSheet.alertSearchFailedTitle'),
        t('employees:selectionSheet.alertSearchFailedMessage')
      );
    } finally {
      setLookupLoading(false);
    }
  }, [employeeNumber, onSelect, onClose, t]);

  const filteredList = listFilter.trim()
    ? listEmployees.filter(
        (e) =>
          e.name.toLowerCase().includes(listFilter.toLowerCase()) ||
          (e.employeeNumber && e.employeeNumber.includes(listFilter))
      )
    : listEmployees;

  const handleSelectEmployee = useCallback(
    (employee: EmployeeSummary) => {
      onSelect({
        id: employee.customerId,
        name: employee.name,
        customerNumber: employee.employeeNumber,
      } as Customer);
      onClose();
    },
    [onSelect, onClose]
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}>
      <View style={styles.container}>
        <View style={styles.header}>
          <TouchableOpacity
            onPress={onClose}
            style={styles.closeBtn}
            accessibilityLabel={t('employees:selectionSheet.closeA11y')}>
            <Ionicons name="close" size={24} color={SoftColors.textPrimary} />
          </TouchableOpacity>
          <Text style={styles.title}>{t('employees:selectionSheet.title')}</Text>
          <View style={styles.placeholder} />
        </View>

        <View style={styles.section}>
          <Text style={styles.label}>{t('employees:selectionSheet.employeeNumberLabel')}</Text>
          <View style={styles.row}>
            <TextInput
              style={styles.input}
              placeholder={t('employees:selectionSheet.numberPlaceholder')}
              placeholderTextColor={SoftColors.textMuted}
              value={employeeNumber}
              onChangeText={setEmployeeNumber}
              editable={!lookupLoading}
              autoCapitalize="none"
              autoCorrect={false}
            />
            <Pressable
              style={[styles.primaryBtn, lookupLoading && styles.btnDisabled]}
              onPress={handleSearchByNumber}
              disabled={lookupLoading || !employeeNumber.trim()}>
              {lookupLoading ? (
                <WaveLoader size={18} color={SoftColors.textInverse} />
              ) : (
                <Text style={styles.primaryBtnText}>{t('employees:selectionSheet.search')}</Text>
              )}
            </Pressable>
          </View>
          <Text style={styles.hint}>{t('employees:selectionSheet.benefitsHint')}</Text>
        </View>

        <View style={styles.divider}>
          <View style={styles.line} />
          <Text style={styles.dividerText}>{t('employees:selectionSheet.or')}</Text>
          <View style={styles.line} />
        </View>

        {!showList ? (
          <Pressable
            style={styles.secondaryBtn}
            onPress={() => {
              setShowList(true);
            }}>
            <Ionicons name="list" size={20} color={SoftColors.accent} />
            <Text style={styles.secondaryBtnText}>
              {t('employees:selectionSheet.chooseFromList')}
            </Text>
          </Pressable>
        ) : (
          <View style={styles.listSection}>
            <View style={styles.listHeader}>
              <TextInput
                style={styles.filterInput}
                placeholder={t('employees:selectionSheet.filterPlaceholder')}
                placeholderTextColor={SoftColors.textMuted}
                value={listFilter}
                onChangeText={setListFilter}
              />
              <TouchableOpacity
                onPress={() => {
                  setShowList(false);
                }}
                style={styles.backToListBtn}>
                <Text style={styles.backToListText}>{t('employees:selectionSheet.back')}</Text>
              </TouchableOpacity>
            </View>
            {listLoading ? (
              <View style={styles.loadingBox}>
                <WaveLoader size={28} color={SoftColors.accent} />
                <Text style={styles.loadingText}>{t('employees:selectionSheet.loadingList')}</Text>
              </View>
            ) : (
              <FlatList
                data={filteredList}
                keyExtractor={(item) => item.userId}
                style={styles.flatList}
                renderItem={({ item }) => (
                  <Pressable
                    style={styles.listItem}
                    onPress={() => {
                      handleSelectEmployee(item);
                    }}>
                    <Text style={styles.listItemName}>{item.name}</Text>
                    {item.employeeNumber ? (
                      <Text style={styles.listItemNumber}>
                        {t('employees:selectionSheet.numberPrefix')} {item.employeeNumber}
                      </Text>
                    ) : null}
                  </Pressable>
                )}
                ListEmptyComponent={
                  <View style={styles.emptyBox}>
                    <Text style={styles.emptyText}>{t('employees:selectionSheet.emptyList')}</Text>
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
