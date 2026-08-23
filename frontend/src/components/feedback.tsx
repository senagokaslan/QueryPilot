import { motion, useReducedMotion } from "framer-motion";
import { AlertTriangle, RefreshCw } from "lucide-react";

export function LoadingState({ label = "Veriler hazırlanıyor" }: { label?: string }) {
  const reduced = useReducedMotion();
  return <div className="loading-state" role="status"><motion.div className="loading-indicator" animate={reduced ? undefined : { scaleX: [.35, 1, .35] }} transition={{ duration: 1.2, repeat: Infinity, ease: "easeInOut" }} /><div><strong>{label}</strong><p>Güncel kayıtlar işleniyor.</p></div></div>;
}

export function EmptyState({ title = "Henüz veri yok", description = "Seçtiğiniz ölçütlere uygun kayıt bulunamadı." }: { title?: string; description?: string }) {
  return <motion.div className="empty-state" initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}><div><strong>{title}</strong><p>{description}</p></div></motion.div>;
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return <motion.div className="error-state" initial={{ opacity: 0, scale: .98 }} animate={{ opacity: 1, scale: 1 }}><div className="error-icon"><AlertTriangle size={21} /></div><div><strong>Veri akışı kesildi</strong><p>{message}</p></div>{onRetry && <button className="btn btn-secondary" onClick={onRetry}><RefreshCw size={15} />Yeniden dene</button>}</motion.div>;
}
