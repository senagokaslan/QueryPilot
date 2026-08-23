import { AnimatePresence, motion, useReducedMotion } from "framer-motion";
import { BarChart3, Boxes, ChartNoAxesCombined, Menu, Package, ReceiptText, Search, ShoppingBag, Users, X } from "lucide-react";
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { easeOutExpo } from "./motion";

const sections = [
  { label: "Görünüm", items: [{ to: "/", label: "Genel bakış", icon: ChartNoAxesCombined }, { to: "/analytics", label: "Derin analiz", icon: BarChart3 }, { to: "/ai", label: "Veriye Sor", icon: Search }] },
  { label: "Satış", items: [{ to: "/orders", label: "Siparişler", icon: ReceiptText }, { to: "/orders/new", label: "Yeni sipariş", icon: ShoppingBag }] },
  { label: "Kayıtlar", items: [{ to: "/categories", label: "Kategoriler", icon: Boxes }, { to: "/products", label: "Ürünler", icon: Package }, { to: "/customers", label: "Müşteriler", icon: Users }] },
];

const routeNames: Array<[string, string]> = [
  ["/orders/new", "Yeni sipariş"], ["/orders/", "Sipariş detayı"], ["/analytics", "Derin analiz"], ["/ai", "Veriye Sor"], ["/categories", "Kategoriler"], ["/products", "Ürünler"], ["/customers", "Müşteriler"], ["/orders", "Siparişler"], ["/", "Genel bakış"],
];

function Brand() {
  return <span className="brand-wordmark">QueryPilot</span>;
}

function Intro() {
  const reduced = useReducedMotion();
  const [visible, setVisible] = useState(() => sessionStorage.getItem("qp-intro-seen") !== "1");
  useEffect(() => {
    if (!visible) return;
    sessionStorage.setItem("qp-intro-seen", "1");
    const timer = window.setTimeout(() => setVisible(false), reduced ? 220 : 1450);
    return () => window.clearTimeout(timer);
  }, [reduced, visible]);
  return <AnimatePresence>{visible && <motion.div className="intro" initial={{ opacity: 1 }} exit={{ opacity: 0, filter: "blur(10px)" }} transition={{ duration: reduced ? 0 : .4 }}><motion.div initial={reduced ? false : { opacity: 0, scale: .92, y: 8 }} animate={{ opacity: 1, scale: 1, y: 0 }} transition={{ duration: .55, ease: easeOutExpo }}><Brand /></motion.div></motion.div>}</AnimatePresence>;
}

export function AppShell({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const reduced = useReducedMotion();
  const current = useMemo(() => routeNames.find(([path]) => path === "/" ? location.pathname === "/" : location.pathname.startsWith(path))?.[1] ?? "QueryPilot", [location.pathname]);
  const desktop = typeof window !== "undefined" && window.innerWidth >= 1024;

  return <div className="app-frame">
    <Intro />
    <AnimatePresence>{open && <motion.button className="mobile-scrim" aria-label="Menüyü kapat" onClick={() => setOpen(false)} initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} />}</AnimatePresence>
    <motion.aside className="sidebar" animate={{ x: open || desktop ? 0 : "-100%" }} transition={{ type: "spring", stiffness: 420, damping: 38 }}>
      <div className="sidebar-head"><NavLink to="/" onClick={() => setOpen(false)} aria-label="QueryPilot ana sayfa"><Brand /></NavLink><button className="icon-btn mobile-nav-toggle" aria-label="Menüyü kapat" onClick={() => setOpen(false)}><X size={18} /></button></div>
      <nav className="sidebar-nav" aria-label="Ana menü">
        {sections.map((section) => <section key={section.label}><p className="nav-section-label">{section.label}</p>{section.items.map(({ to, label, icon: Icon }) => <NavLink end={to === "/"} className="nav-link" to={to} key={to} onClick={() => setOpen(false)}>{({ isActive }) => <><AnimatePresence>{isActive && <motion.span layoutId="active-navigation" className="nav-active" transition={reduced ? { duration: 0 } : { type: "spring", stiffness: 500, damping: 40 }} />}</AnimatePresence><span className="nav-icon"><Icon size={17} /></span><span>{label}</span></>}</NavLink>)}</section>)}
      </nav>
    </motion.aside>
    <div className="workspace">
      <header className="topbar"><button className="icon-btn mobile-nav-toggle" aria-label="Menüyü aç" onClick={() => setOpen(true)}><Menu size={19} /></button><div className="topbar-context"><span>QueryPilot</span><i>/</i><strong>{current}</strong></div><div className="topbar-date"><span>{new Intl.DateTimeFormat("tr-TR", { day: "2-digit", month: "short", year: "numeric" }).format(new Date())}</span></div></header>
      <main className="main-stage">{children}</main>
    </div>
  </div>;
}
