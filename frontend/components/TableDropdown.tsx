// Bu komponent, açık olan masalar/satışlar arasında geçiş için dropdown ve aç/kapat düğmesi sunar.
import React, { useContext } from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { TableSlotContext } from '../contexts/TableSlotContext';

const TableDropdown = () => {
  const { slots, activeSlot, setActiveSlot, openSlot, closeSlot } = useContext(TableSlotContext);
  const openSlots = Object.entries(slots).filter(([_, slot]) => slot.isOpen);

  return (
    <View style={styles.container}>
      <Text style={styles.label}>Açık Masalar/Satışlar:</Text>
      <View style={styles.dropdownRow}>
        {openSlots.length === 0 ? (
          <Text style={styles.emptyText}>Açık masa yok</Text>
        ) : (
          openSlots.map(([slotNumber]) => (
            <TouchableOpacity
              key={slotNumber}
              style={[
                styles.dropdownButton,
                Number(slotNumber) === activeSlot && styles.activeDropdownButton,
              ]}
              onPress={() => setActiveSlot(Number(slotNumber))}
            >
              <Text style={styles.dropdownButtonText}>{slotNumber}</Text>
            </TouchableOpacity>
          ))
        )}
      </View>
      <View style={styles.actionRow}>
        {!slots[activeSlot].isOpen ? (
          <TouchableOpacity style={styles.openBtn} onPress={() => openSlot(activeSlot)}>
            <Text style={styles.actionText}>Aç</Text>
          </TouchableOpacity>
        ) : (
          <TouchableOpacity style={styles.closeBtn} onPress={() => closeSlot(activeSlot)}>
            <Text style={styles.actionText}>Kapat</Text>
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: 16,
    alignItems: 'center',
  },
  label: {
    fontSize: 18,
    marginBottom: 8,
  },
  dropdownRow: {
    flexDirection: 'row',
    marginBottom: 8,
  },
  dropdownButton: {
    backgroundColor: '#e0e0e0',
    padding: 12,
    borderRadius: 8,
    marginHorizontal: 4,
  },
  activeDropdownButton: {
    backgroundColor: '#1976d2',
  },
  dropdownButtonText: {
    color: '#222',
    fontSize: 18,
    fontWeight: 'bold',
  },
  emptyText: {
    color: '#888',
    fontSize: 16,
  },
  actionRow: {
    flexDirection: 'row',
    marginTop: 8,
  },
  openBtn: {
    backgroundColor: '#388e3c',
    padding: 10,
    borderRadius: 8,
    marginHorizontal: 4,
  },
  closeBtn: {
    backgroundColor: '#d32f2f',
    padding: 10,
    borderRadius: 8,
    marginHorizontal: 4,
  },
  actionText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 16,
  },
});

export default TableDropdown; 