'use client';

import { StyleProvider, createCache, extractStyle } from '@ant-design/cssinjs/lib';
import type Entity from '@ant-design/cssinjs/lib/Cache';
import { useServerInsertedHTML } from 'next/navigation';
import React, { PropsWithChildren, useMemo } from 'react';

const StyledComponentsRegistry = ({ children }: PropsWithChildren) => {
  const cache = useMemo<Entity>(() => createCache(), []);
  useServerInsertedHTML(() => (
    <style id="antd" dangerouslySetInnerHTML={{ __html: extractStyle(cache, true) }} />
  ));
  return <StyleProvider cache={cache}>{children}</StyleProvider>;
};

export default StyledComponentsRegistry;
