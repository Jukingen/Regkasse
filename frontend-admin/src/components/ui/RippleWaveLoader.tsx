"use client";

import { motion } from "framer-motion";

const BAR_INDICES = [0, 1, 2, 3, 4, 5, 6] as const;

/**
 * Seven-bar ripple wave loading indicator (framer-motion keyframe animation).
 */
export function RippleWaveLoader() {
  return (
    <div
      className="inline-flex items-center gap-1"
      role="status"
      aria-live="polite"
      aria-label="Loading"
    >
      {BAR_INDICES.map((index) => (
        <motion.div
          key={index}
          className="h-8 w-2 rounded-full bg-red-500"
          animate={{
            scaleY: [0.5, 1.5, 0.5],
            scaleX: [1, 0.8, 1],
            y: ["0%", "-15%", "0%"],
          }}
          transition={{
            duration: 1,
            repeat: Infinity,
            ease: "easeInOut",
            delay: index * 0.1,
          }}
        />
      ))}
    </div>
  );
}
