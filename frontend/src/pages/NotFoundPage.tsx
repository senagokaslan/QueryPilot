import { motion, useReducedMotion } from "framer-motion";
import { ArrowLeft } from "lucide-react";
import { Link } from "react-router-dom";

export function NotFoundPage() { const reduced = useReducedMotion(); return <div className="grid min-h-[70vh] place-items-center text-center"><motion.div className="relative" initial={reduced ? false : { opacity: 0, scale: .94 }} animate={{ opacity: 1, scale: 1 }}><strong className="not-found-code">404</strong><p className="eyebrow mt-6">Rota dışı</p><h1 className="not-found-title">Bu görünüm bulunamadı</h1><p className="response-copy">Aradığınız ekran mevcut değil veya başka bir yere taşınmış olabilir.</p><Link className="btn btn-primary mt-6" to="/"><ArrowLeft size={15} />Genel bakışa dön</Link></motion.div></div>; }
