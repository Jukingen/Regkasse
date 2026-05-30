import dayjs, { type Dayjs } from 'dayjs';

export type PitrDateTimeConstraints = {
  disabledDate: (current: Dayjs) => boolean;
  disabledTime: (current: Dayjs | null) => {
    disabledHours?: () => number[];
    disabledMinutes?: (hour: number) => number[];
    disabledSeconds?: (hour: number, minute: number) => number[];
  };
};

/** Restrict DatePicker to the PITR window (day + hour/minute/second on boundary days). */
export function buildPitrDateTimeConstraints(
  earliestUtc: string | null | undefined,
  latestUtc: string | null | undefined,
): PitrDateTimeConstraints {
  if (!earliestUtc || !latestUtc) {
    return {
      disabledDate: () => false,
      disabledTime: () => ({}),
    };
  }

  const earliest = dayjs(earliestUtc);
  const latest = dayjs(latestUtc);

  const disabledDate = (current: Dayjs) =>
    current.isBefore(earliest, 'day') || current.isAfter(latest, 'day');

  const disabledTime = (current: Dayjs | null) => {
    if (!current) return {};

    const disabledHours = () => {
      const hours: number[] = [];
      if (current.isSame(earliest, 'day')) {
        for (let h = 0; h < earliest.hour(); h++) hours.push(h);
      }
      if (current.isSame(latest, 'day')) {
        for (let h = latest.hour() + 1; h < 24; h++) hours.push(h);
      }
      return hours;
    };

    const disabledMinutes = (selectedHour: number) => {
      const minutes: number[] = [];
      if (current.isSame(earliest, 'day') && selectedHour === earliest.hour()) {
        for (let m = 0; m < earliest.minute(); m++) minutes.push(m);
      }
      if (current.isSame(latest, 'day') && selectedHour === latest.hour()) {
        for (let m = latest.minute() + 1; m < 60; m++) minutes.push(m);
      }
      return minutes;
    };

    const disabledSeconds = (selectedHour: number, selectedMinute: number) => {
      const seconds: number[] = [];
      if (
        current.isSame(earliest, 'day') &&
        selectedHour === earliest.hour() &&
        selectedMinute === earliest.minute()
      ) {
        for (let s = 0; s < earliest.second(); s++) seconds.push(s);
      }
      if (
        current.isSame(latest, 'day') &&
        selectedHour === latest.hour() &&
        selectedMinute === latest.minute()
      ) {
        for (let s = latest.second() + 1; s < 60; s++) seconds.push(s);
      }
      return seconds;
    };

    return { disabledHours, disabledMinutes, disabledSeconds };
  };

  return { disabledDate, disabledTime };
}
