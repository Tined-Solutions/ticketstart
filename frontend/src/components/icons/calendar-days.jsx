import { AnimatePresence, motion, useAnimation } from "framer-motion";
import { forwardRef, useCallback, useImperativeHandle, useRef } from "react";

import { useReducedMotion } from "../../lib/motion.js";

const DOTS = [
  { cx: 8, cy: 14 },
  { cx: 12, cy: 14 },
  { cx: 16, cy: 14 },
  { cx: 8, cy: 18 },
  { cx: 12, cy: 18 },
  { cx: 16, cy: 18 },
];

const VARIANTS = {
  normal: {
    opacity: 1,
    transition: {
      duration: 0.2,
    },
  },
  animate: (i) => ({
    opacity: [1, 0.3, 1],
    transition: {
      delay: i * 0.1,
      duration: 0.4,
      times: [0, 0.5, 1],
    },
  }),
};

const CalendarDaysIcon = forwardRef(
  ({ onMouseEnter, onMouseLeave, className, size = 28, ...props }, ref) => {
    const controls = useAnimation();
    const isControlledRef = useRef(false);
    // Respect prefers-reduced-motion (repo motion discipline).
    const shouldReduceMotion = useReducedMotion();

    useImperativeHandle(ref, () => {
      isControlledRef.current = true;
      return {
        startAnimation: () => {
          if (!shouldReduceMotion) controls.start("animate");
        },
        stopAnimation: () => controls.start("normal"),
      };
    });

    const handleMouseEnter = useCallback(
      (e) => {
        if (isControlledRef.current) {
          onMouseEnter?.(e);
        } else if (!shouldReduceMotion) {
          controls.start("animate");
        }
      },
      [controls, onMouseEnter, shouldReduceMotion]
    );

    const handleMouseLeave = useCallback(
      (e) => {
        if (isControlledRef.current) {
          onMouseLeave?.(e);
        } else {
          controls.start("normal");
        }
      },
      [controls, onMouseLeave]
    );

    return (
      <div
        className={className}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        {...props}
      >
        <svg
          aria-hidden="true"
          fill="none"
          height={size}
          stroke="currentColor"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="2"
          viewBox="0 0 24 24"
          width={size}
          xmlns="http://www.w3.org/2000/svg"
        >
          <path d="M8 2v4" />
          <path d="M16 2v4" />
          <rect height="18" rx="2" width="18" x="3" y="4" />
          <path d="M3 10h18" />
          <AnimatePresence>
            {DOTS.map((dot, index) => (
              <motion.circle
                animate={controls}
                custom={index}
                cx={dot.cx}
                cy={dot.cy}
                fill="currentColor"
                initial="normal"
                key={`${dot.cx}-${dot.cy}`}
                r="1"
                stroke="none"
                variants={VARIANTS}
              />
            ))}
          </AnimatePresence>
        </svg>
      </div>
    );
  }
);

CalendarDaysIcon.displayName = "CalendarDaysIcon";

export { CalendarDaysIcon };
