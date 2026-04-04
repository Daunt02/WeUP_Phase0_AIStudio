/**
 * Temporal Debug Panel — P21
 * Displays temporal query results and time window boundaries
 * Only visible in development mode
 */

import React from 'react';

interface TemporalDebugPanelProps {
  preset?: string;
  presetLabel?: string;
  timeWindowStart?: string;
  timeWindowEnd?: string;
  timezone?: string;
  eventCount?: number;
  loading?: boolean;
}

export default function TemporalDebugPanel({
  preset,
  presetLabel,
  timeWindowStart,
  timeWindowEnd,
  timezone,
  eventCount,
  loading,
}: TemporalDebugPanelProps) {
  // Only render in development
  if (process.env.NODE_ENV !== 'development') {
    return null;
  }

  if (!preset) {
    return null;
  }

  return (
    <div className="absolute bottom-32 left-4 bg-gray-900 bg-opacity-90 text-white p-3 rounded-lg text-xs max-w-xs shadow-lg z-40">
      <div className="font-bold mb-2 text-blue-300">Temporal Query Debug</div>

      <div className="space-y-1">
        <div>
          <span className="text-gray-400">Preset:</span> <span className="font-mono">{presetLabel || preset}</span>
        </div>

        {timeWindowStart && (
          <div>
            <span className="text-gray-400">Window Start:</span>
            <br />
            <span className="font-mono text-green-300 text-xxs">
              {new Date(timeWindowStart).toLocaleString()}
            </span>
          </div>
        )}

        {timeWindowEnd && (
          <div>
            <span className="text-gray-400">Window End:</span>
            <br />
            <span className="font-mono text-green-300 text-xxs">
              {new Date(timeWindowEnd).toLocaleString()}
            </span>
          </div>
        )}

        {timezone && (
          <div>
            <span className="text-gray-400">Timezone:</span> <span className="font-mono">{timezone}</span>
          </div>
        )}

        {eventCount !== undefined && (
          <div>
            <span className="text-gray-400">Events:</span> <span className="font-mono text-yellow-300">{eventCount}</span>
          </div>
        )}

        {loading && (
          <div className="text-yellow-300">⏳ Loading...</div>
        )}
      </div>
    </div>
  );
}
