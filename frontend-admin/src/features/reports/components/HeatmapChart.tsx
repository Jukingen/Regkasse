'use client';

import React, { useMemo } from 'react';

import { useCalendarWeekdayLabels } from '@/hooks/useCalendarWeekdayLabels';

export type HeatmapChartProps = {
  /** 7×24 grid (Mon=0 … Sun=6). Accepts legacy `cells` or admin `heatmap`. */
  cells?: number[][];
  heatmap?: number[][];
  maxCellCount?: number;
  busiestLabel?: string;
  quietestLabel?: string;
};

export function HeatmapChart({
  cells,
  heatmap,
  maxCellCount: maxProp,
  busiestLabel,
  quietestLabel,
}: HeatmapChartProps) {
  const weekdayLabels = useCalendarWeekdayLabels({ short: true });
  const grid = heatmap ?? cells ?? [];
  const max = useMemo(() => {
    if (maxProp != null && maxProp > 0) return maxProp;
    let m = 0;
    for (const row of grid) {
      for (const v of row) m = Math.max(m, v);
    }
    return m;
  }, [grid, maxProp]);

  return (
    <div>
      {(busiestLabel || quietestLabel) && (
        <p style={{ marginBottom: 8, fontSize: 13, color: 'rgba(0,0,0,0.65)' }}>
          {busiestLabel}
          {busiestLabel && quietestLabel ? ' · ' : null}
          {quietestLabel}
        </p>
      )}
      <div style={{ overflowX: 'auto' }}>
        <table style={{ borderCollapse: 'collapse', fontSize: 11 }}>
          <thead>
            <tr>
              <th />
              {Array.from({ length: 24 }, (_, h) => (
                <th key={h} style={{ padding: 2, minWidth: 22 }}>
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {grid.map((row, dow) => (
              <tr key={dow}>
                <td style={{ padding: 4, fontWeight: 600 }}>{weekdayLabels[dow] ?? ''}</td>
                {row.map((count, hour) => {
                  const intensity = max > 0 ? count / max : 0;
                  return (
                    <td
                      key={hour}
                      title={`${count}`}
                      style={{
                        padding: 2,
                        background: `rgba(22, 119, 255, ${0.08 + intensity * 0.72})`,
                        textAlign: 'center',
                      }}
                    >
                      {count > 0 ? count : ''}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
