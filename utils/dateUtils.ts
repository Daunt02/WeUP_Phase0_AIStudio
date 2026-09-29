
/**
 * WEUP TEMPORAL UTILITIES
 * Locked to Houston Time (Central Standard Time)
 */

export const getHoustonDate = (): Date => {
  // Force Houston time calculation
  const now = new Date();
  const houstonTime = now.toLocaleString("en-US", { timeZone: "America/Chicago" });
  return new Date(houstonTime);
};

export const formatHoustonDateLabel = (date: Date): string => {
  const formatter = new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    timeZone: 'America/Chicago'
  });
  const parts = formatter.formatToParts(date);
  const month = parts.find(p => p.type === 'month')?.value.toUpperCase();
  const day = parts.find(p => p.type === 'day')?.value;
  return `${month} ${day}`;
};

export const getUpcomingWeek = () => {
  const today = getHoustonDate();
  const week = [];
  const days = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'];

  for (let i = 0; i < 7; i++) {
    const next = new Date(today);
    next.setDate(today.getDate() + i);
    week.push({
      day: days[next.getDay()],
      date: formatHoustonDateLabel(next)
    });
  }
  return week;
};

export const getCurrentTimeBucket = (): string => {
  const houstonDate = getHoustonDate();
  const hour = houstonDate.getHours();

  if (hour >= 5 && hour < 12) return 'MORNING';
  if (hour >= 12 && hour < 17) return 'DAY';
  if (hour >= 17 && hour < 21) return 'EVENING';
  if (hour >= 21 || hour < 2) return 'NIGHT';
  return 'LATE';
};
