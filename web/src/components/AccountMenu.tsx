import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { LogOut, UserCog } from "lucide-react";
import { api } from "../api";
import { displayNameOf, initials, isDesigner, useAuth } from "../auth";
import { useEscapeKey } from "../useEscapeKey";
import { Avatar } from "./ui";

/**
 * The account button and its card (avatar, name, email, organization, "Manage your account", "Sign out"),
 * the same in the Start screen and the designer — like the account menu in Google apps.
 */
export function AccountMenu({ variant = "rail" }: { variant?: "rail" | "toolbar" | "compact" }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const [open, setOpen] = useState(false);
  const [org, setOrg] = useState<string | null>(null);
  const [pos, setPos] = useState<{ left: number; top?: number; bottom?: number }>({ left: 0 });
  const button = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLDivElement>(null);
  useEscapeKey(() => setOpen(false));

  useEffect(() => {
    if (!open) return;
    if (org === null) api.getTenant().then((tn) => setOrg(tn.name)).catch(() => setOrg(""));
    const close = (e: MouseEvent) => {
      if (!panel.current?.contains(e.target as Node) && !button.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open, org]);

  if (!user) return null;

  const toggle = () => {
    const r = button.current!.getBoundingClientRect();
    const width = 320;
    const left = Math.max(8, Math.min(window.innerWidth - width - 8, variant === "rail" ? r.left : r.right - width));
    // From the rail (bottom-left) the card opens upwards; from a toolbar it drops down.
    setPos(variant === "rail" ? { left, bottom: window.innerHeight - r.top + 8 } : { left, top: r.bottom + 8 });
    setOpen((o) => !o);
  };

  const name = displayNameOf(user);
  const role = isDesigner(user) ? t("nav.designer") : t("nav.viewer");

  return (
    <>
      <button
        ref={button}
        className={`account-btn account-btn-${variant}`}
        onClick={toggle}
        aria-haspopup="dialog"
        aria-expanded={open}
        title={`${name}\n${user.email}`}
        aria-label={t("account.menuLabel", { name })}
      >
        <Avatar text={initials(user)} seed={user.email} size={variant === "rail" ? 32 : 30} />
        {variant === "rail" && (
          <span className="account-btn-text">
            <span className="account-btn-name">{name}</span>
            <span className="account-btn-role">{role}</span>
          </span>
        )}
      </button>
      {open &&
        createPortal(
          <div ref={panel} className="account-card" role="dialog" aria-label={t("account.title")} style={pos}>
            <div className="account-card-email">{user.email}</div>
            <Avatar text={initials(user)} seed={user.email} size={72} />
            <div className="account-card-hello">{t("account.hello", { name: user.displayName?.split(" ")[0] || name })}</div>
            <div className="account-card-meta">
              {org ? `${org} · ` : ""}
              {role}
            </div>
            <button
              className="btn outline account-card-manage"
              onClick={() => {
                setOpen(false);
                navigate("/account");
              }}
            >
              <UserCog size={16} /> {t("account.manage")}
            </button>
            <div className="account-card-sep" />
            <button className="account-card-signout" onClick={logout}>
              <LogOut size={18} /> {t("nav.signOut")}
            </button>
          </div>,
          document.body,
        )}
    </>
  );
}
