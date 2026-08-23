import { motion, useReducedMotion } from "framer-motion";
import { ChevronLeft, ChevronRight, LoaderCircle, X } from "lucide-react";
import type { ReactNode } from "react";
import type { OrderStatus } from "../types/api";
import { easeOutExpo } from "./motion";

export function PageHeader({ eyebrow, title, description, action }: { eyebrow: string; title: string; description: string; action?: ReactNode }) {
  return <div className="page-header"><div className="page-heading"><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{description}</p></div>{action && <div className="page-action">{action}</div>}</div>;
}

export function Modal({ title, description, children, onClose }: { title: string; description?: string; children: ReactNode; onClose: () => void }) {
  const reduced = useReducedMotion();
  return <motion.div className="modal-layer" role="dialog" aria-modal="true" aria-label={title} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: reduced ? 0 : .18 }} onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <motion.div className="modal-card" initial={reduced ? false : { opacity: 0, y: 24, scale: .96 }} animate={{ opacity: 1, y: 0, scale: 1 }} transition={reduced ? { duration: 0 } : { type: "spring", stiffness: 390, damping: 32 }}>
      <div className="modal-head"><div><p className="eyebrow">Kayıt düzenleyici</p><h2>{title}</h2>{description && <p>{description}</p>}</div><motion.button className="icon-btn" type="button" aria-label="Pencereyi kapat" onClick={onClose} whileHover={reduced ? undefined : { rotate: 6, scale: 1.06 }} whileTap={reduced ? undefined : { scale: .92 }}><X size={18} /></motion.button></div>{children}
    </motion.div>
  </motion.div>;
}

export function FormError({ message }: { message?: string }) { return message ? <motion.p className="form-error" initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }}>{message}</motion.p> : null; }

export function SubmitButton({ children, pending }: { children: ReactNode; pending: boolean }) {
  const reduced = useReducedMotion();
  return <motion.button className="btn btn-primary" type="submit" disabled={pending} whileHover={reduced || pending ? undefined : { y: -2 }} whileTap={reduced || pending ? undefined : { scale: .97 }}>{pending && <LoaderCircle className="animate-spin" size={15} />}{children}</motion.button>;
}

export function Pagination({ page, totalPages, totalCount, onPage }: { page: number; totalPages: number; totalCount: number; onPage: (page: number) => void }) {
  if (totalPages <= 1) return <p className="pagination-single">Toplam {totalCount} kayıt</p>;
  return <div className="pagination"><div><strong>{page}</strong><span>/ {totalPages} sayfa, {totalCount} kayıt</span></div><div className="pagination-actions"><button className="icon-btn" aria-label="Önceki sayfa" disabled={page <= 1} onClick={() => onPage(page - 1)}><ChevronLeft size={17} /></button><button className="icon-btn" aria-label="Sonraki sayfa" disabled={page >= totalPages} onClick={() => onPage(page + 1)}><ChevronRight size={17} /></button></div></div>;
}

export function ActiveBadge({ active }: { active: boolean }) { return <span className={`status-badge ${active ? "status-active" : "status-muted"}`}>{active ? "Aktif" : "Pasif"}</span>; }

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const labels: Record<OrderStatus, string> = { Pending: "Bekliyor", Completed: "Tamamlandı", Cancelled: "İptal" };
  return <span className={`status-badge status-${status.toLowerCase()}`}>{labels[status]}</span>;
}

export function TableShell({ children, busy = false }: { children: ReactNode; busy?: boolean }) {
  return <div className="table-shell"><motion.div animate={{ opacity: busy ? .45 : 1, filter: busy ? "blur(2px)" : "blur(0px)" }} transition={{ duration: .2 }} className="table-scroll">{children}</motion.div>{busy && <motion.div className="table-progress" initial={{ scaleX: 0 }} animate={{ scaleX: 1 }} transition={{ duration: .7, repeat: Infinity, repeatType: "reverse", ease: easeOutExpo }} />}</div>;
}

export function CollectionSummary({ items }: { items: Array<{ label: string; value: string | number; detail?: string; tone?: "coral" | "sage" | "lilac" }> }) {
  return <div className="collection-summary">{items.map((item, index) => <motion.div key={item.label} className={`summary-chip summary-${item.tone ?? "coral"}`} initial={{ opacity: 0, x: -8 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: index * .06, ease: easeOutExpo }}><span>{item.label}</span><strong>{item.value}</strong>{item.detail && <small>{item.detail}</small>}</motion.div>)}</div>;
}

export function ChartTooltip({ active, label, payload, valueFormatter }: { active?: boolean; label?: string; payload?: Array<{ name?: string; value?: string | number; color?: string }>; valueFormatter?: (value: number, name?: string) => string }) {
  if (!active || !payload?.length) return null;
  return <motion.div className="chart-tooltip" initial={{ opacity: 0, y: 5, scale: .96 }} animate={{ opacity: 1, y: 0, scale: 1 }} transition={{ type: "spring", stiffness: 450, damping: 32 }}><p>{label}</p>{payload.map((item, index) => <div key={`${item.name}-${index}`}><i style={{ background: item.color }} /><span>{item.name}</span><strong>{valueFormatter ? valueFormatter(Number(item.value), item.name) : item.value}</strong></div>)}</motion.div>;
}
