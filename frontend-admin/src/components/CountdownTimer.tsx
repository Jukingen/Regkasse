'use client';

import { Typography } from 'antd';
import { useEffect, useRef, useState } from 'react';

type Props = {
  seconds: number;
  onComplete: () => void;
  active: boolean;
};

export function CountdownTimer({ seconds, onComplete, active }: Props) {
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
    const id = window.setInterval(() => {
      setRemaining((r) => {
        if (r <= 1) {
          window.clearInterval(id);
          if (!completedRef.current) {
            completedRef.current = true;
            onCompleteRef.current();
          }
          return 0;
        }
        return r - 1;
      });
    }, 1000);
    return () => window.clearInterval(id);
  }, [active, remaining]);

  if (!active) return null;

  return (
    <Typography.Title level={2} style={{ margin: '12px 0', textAlign: 'center' }}>
      {remaining}s
    </Typography.Title>
  );
}
