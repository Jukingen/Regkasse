import React, { useEffect, useRef, useState } from 'react';
import { StyleSheet, Text } from 'react-native';

type Props = {
  seconds: number;
  active: boolean;
  onComplete: () => void;
};

export function CountdownTimer({ seconds, active, onComplete }: Props) {
  const [remaining, setRemaining] = useState(seconds);
  const onCompleteRef = useRef(onComplete);
  const completedRef = useRef(false);

  useEffect(() => {
    onCompleteRef.current = onComplete;
  }, [onComplete]);

  useEffect(() => {
    completedRef.current = false;
    if (!active) {
      setRemaining(seconds);
      return;
    }
    setRemaining(seconds);
  }, [active, seconds]);

  useEffect(() => {
    if (!active || remaining <= 0) return;

    const id = setInterval(() => {
      setRemaining((r) => {
        if (r <= 1) {
          clearInterval(id);
          if (!completedRef.current) {
            completedRef.current = true;
            onCompleteRef.current();
          }
          return 0;
        }
        return r - 1;
      });
    }, 1000);

    return () => {
      clearInterval(id);
    };
  }, [active, remaining]);

  if (!active) return null;

  return <Text style={styles.countdown}>{remaining}s</Text>;
}

const styles = StyleSheet.create({
  countdown: {
    fontSize: 28,
    fontWeight: '700',
    textAlign: 'center',
    marginVertical: 12,
    color: '#1a1a1a',
  },
});
