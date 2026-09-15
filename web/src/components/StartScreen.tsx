import { useEffect, useMemo, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  CalendarClock,
  ChevronDown,
  ChevronRight,
  Clock,
  Eye,
  FileBarChart2,
  FileSpreadsheet,
  FileText,
  Folder,
  FolderOpen,
  FolderPlus,
  LayoutGrid,
  List,
  Loader2,
  LogOut,
  Mail,
  MoreVertical,
  Pencil,
  Plus,
  Search,
  Settings,
  Share2,
  Trash2,
  Users,
  type LucideIcon,
} from "lucide-react";
import { api } from "../api";
import { isDesigner, useAuth, type TenantInfo } from "../auth";
import { downloadBlob } from "../download";
import { notificationPermission, requestNotificationPermission } from "../notifications";
import { usePrefs } from "../prefs";
import { timeAgo } from "../time";
import type { FolderSummary, Orientation, PageSize, ReportDefinition, ReportSummary } from "../types";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { EmailSettingsPage } from "./EmailSettingsPage";
import { JobsPage } from "./JobsPage";
import { NewReportDialog } from "./NewReportDialog";
import { PageHeader } from "./PageHeader";
import { SortableTh, useSort } from "./SortableTh";
import { ReportPreviewDialog } from "./ReportPreviewDialog";
import { ScheduleDialog } from "./ScheduleDialog";
import { SchedulesPage } from "./SchedulesPage";
import { SettingsPage } from "./SettingsPage";
import { ShareDialog } from "./ShareDialog";
import { TeamPage } from "./TeamPage";

type View = "reports" | "team" | "email" | "jobs" | "schedules" | "settings";
type DragPayload = { kind: "report" | "folder"; id: string };
const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

/** Paths each view lives at — StartScreen is mounted directly under one of these routes (see
 * App.tsx), so switching tabs is a real navigation, not local state. */
const VIEW_PATH: Record<View, string> = {
  reports: "/reports",
  jobs: "/jobs",
  team: "/team",
  email: "/email-settings",
  schedules: "/schedules",
  settings: "/settings",
};

/** A nav entry is a real <a href> so it can be Ctrl/middle-clicked into a new tab like any
 * link (the whole point of moving the app onto routes) — a plain left-click is intercepted for
 * client-side navigation. aria-current tells a screen reader which one you're on. */
function NavItem({
  view,
  current,
  icon: Icon,
  label,
  badge,
  onNavigate,
}: {
  view: View;
  current: View;
  icon: LucideIcon;
  label: string;
  badge?: number;
  onNavigate: (v: View) => void;
}) {
  const active = view === current;
  return (
    <a
      className={`start-nav-item${active ? " on" : ""}`}
      href={VIEW_PATH[view]}
      aria-current={active ? "page" : undefined}
      onClick={(e) => {
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.button === 1) return;
        e.preventDefault();
        onNavigate(view);
      }}
    >
      <Icon size={16} /> <span>{label}</span>
      {badge != null && badge > 0 && <span className="nav-badge">{badge}</span>}
    </a>
  );
}

export function StartScreen({
  view,
  reports,
  samples,
  busy,
  onBlank,
  onSample,
  onOpen,
  onDelete,
  onMoveToFolder,
}: {
  view: View;
  reports: ReportSummary[];
  samples: { name: string; category: string; definition: ReportDefinition }[];
  busy: boolean;
  onBlank: (mode: "free" | "banded", page: { size: PageSize; orientation: Orientation }, folderId: string | null) => void;
  onSample: (name: string, page: { size: PageSize; orientation: Orientation }, folderId: string | null) => void;
  onOpen: (id: string) => void;
  onDelete: (id: string) => void;
  onMoveToFolder: (id: string, folderId: string | null) => void;
}) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const setView = (v: View) => navigate(VIEW_PATH[v]);

  // Which folder you're in and what you searched for live in the URL, not component state:
  // otherwise a trip to Jobs and back dumped you at the root with an empty search box, and a
  // folder couldn't be linked to or bookmarked. Folder changes push (so Back walks up the
  // tree); typing replaces, so a search doesn't bury the history in one entry per keystroke.
  const [searchParams, setSearchParams] = useSearchParams();
  const currentFolderId = searchParams.get("folder");
  const query = searchParams.get("q") ?? "";
  const patchParams = (patch: Record<string, string | null>, replace: boolean) =>
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [k, v] of Object.entries(patch)) {
          if (v) next.set(k, v);
          else next.delete(k);
        }
        return next;
      },
      { replace },
    );
  const setCurrentFolderId = (id: string | null) => patchParams({ folder: id }, false);
  const setQuery = (q: string) => patchParams({ q: q || null }, true);

  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [newReportOpen, setNewReportOpen] = useState(false);
  const [previewReport, setPreviewReport] = useState<ReportSummary | null>(null);
  const [shareReport, setShareReport] = useState<ReportSummary | null>(null);
  const [scheduleReport, setScheduleReport] = useState<ReportSummary | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const canEdit = isDesigner(user);
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [userMenu, setUserMenu] = useState<{ x: number; y: number } | null>(null);

  useEffect(() => {
    api.getTenant().then(setTenant).catch(() => undefined);
  }, []);

  // Nav badges — a running-job count and a pending-invite count are otherwise invisible
  // unless you happen to click into Jobs/Team, so surface them right on the tab.
  const [runningJobs, setRunningJobs] = useState(0);
  const [pendingInvites, setPendingInvites] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const poll = () => {
      api
        .listJobs()
        .then((jobs) => {
          if (!cancelled) setRunningJobs(jobs.filter((j) => j.status === "Queued" || j.status === "Running").length);
        })
        .catch(() => undefined);
    };
    poll();
    const id = window.setInterval(poll, 5000);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, []);

  useEffect(() => {
    if (!canEdit) return;
    api
      .listInvites()
      .then((invites) => setPendingInvites(invites.length))
      .catch(() => undefined);
    // Re-checked whenever the Team tab is left, so generating/revoking an invite there
    // updates the badge without a separate polling loop.
  }, [canEdit, view]);

  const [folders, setFolders] = useState<FolderSummary[]>([]);
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [renamingFolderId, setRenamingFolderId] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [confirmFolderId, setConfirmFolderId] = useState<string | null>(null);
  const [folderError, setFolderError] = useState<string | null>(null);
  const [dragOverFolderId, setDragOverFolderId] = useState<string | null | "root">(null);
  const [menu, setMenu] = useState<{ x: number; y: number; items: MenuItem[] } | null>(null);
  const [foldersLoaded, setFoldersLoaded] = useState(false);
  const viewMode = usePrefs((s) => s.folderViewMode);
  const setViewMode = (mode: "grid" | "detail") => usePrefs.getState().set("folderViewMode", mode);

  const refreshFolders = () => {
    api
      .listFolders()
      .then(setFolders)
      .catch((e) => setFolderError(msg(e)))
      .finally(() => setFoldersLoaded(true));
  };
  useEffect(refreshFolders, []);

  // Grid tiles have no headers to click, so they keep the newest-first order they always had;
  // the detail table re-sorts this list by whichever column you pick.
  const { sort, toggle: toggleSort, apply: applySort } = useSort<"name" | "layoutMode" | "updatedAtUtc" | "createdByEmail">({
    key: "updatedAtUtc",
    dir: "desc",
  });

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return [...reports]
      .filter((r) => !q || r.name.toLowerCase().includes(q))
      .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
  }, [reports, query]);

  const folderById = useMemo(() => new Map(folders.map((f) => [f.id, f])), [folders]);

  const childrenOf = useMemo(() => {
    const map = new Map<string | null, FolderSummary[]>();
    for (const f of folders) {
      const key = f.parentFolderId ?? null;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(f);
    }
    for (const list of map.values()) list.sort((a, b) => a.name.localeCompare(b.name));
    return map;
  }, [folders]);

  const breadcrumb = useMemo(() => {
    const chain: FolderSummary[] = [];
    let cur = currentFolderId ? folderById.get(currentFolderId) : undefined;
    while (cur) {
      chain.unshift(cur);
      cur = cur.parentFolderId ? folderById.get(cur.parentFolderId) : undefined;
    }
    return chain;
  }, [currentFolderId, folderById]);

  const subfolders = childrenOf.get(currentFolderId) ?? [];

  // Search flattens every folder into one result list — without this, two reports with the
  // same name in different folders (or just "which folder was that in?") are indistinguishable.
  const folderPath = (folderId: string | null | undefined): string | null => {
    if (!folderId) return null;
    const chain: string[] = [];
    let cur = folderById.get(folderId);
    while (cur) {
      chain.unshift(cur.name);
      cur = cur.parentFolderId ? folderById.get(cur.parentFolderId) : undefined;
    }
    return chain.join(" / ") || null;
  };

  // A real <a href> so Ctrl/Cmd/middle-click opens the report in a new tab (the browser's own
  // default anchor behavior) — a plain left-click is intercepted for normal in-app navigation
  // instead of a full page reload.
  const reportHref = (id: string) => `/reports/${id}/design`;
  const openOnClick = (e: React.MouseEvent, id: string) => {
    if (busy || e.metaKey || e.ctrlKey || e.shiftKey || e.button === 1) return;
    e.preventDefault();
    onOpen(id);
  };

  const reportCountByFolder = useMemo(() => {
    const map = new Map<string, number>();
    for (const r of reports) {
      if (r.folderId) map.set(r.folderId, (map.get(r.folderId) ?? 0) + 1);
    }
    return map;
  }, [reports]);

  const folderDepth = (f: FolderSummary): number => {
    let depth = 0;
    let cur: FolderSummary | undefined = f;
    while (cur?.parentFolderId) {
      cur = folderById.get(cur.parentFolderId);
      depth++;
    }
    return depth;
  };

  const folderOptions = useMemo(
    () =>
      [...folders]
        .sort((a, b) => a.name.localeCompare(b.name))
        .map((f) => ({ id: f.id, label: `${"— ".repeat(folderDepth(f))}${f.name}` })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [folders],
  );

  // A folder can't be moved into itself or into one of its own descendants — used to
  // grey out invalid targets in the "Move to" submenu (the server also rejects these).
  const isDescendantOrSelf = (candidateId: string, folderId: string): boolean => {
    let cur: FolderSummary | undefined = folderById.get(candidateId);
    while (cur) {
      if (cur.id === folderId) return true;
      cur = cur.parentFolderId ? folderById.get(cur.parentFolderId) : undefined;
    }
    return false;
  };

  const isSearching = query.trim().length > 0;
  const reportsShown = isSearching ? filtered : filtered.filter((r) => (r.folderId ?? null) === currentFolderId);

  const submitNewFolder = async (e: React.FormEvent) => {
    e.preventDefault();
    const name = newFolderName.trim();
    if (!name) return;
    setFolderError(null);
    try {
      await api.createFolder(name, currentFolderId);
      setNewFolderName("");
      setCreatingFolder(false);
      refreshFolders();
    } catch (err) {
      setFolderError(msg(err));
    }
  };

  const commitRename = async (id: string) => {
    const name = renameValue.trim();
    if (!name) return;
    setFolderError(null);
    try {
      await api.renameFolder(id, name);
      setRenamingFolderId(null);
      refreshFolders();
    } catch (err) {
      setFolderError(msg(err));
    }
  };

  const doDeleteFolder = async (id: string) => {
    setFolderError(null);
    try {
      await api.deleteFolder(id);
      setConfirmFolderId(null);
      refreshFolders();
    } catch (err) {
      setFolderError(msg(err));
    }
  };

  const moveFolder = async (id: string, parentFolderId: string | null) => {
    setFolderError(null);
    try {
      await api.moveFolder(id, parentFolderId);
      refreshFolders();
    } catch (err) {
      setFolderError(msg(err));
    }
  };

  const startDrag = (e: React.DragEvent, payload: DragPayload) => {
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("application/json", JSON.stringify(payload));
  };

  const dropOn = (target: string | null) => (e: React.DragEvent) => {
    e.preventDefault();
    // A tile sits inside the folder-content drop zone, so a drop on the tile would
    // otherwise bubble up and re-fire the container's own onDrop for the same event.
    e.stopPropagation();
    setDragOverFolderId(null);
    let payload: DragPayload;
    try {
      payload = JSON.parse(e.dataTransfer.getData("application/json"));
    } catch {
      return;
    }
    if (payload.kind === "report") {
      if ((reports.find((r) => r.id === payload.id)?.folderId ?? null) !== target) {
        onMoveToFolder(payload.id, target);
      }
    } else if (payload.id !== target && !(target && isDescendantOrSelf(target, payload.id))) {
      void moveFolder(payload.id, target);
    }
  };

  const dragOverProps = (target: string | null) => ({
    onDragOver: (e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      setDragOverFolderId(target ?? "root");
    },
    onDragLeave: () => setDragOverFolderId((cur) => (cur === (target ?? "root") ? null : cur)),
    onDrop: dropOn(target),
  });

  const moveToSubmenu = (report: ReportSummary): MenuItem[] => [
    { label: t("reports.menu.root"), disabled: (report.folderId ?? null) === null, onClick: () => onMoveToFolder(report.id, null) },
    ...folderOptions.map((f) => ({
      label: f.label,
      disabled: f.id === (report.folderId ?? null),
      onClick: () => onMoveToFolder(report.id, f.id),
    })),
  ];

  const exportReport = async (r: ReportSummary, format: "pdf" | "xlsx") => {
    setActionError(null);
    try {
      const blob = await api.exportSavedBlob(r.id, format);
      downloadBlob(blob, `${r.name || "report"}.${format}`);
    } catch (err) {
      setActionError(msg(err));
    }
  };

  const runInBackground = async (r: ReportSummary, format: "pdf" | "xlsx") => {
    // Fired synchronously (before any await) so it still counts as a direct result of the
    // click — browsers ignore Notification.requestPermission() calls that aren't. A no-op
    // once the user has already answered once, so this is safe to call every time.
    if (notificationPermission() === "default") void requestNotificationPermission();

    setActionError(null);
    try {
      await api.enqueueReportJob(r.id, format);
      setView("jobs");
    } catch (err) {
      setActionError(msg(err));
    }
  };

  const openReportMenu = (e: React.MouseEvent, r: ReportSummary) => {
    e.preventDefault();
    setMenu({
      x: e.clientX,
      y: e.clientY,
      items: [
        { label: t("reports.menu.open"), icon: FolderOpen, onClick: () => onOpen(r.id) },
        { label: t("reports.menu.preview"), icon: Eye, onClick: () => setPreviewReport(r) },
        {
          label: t("reports.menu.export"),
          children: [
            { label: "PDF", icon: FileText, onClick: () => void exportReport(r, "pdf") },
            { label: "Excel", icon: FileSpreadsheet, onClick: () => void exportReport(r, "xlsx") },
          ],
        },
        {
          label: t("reports.menu.runInBackground"),
          icon: Clock,
          children: [
            { label: "PDF", icon: FileText, onClick: () => void runInBackground(r, "pdf") },
            { label: "Excel", icon: FileSpreadsheet, onClick: () => void runInBackground(r, "xlsx") },
          ],
        },
        ...(canEdit
          ? ([
              { label: t("reports.menu.moveTo"), children: moveToSubmenu(r) },
              { label: t("reports.menu.share"), icon: Share2, onClick: () => setShareReport(r) },
              { label: t("reports.menu.schedule"), icon: CalendarClock, onClick: () => setScheduleReport(r) },
              { sep: true },
              { label: t("common.delete"), icon: Trash2, danger: true, onClick: () => setConfirmId(r.id) },
            ] as MenuItem[])
          : []),
      ],
    });
  };

  const openFolderMenu = (e: React.MouseEvent, f: FolderSummary) => {
    e.preventDefault();
    setMenu({
      x: e.clientX,
      y: e.clientY,
      items: [
        { label: t("reports.menu.open"), icon: FolderOpen, onClick: () => setCurrentFolderId(f.id) },
        ...(canEdit
          ? ([
              {
                label: t("reports.menu.rename"),
                icon: Pencil,
                onClick: () => {
                  setRenamingFolderId(f.id);
                  setRenameValue(f.name);
                },
              },
              { sep: true },
              { label: t("common.delete"), icon: Trash2, danger: true, onClick: () => setConfirmFolderId(f.id) },
            ] as MenuItem[])
          : []),
      ],
    });
  };

  return (
    <div className="start-screen">
      <nav className="start-nav">
        <div className="start-nav-brand">
          <FileBarChart2 size={18} />
          <span>JetReportDesigner</span>
        </div>
        {tenant && <div className="start-nav-org">{tenant.name}</div>}

        <div className="start-nav-group">
          <NavItem view="reports" current={view} icon={LayoutGrid} label={t("nav.reports")} onNavigate={setView} />
          <NavItem view="jobs" current={view} icon={Clock} label={t("nav.jobs")} badge={runningJobs} onNavigate={setView} />
        </div>

        {canEdit && (
          <div className="start-nav-group">
            <div className="start-nav-label">{t("nav.organization")}</div>
            <NavItem view="team" current={view} icon={Users} label={t("nav.team")} badge={pendingInvites} onNavigate={setView} />
            <NavItem view="email" current={view} icon={Mail} label={t("nav.email")} onNavigate={setView} />
            <NavItem view="schedules" current={view} icon={CalendarClock} label={t("nav.schedules")} onNavigate={setView} />
          </div>
        )}

        <div className="start-nav-footer">
          {/* Not gated on canEdit — everything in Settings is a personal preference (theme,
              units, panel behaviour), so a Viewer needs it just as much as a Designer. */}
          <NavItem view="settings" current={view} icon={Settings} label={t("nav.settings")} onNavigate={setView} />

          {user && (
            <>
              <button
                className="start-nav-item start-nav-user"
                onClick={(e) =>
                  setUserMenu({
                    x: e.currentTarget.getBoundingClientRect().right,
                    y: e.currentTarget.getBoundingClientRect().bottom + 4,
                  })
                }
                title={user.email}
              >
                <span className="user-avatar">{user.email[0]?.toUpperCase()}</span>
                {/* The full address never fit the 228px rail — it was rendering as
                    "admin@asi…", which is useless for telling accounts apart. Show the
                    local part plus the role; the full address is in the tooltip and as the
                    first line of the menu this opens. */}
                <span className="start-nav-user-text">
                  <span className="start-nav-user-name">{user.email.split("@")[0]}</span>
                  <span className="start-nav-user-role">{canEdit ? t("nav.designer") : t("nav.viewer")}</span>
                </span>
                <ChevronDown size={12} />
              </button>
              {userMenu && (
                <ContextMenu
                  x={userMenu.x}
                  y={userMenu.y}
                  items={[
                    { label: user.email, disabled: true },
                    { label: canEdit ? t("nav.designer") : t("nav.viewer"), disabled: true },
                    { sep: true },
                    { label: t("nav.signOut"), icon: LogOut, onClick: logout, danger: true },
                  ]}
                  onClose={() => setUserMenu(null)}
                />
              )}
            </>
          )}
        </div>
      </nav>

      <div className="start-scroll">
        {view === "team" ? (
          <TeamPage />
        ) : view === "email" ? (
          <EmailSettingsPage />
        ) : view === "jobs" ? (
          <JobsPage />
        ) : view === "schedules" ? (
          <SchedulesPage />
        ) : view === "settings" ? (
          <SettingsPage />
        ) : (
          <>
            {newReportOpen && (
              <NewReportDialog
                samples={samples}
                busy={busy}
                onCreateBlank={(mode, page) => {
                  setNewReportOpen(false);
                  onBlank(mode, page, currentFolderId);
                }}
                onCreateFromSample={(name, page) => {
                  setNewReportOpen(false);
                  onSample(name, page, currentFolderId);
                }}
                onClose={() => setNewReportOpen(false)}
              />
            )}

            <section className="start-section">
              <PageHeader
                title={t("reports.title")}
                description={canEdit ? undefined : t("reports.viewerHint")}
                actions={
                  <label className="start-search">
                    <Search size={14} />
                    <input value={query} placeholder={t("reports.search")} onChange={(e) => setQuery(e.target.value)} />
                  </label>
                }
              />

              {(folderError || actionError) && (
                <p className="hint" style={{ color: "var(--error)" }}>{folderError ?? actionError}</p>
              )}

              {!foldersLoaded ? (
                <div className="share-loading">
                  <Loader2 size={16} className="spin" />
                </div>
              ) : (
              <div className="drive">
                <nav className="drive-sidebar">
                  <div className="tree-row">
                    <span className="tree-spacer" />
                    <button
                      className={`tree-item${currentFolderId === null ? " on" : ""}${dragOverFolderId === "root" ? " drag-over" : ""}`}
                      onClick={() => setCurrentFolderId(null)}
                      {...dragOverProps(null)}
                    >
                      <LayoutGrid /> {t("reports.allReports")}
                    </button>
                  </div>
                  {(childrenOf.get(null) ?? []).map((f) => (
                    <FolderTreeNode
                      key={f.id}
                      folder={f}
                      depth={0}
                      childrenOf={childrenOf}
                      currentFolderId={currentFolderId}
                      dragOverFolderId={dragOverFolderId}
                      canEdit={canEdit}
                      onNavigate={setCurrentFolderId}
                      onContextMenu={openFolderMenu}
                      onDragStart={startDrag}
                      dragOverProps={dragOverProps}
                      renamingFolderId={renamingFolderId}
                      renameValue={renameValue}
                      onRenameValueChange={setRenameValue}
                      onCommitRename={commitRename}
                      onCancelRename={() => setRenamingFolderId(null)}
                      confirmFolderId={confirmFolderId}
                      onConfirmDelete={doDeleteFolder}
                      onCancelConfirm={() => setConfirmFolderId(null)}
                    />
                  ))}
                </nav>

                <div className="drive-main">
                  <div className="drive-toolbar">
                    {isSearching ? (
                      <span className="drive-path">{t("reports.searchResults")}</span>
                    ) : breadcrumb.length === 0 ? (
                      // At the root, "All reports" already appears as the page title above and
                      // as the highlighted tree item — repeating it here as a breadcrumb too
                      // was pure redundancy. The trail earns its place once you're actually
                      // inside a folder, as a way back.
                      <span className="drive-path">{t("reports.allReports")}</span>
                    ) : (
                      <nav className="breadcrumb">
                        <button className={currentFolderId === null ? "on" : ""} onClick={() => setCurrentFolderId(null)}>
                          All reports
                        </button>
                        {breadcrumb.map((f) => (
                          <span key={f.id} className="breadcrumb-crumb">
                            <ChevronRight size={13} />
                            <button className={currentFolderId === f.id ? "on" : ""} onClick={() => setCurrentFolderId(f.id)}>
                              {f.name}
                            </button>
                          </span>
                        ))}
                      </nav>
                    )}

                    <div className="row">
                      <div className="segmented" role="group" aria-label="View">
                        <button
                          className={viewMode === "grid" ? "on" : ""}
                          onClick={() => setViewMode("grid")}
                          title={t("reports.gridView")}
                          aria-label={t("reports.gridView")}
                        >
                          <LayoutGrid size={13} />
                        </button>
                        <button
                          className={viewMode === "detail" ? "on" : ""}
                          onClick={() => setViewMode("detail")}
                          title={t("reports.detailView")}
                          aria-label={t("reports.detailView")}
                        >
                          <List size={13} />
                        </button>
                      </div>

                      {canEdit && (
                        <button className="btn primary" onClick={() => setNewReportOpen(true)} disabled={busy}>
                          <Plus size={14} /> {t("reports.newReport")}
                        </button>
                      )}

                      {canEdit && !isSearching && (
                        creatingFolder ? (
                          <form className="row" onSubmit={submitNewFolder}>
                            <input
                              autoFocus
                              value={newFolderName}
                              placeholder={t("reports.folderName")}
                              onChange={(e) => setNewFolderName(e.target.value)}
                              onKeyDown={(e) => e.key === "Escape" && setCreatingFolder(false)}
                            />
                            <button className="mini" type="submit">{t("reports.add")}</button>
                            <button className="mini" type="button" onClick={() => setCreatingFolder(false)}>{t("common.cancel")}</button>
                          </form>
                        ) : (
                          <button className="mini" onClick={() => setCreatingFolder(true)}>
                            <FolderPlus size={13} /> {t("reports.newFolder")}
                          </button>
                        )
                      )}
                    </div>
                  </div>

                  <div
                    className={`drive-content${dragOverFolderId === (currentFolderId ?? "root") && !isSearching ? " drag-over" : ""}`}
                    {...(isSearching ? {} : dragOverProps(currentFolderId))}
                  >
                    {reportsShown.length === 0 && (isSearching || subfolders.length === 0) ? (
                      <div className="start-empty">
                        <FileText />
                        <div>
                          {reports.length === 0
                            ? t("reports.emptyNoReports")
                            : isSearching
                              ? t("reports.emptyNoMatch")
                              : t("reports.emptyFolder")}
                        </div>
                        {reports.length === 0 && <p>{t("reports.emptyHint")}</p>}
                      </div>
                    ) : viewMode === "grid" ? (
                      <div className="drive-grid">
                        {!isSearching &&
                          subfolders.map((f) =>
                            renamingFolderId === f.id ? (
                              <div key={f.id} className="drive-tile folder-tile drive-tile-editing">
                                <Folder className="tile-icon" />
                                <input
                                  autoFocus
                                  value={renameValue}
                                  onChange={(e) => setRenameValue(e.target.value)}
                                  onKeyDown={(e) => e.key === "Enter" && void commitRename(f.id)}
                                />
                                <div className="row">
                                  <button className="mini" onClick={() => void commitRename(f.id)}>{t("common.save")}</button>
                                  <button className="mini" onClick={() => setRenamingFolderId(null)}>{t("common.cancel")}</button>
                                </div>
                              </div>
                            ) : confirmFolderId === f.id ? (
                              <div key={f.id} className="drive-tile folder-tile drive-tile-editing">
                                <Folder className="tile-icon" />
                                <span>{t("reports.deleteConfirm", { name: f.name })}</span>
                                <div className="row">
                                  <button className="mini danger" onClick={() => void doDeleteFolder(f.id)}>{t("common.delete")}</button>
                                  <button className="mini" onClick={() => setConfirmFolderId(null)}>{t("common.cancel")}</button>
                                </div>
                              </div>
                            ) : (
                              <button
                                key={f.id}
                                className={`drive-tile folder-tile${dragOverFolderId === f.id ? " drag-over" : ""}`}
                                onClick={() => setCurrentFolderId(f.id)}
                                onContextMenu={(e) => openFolderMenu(e, f)}
                                draggable={canEdit}
                                onDragStart={(e) => startDrag(e, { kind: "folder", id: f.id })}
                                {...dragOverProps(f.id)}
                              >
                                <Folder className="tile-icon" />
                                <span className="drive-tile-name">{f.name}</span>
                                <span className="drive-tile-count">
                                  {t("reports.reportCount", { count: reportCountByFolder.get(f.id) ?? 0 })}
                                </span>
                              </button>
                            ),
                          )}

                        {reportsShown.map((r) =>
                          confirmId === r.id ? (
                            <div key={r.id} className="drive-tile drive-tile-editing">
                              <FileText className="tile-icon" />
                              <span>{t("reports.deleteConfirm", { name: r.name })}</span>
                              <div className="row">
                                <button
                                  className="mini danger"
                                  onClick={() => {
                                    onDelete(r.id);
                                    setConfirmId(null);
                                  }}
                                >
                                  Delete
                                </button>
                                <button className="mini" onClick={() => setConfirmId(null)}>{t("common.cancel")}</button>
                              </div>
                            </div>
                          ) : (
                            <div key={r.id} className="drive-tile-cell">
                              <a
                                className="drive-tile"
                                href={reportHref(r.id)}
                                onClick={(e) => openOnClick(e, r.id)}
                                onContextMenu={(e) => openReportMenu(e, r)}
                                aria-disabled={busy}
                                draggable={canEdit}
                                onDragStart={(e) => startDrag(e, { kind: "report", id: r.id })}
                              >
                                <FileText className="tile-icon" />
                                <span className="drive-tile-name">{r.name}</span>
                                {isSearching && folderPath(r.folderId) && (
                                  <span className="drive-tile-path">{folderPath(r.folderId)}</span>
                                )}
                                <span className="drive-tile-meta">
                                  <span className="chip">{t(`reports.layout.${r.layoutMode}`)}</span> {timeAgo(r.updatedAtUtc, i18n.language)}
                                </span>
                              </a>
                              <button
                                className="mini ghost drive-tile-kebab"
                                title={t("common.moreActions")}
                                aria-label={t("common.moreActions")}
                                onClick={(e) => {
                                  e.stopPropagation();
                                  openReportMenu(e, r);
                                }}
                              >
                                <MoreVertical size={14} />
                              </button>
                            </div>
                          ),
                        )}
                      </div>
                    ) : (
                      <table className="drive-table">
                        <thead>
                          <tr>
                            <SortableTh column="name" sort={sort} onToggle={toggleSort}>{t("reports.name")}</SortableTh>
                            <SortableTh column="layoutMode" sort={sort} onToggle={toggleSort}>{t("reports.type")}</SortableTh>
                            <SortableTh column="updatedAtUtc" sort={sort} onToggle={toggleSort}>{t("reports.updated")}</SortableTh>
                            <SortableTh column="createdByEmail" sort={sort} onToggle={toggleSort}>{t("reports.createdBy")}</SortableTh>
                            <th />
                          </tr>
                        </thead>
                        <tbody>
                          {!isSearching &&
                            subfolders.map((f) =>
                              renamingFolderId === f.id ? (
                                <tr key={f.id}>
                                  <td colSpan={5} className="drive-table-editing">
                                    <Folder className="tile-icon" />
                                    <input
                                      autoFocus
                                      value={renameValue}
                                      onChange={(e) => setRenameValue(e.target.value)}
                                      onKeyDown={(e) => e.key === "Enter" && void commitRename(f.id)}
                                    />
                                    <button className="mini" onClick={() => void commitRename(f.id)}>{t("common.save")}</button>
                                    <button className="mini" onClick={() => setRenamingFolderId(null)}>{t("common.cancel")}</button>
                                  </td>
                                </tr>
                              ) : confirmFolderId === f.id ? (
                                <tr key={f.id}>
                                  <td colSpan={5} className="drive-table-editing">
                                    <Folder className="tile-icon" />
                                    <span>{t("reports.deleteConfirm", { name: f.name })}</span>
                                    <button className="mini danger" onClick={() => void doDeleteFolder(f.id)}>{t("common.delete")}</button>
                                    <button className="mini" onClick={() => setConfirmFolderId(null)}>{t("common.cancel")}</button>
                                  </td>
                                </tr>
                              ) : (
                                <tr
                                  key={f.id}
                                  className={dragOverFolderId === f.id ? "drag-over" : ""}
                                  onClick={() => setCurrentFolderId(f.id)}
                                  onContextMenu={(e) => openFolderMenu(e, f)}
                                  draggable={canEdit}
                                  onDragStart={(e) => startDrag(e, { kind: "folder", id: f.id })}
                                  {...dragOverProps(f.id)}
                                >
                                  <td className="drive-table-name">
                                    <Folder /> {f.name}
                                    <span className="drive-tile-count">
                                      {t("reports.reportCount", { count: reportCountByFolder.get(f.id) ?? 0 })}
                                    </span>
                                  </td>
                                  <td>{t("reports.folder")}</td>
                                  <td>{new Date(f.createdAtUtc).toLocaleDateString(i18n.language)}</td>
                                  <td>—</td>
                                  <td>
                                    <button
                                      className="mini ghost row-kebab"
                                      title={t("common.moreActions")}
                                      aria-label={t("common.moreActions")}
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        openFolderMenu(e, f);
                                      }}
                                    >
                                      <MoreVertical size={14} />
                                    </button>
                                  </td>
                                </tr>
                              ),
                            )}

                          {applySort(reportsShown, (r, key) => r[key]).map((r) =>
                            confirmId === r.id ? (
                              <tr key={r.id}>
                                <td colSpan={5} className="drive-table-editing">
                                  <FileText className="tile-icon" />
                                  <span>{t("reports.deleteConfirm", { name: r.name })}</span>
                                  <button
                                    className="mini danger"
                                    onClick={() => {
                                      onDelete(r.id);
                                      setConfirmId(null);
                                    }}
                                  >
                                    Delete
                                  </button>
                                  <button className="mini" onClick={() => setConfirmId(null)}>{t("common.cancel")}</button>
                                </td>
                              </tr>
                            ) : (
                              <tr
                                key={r.id}
                                onContextMenu={(e) => openReportMenu(e, r)}
                                draggable={canEdit}
                                onDragStart={(e) => startDrag(e, { kind: "report", id: r.id })}
                              >
                                <td className="drive-table-name">
                                  <a className="drive-row-link" href={reportHref(r.id)} onClick={(e) => openOnClick(e, r.id)}>
                                    <FileText /> {r.name}
                                  </a>
                                  {isSearching && folderPath(r.folderId) && (
                                    <span className="drive-tile-count">{folderPath(r.folderId)}</span>
                                  )}
                                </td>
                                <td><span className="chip">{t(`reports.layout.${r.layoutMode}`)}</span></td>
                                <td title={new Date(r.updatedAtUtc).toLocaleString(i18n.language)}>{timeAgo(r.updatedAtUtc, i18n.language)}</td>
                                <td>{r.createdByEmail ?? "—"}</td>
                                <td>
                                  <button
                                    className="mini ghost row-kebab"
                                    title={t("common.moreActions")}
                                    aria-label={t("common.moreActions")}
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      openReportMenu(e, r);
                                    }}
                                  >
                                    <MoreVertical size={14} />
                                  </button>
                                </td>
                              </tr>
                            ),
                          )}
                        </tbody>
                      </table>
                    )}
                  </div>
                </div>
              </div>
              )}
            </section>
          </>
        )}
      </div>

      {menu && <ContextMenu x={menu.x} y={menu.y} items={menu.items} onClose={() => setMenu(null)} />}

      {previewReport && (
        <ReportPreviewDialog
          report={previewReport}
          onShare={() => {
            setShareReport(previewReport);
            setPreviewReport(null);
          }}
          onClose={() => setPreviewReport(null)}
        />
      )}

      {shareReport && <ShareDialog report={shareReport} onClose={() => setShareReport(null)} />}

      {scheduleReport && (
        <ScheduleDialog
          reportId={scheduleReport.id}
          reportName={scheduleReport.name}
          onClose={() => setScheduleReport(null)}
        />
      )}
    </div>
  );
}

function FolderTreeNode({
  folder,
  depth,
  childrenOf,
  currentFolderId,
  dragOverFolderId,
  canEdit,
  onNavigate,
  onContextMenu,
  onDragStart,
  dragOverProps,
  renamingFolderId,
  renameValue,
  onRenameValueChange,
  onCommitRename,
  onCancelRename,
  confirmFolderId,
  onConfirmDelete,
  onCancelConfirm,
}: {
  folder: FolderSummary;
  depth: number;
  childrenOf: Map<string | null, FolderSummary[]>;
  currentFolderId: string | null;
  dragOverFolderId: string | null | "root";
  canEdit: boolean;
  onNavigate: (id: string) => void;
  onContextMenu: (e: React.MouseEvent, f: FolderSummary) => void;
  onDragStart: (e: React.DragEvent, payload: DragPayload) => void;
  dragOverProps: (target: string | null) => {
    onDragOver: (e: React.DragEvent) => void;
    onDragLeave: () => void;
    onDrop: (e: React.DragEvent) => void;
  };
  renamingFolderId: string | null;
  renameValue: string;
  onRenameValueChange: (v: string) => void;
  onCommitRename: (id: string) => void;
  onCancelRename: () => void;
  confirmFolderId: string | null;
  onConfirmDelete: (id: string) => void;
  onCancelConfirm: () => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(true);
  const kids = childrenOf.get(folder.id) ?? [];

  const childProps = {
    childrenOf,
    currentFolderId,
    dragOverFolderId,
    canEdit,
    onNavigate,
    onContextMenu,
    onDragStart,
    dragOverProps,
    renamingFolderId,
    renameValue,
    onRenameValueChange,
    onCommitRename,
    onCancelRename,
    confirmFolderId,
    onConfirmDelete,
    onCancelConfirm,
  };

  return (
    <div>
      <div className="tree-row" style={{ paddingLeft: depth * 14 }}>
        {kids.length > 0 ? (
          <button className="tree-toggle" onClick={() => setOpen((o) => !o)} aria-label={open ? t("common.close") : t("reports.menu.open")}>
            <ChevronDown size={13} style={{ transform: open ? undefined : "rotate(-90deg)" }} />
          </button>
        ) : (
          <span className="tree-spacer" />
        )}
        {renamingFolderId === folder.id ? (
          <div className="tree-item tree-item-editing">
            <input
              autoFocus
              value={renameValue}
              onChange={(e) => onRenameValueChange(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && onCommitRename(folder.id)}
            />
            <button className="mini" onClick={() => onCommitRename(folder.id)}>{t("common.save")}</button>
            <button className="mini" onClick={onCancelRename}>{t("common.cancel")}</button>
          </div>
        ) : confirmFolderId === folder.id ? (
          <div className="tree-item tree-item-editing">
            <span>{t("reports.deleteConfirm", { name: folder.name })}</span>
            <button className="mini danger" onClick={() => onConfirmDelete(folder.id)}>{t("common.delete")}</button>
            <button className="mini" onClick={onCancelConfirm}>{t("common.cancel")}</button>
          </div>
        ) : (
          <button
            className={`tree-item${currentFolderId === folder.id ? " on" : ""}${dragOverFolderId === folder.id ? " drag-over" : ""}`}
            onClick={() => onNavigate(folder.id)}
            onContextMenu={(e) => onContextMenu(e, folder)}
            draggable={canEdit}
            onDragStart={(e) => onDragStart(e, { kind: "folder", id: folder.id })}
            {...dragOverProps(folder.id)}
          >
            <Folder /> {folder.name}
          </button>
        )}
      </div>
      {open && kids.map((k) => <FolderTreeNode key={k.id} folder={k} depth={depth + 1} {...childProps} />)}
    </div>
  );
}
