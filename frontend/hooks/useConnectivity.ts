import { useEffect, useRef, useState } from 'react';
import NetInfo from '@react-native-community/netinfo';
import { Alert } from 'react-native';

import { isDevSimulatePosNetworkOffline } from '../constants/devSimulatePosOffline';

export type UseConnectivityOptions = {
  /** When true, treat device as online for POS offline UX (development-mode server flag). */
  forceOnline?: boolean;
};

/**
 * Subscribes to connectivity changes and surfaces German operator alerts for offline/online transitions.
 */
export const useConnectivity = (options?: UseConnectivityOptions) => {
  const forceOnline = options?.forceOnline === true;
  const [isConnected, setIsConnected] = useState(true);
  const [wasOffline, setWasOffline] = useState(false);
  const wasOfflineRef = useRef(false);
  const forceOnlineRef = useRef(forceOnline);
  forceOnlineRef.current = forceOnline;

  useEffect(() => {
    const unsubscribe = NetInfo.addEventListener((state) => {
      const connectedRaw = state.isConnected ?? false;
      const connected =
        forceOnlineRef.current || (connectedRaw && !isDevSimulatePosNetworkOffline());

      if (!connected && !wasOfflineRef.current) {
        Alert.alert(
          'Offline-Modus',
          'Keine Verbindung zum Server. Gutscheine können nicht eingelöst werden. Zahlungen werden in Warteschlange gestellt.',
          [{ text: 'Verstanden' }]
        );
        wasOfflineRef.current = true;
        setWasOffline(true);
      } else if (connected && wasOfflineRef.current) {
        Alert.alert('Online-Modus wiederhergestellt', 'Ausstehende Zahlungen werden jetzt verarbeitet.', [{ text: 'OK' }]);
        wasOfflineRef.current = false;
        setWasOffline(false);
      }

      setIsConnected(connected);
    });

    return () => unsubscribe();
  }, []);

  useEffect(() => {
    if (forceOnline) {
      wasOfflineRef.current = false;
      setWasOffline(false);
      setIsConnected(true);
    }
  }, [forceOnline]);

  return { isConnected, wasOffline };
};
