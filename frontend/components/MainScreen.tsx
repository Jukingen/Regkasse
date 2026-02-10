// Bu komponent, 1-9 arası numaralı butonlarla hızlı masa/satış seçimi yapılmasını sağlar. Her buton bir masa/satış slotunu temsil eder. Dokunmatik uyumlu ve sade bir ana ekrandır.
import React, { useContext } from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { TableSlotContext } from '../contexts/TableSlotContext';
import { useTranslation } from 'react-i18next';

const SLOT_COUNT = 9;

const MainScreen = () => {
  const { activeSlot, setActiveSlot, slots } = useContext(TableSlotContext);
  const { t } = useTranslation();

  return (
    <View style={styles.container}>
      <Text style={styles.title}>{t('mainScreen.title', 'Masa/Satış Seçimi')}</Text>
      <View style={styles.grid}>
        {[...Array(SLOT_COUNT)].map((_, i) => {
          const slotNumber = i + 1;
          const isActive = activeSlot === slotNumber;
          return (
            <TouchableOpacity
              key={slotNumber}
              style={[styles.button, isActive && styles.activeButton]}
              onPress={() => setActiveSlot(slotNumber)}
            >
              <Text style={styles.buttonText}>{slotNumber}</Text>
              {slots[slotNumber]?.isOpen && (
                <Text style={styles.openText}>{t('mainScreen.open', 'Açık')}</Text>
              )}
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#fff',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 32,
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    width: 320,
    justifyContent: 'center',
  },
  button: {
    width: 90,
    height: 90,
    margin: 10,
    borderRadius: 16,
    backgroundColor: '#e0e0e0',
    justifyContent: 'center',
    alignItems: 'center',
    elevation: 2,
  },
  activeButton: {
    backgroundColor: '#1976d2',
  },
  buttonText: {
    fontSize: 32,
    color: '#222',
    fontWeight: 'bold',
  },
  openText: {
    fontSize: 14,
    color: '#388e3c',
    marginTop: 4,
  },
});

export default MainScreen; 