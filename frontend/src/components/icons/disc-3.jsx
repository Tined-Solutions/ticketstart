import { motion, useAnimation } from "framer-motion";
import { forwardRef, useCallback, useImperativeHandle, useRef } from "react";

import { useReducedMotion } from "../../lib/motion.js";

const Disc3Icon = forwardRef(
  ({ onMouseEnter, onMouseLeave, className, size = 28, ...props }, ref) => {
    const controls = useAnimation();
    const isControlledRef = useRef(false);
    const shouldReduceMotion = useReducedMotion();

    useImperativeHandle(ref, () => {
      isControlledRef.current = true;
      return {
        startAnimation: () => {
          // Respect prefers-reduced-motion for imperative triggers.
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
          // Respect prefers-reduced-motion: skip the hover animation.
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
          <circle cx="12" cy="12" r="10" />
          <circle cx="12" cy="12" r="2" />

          <motion.g
            animate={controls}
            style={{ transformOrigin: "12px 12px" }}
            transition={{ duration: 0.4, ease: "easeInOut" }}
            variants={{
              normal: { rotate: 0 },
              animate: { rotate: 90 },
            }}
          >
            <path d="M6 12c0-1.7.7-3.2 1.8-4.2" />
            <path d="M18 12c0 1.7-.7 3.2-1.8 4.2" />
          </motion.g>
        </svg>
      </div>
    );
  }
);

Disc3Icon.displayName = "Disc3Icon";

export { Disc3Icon };
