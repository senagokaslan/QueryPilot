import { animate, motion, useMotionValue, useReducedMotion, useTransform } from "framer-motion";
import { useEffect, type ReactNode } from "react";

export const easeOutExpo = [0.16, 1, 0.3, 1] as const;

export function PageMotion({ children }: { children: ReactNode }) {
  const reduced = useReducedMotion();
  return <motion.div initial={reduced ? false : { opacity: 0, y: 14, filter: "blur(8px)" }} animate={{ opacity: 1, y: 0, filter: "blur(0px)" }} exit={reduced ? undefined : { opacity: 0, y: -8, filter: "blur(5px)" }} transition={{ duration: reduced ? 0 : 0.38, ease: easeOutExpo }}>{children}</motion.div>;
}

export function Reveal({ children, delay = 0, className = "" }: { children: ReactNode; delay?: number; className?: string }) {
  const reduced = useReducedMotion();
  return <motion.div className={className} initial={reduced ? false : { opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: reduced ? 0 : 0.5, delay: reduced ? 0 : delay, ease: easeOutExpo }}>{children}</motion.div>;
}

export function AnimatedNumber({ value, format }: { value: number; format: (value: number) => string }) {
  const reduced = useReducedMotion();
  const number = useMotionValue(reduced ? value : 0);
  const display = useTransform(number, (latest) => format(latest));
  useEffect(() => {
    if (reduced) { number.set(value); return; }
    const controls = animate(number, value, { duration: 0.9, ease: easeOutExpo });
    return () => controls.stop();
  }, [number, reduced, value]);
  return <motion.span>{display}</motion.span>;
}

export function MotionTableBody({ children, refreshKey }: { children: ReactNode; refreshKey?: string | number }) {
  const reduced = useReducedMotion();
  return <motion.tbody key={refreshKey} initial="hidden" animate="show" variants={{ hidden: {}, show: { transition: { staggerChildren: reduced ? 0 : 0.035 } } }}>{children}</motion.tbody>;
}

export function MotionTableRow({ children }: { children: ReactNode }) {
  const reduced = useReducedMotion();
  return <motion.tr variants={{ hidden: reduced ? { opacity: 1 } : { opacity: 0, y: 8 }, show: { opacity: 1, y: 0, transition: { duration: reduced ? 0 : 0.3, ease: easeOutExpo } } }}>{children}</motion.tr>;
}

export function StreamingText({ children }: { children: string }) {
  const reduced = useReducedMotion();
  const words = children.split(" ");
  return <motion.p className="streaming-copy" initial="hidden" animate="show" aria-label={children}>{words.map((word, index) => <motion.span aria-hidden="true" key={`${word}-${index}`} variants={{ hidden: reduced ? { opacity: 1 } : { opacity: 0, filter: "blur(3px)" }, show: { opacity: 1, filter: "blur(0px)" } }} transition={{ duration: reduced ? 0 : 0.16, delay: reduced ? 0 : Math.min(index * 0.025, 0.8) }}>{word}{" "}</motion.span>)}</motion.p>;
}

export function AnalysisPulse() {
  const reduced = useReducedMotion();
  return <div className="analysis-pulse" aria-hidden="true">{[0, 1, 2, 3].map((index) => <motion.span key={index} animate={reduced ? undefined : { scaleY: [0.35, 1, 0.35], opacity: [0.45, 1, 0.45] }} transition={{ duration: 0.9, repeat: Infinity, delay: index * 0.1, ease: "easeInOut" }} />)}</div>;
}
