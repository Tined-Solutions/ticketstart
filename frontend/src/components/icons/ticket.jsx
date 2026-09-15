import { motion, useAnimation } from "framer-motion";
import { forwardRef, useCallback, useImperativeHandle, useRef } from "react";

import { useReducedMotion } from "../../lib/motion.js";

const TRANSITION = {
  type: "spring",
  stiffness: 300,
  damping: 20,
};

// Left half + perforation dashes slide left together (the tear line follows).
const LEFT_VARIANTS = {
  normal: { x: 0 },
  animate: { x: -3 },
};

// Right half slides right and tilts clockwise, as if coming away.
const RIGHT_VARIANTS = {
  normal: { x: 0, rotate: 0 },
  animate: { x: 3, rotate: 4 },
};

const TicketIcon = forwardRef(
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
          className="overflow-visible"
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
          <motion.g
            animate={controls}
            initial="normal"
            transition={TRANSITION}
            variants={LEFT_VARIANTS}
          >
            <path d="M13 5H4a2 2 0 0 0-2 2v2a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h9" />
            <path d="M13 5v2" />
            <path d="M13 11v2" />
            <path d="M13 17v2" />
          </motion.g>
          <motion.path
            animate={controls}
            d="M13 5h7a2 2 0 0 1 2 2v2a3 3 0 0 0 0 6v2a2 2 0 0 1-2 2h-7"
            initial="normal"
            style={{ transformOrigin: "13px 12px" }}
            transition={TRANSITION}
            variants={RIGHT_VARIANTS}
          />
        </svg>
      </div>
    );
  }
);

TicketIcon.displayName = "TicketIcon";

export { TicketIcon };
