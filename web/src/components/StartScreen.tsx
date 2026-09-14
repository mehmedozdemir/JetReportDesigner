import { useEffect, useMemo, useState } from "react";
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
  LogOut,
  Mail,
  Pencil,
  Plus,
  Search,
  Settings,
  Share2,
  Trash2,
  Users,
  X,
} from "lucide-react";
import { api } from "../api";
import { isDesigner, useAuth, type TenantInfo } from "../auth";
import { downloadBlob } from "../download";
import { usePrefs } from "../prefs";
import { timeAgo } from "../time";
import type { FolderSummary, Orientation, PageSize, ReportDefinition, ReportSummary } from "../types";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { EmailSettingsPage } from "./EmailSettingsPage";
import { JobsPage } from "./JobsPage";
import { NewReportDialog } from "./NewReportDialog";
import { ReportPreviewDialog } from "./ReportPreviewDialog";
import { ScheduleDialog } from "./ScheduleDialog";
import { SchedulesPage } from "./SchedulesPage";
import { ShareDialog } from "./ShareDialog";
import { TeamPage } from "./TeamPage";

type View = "reports" | "team" | "email" | "jobs" | "schedules";
type DragPayload = { kind: "report" | "folder"; id: string };
const msg = (e: unknown) => (e instanceof Error ? e.message : String(e));

export function StartScreen({
  reports,
  samples,
  busy,
  onBlank,
  onSample,
  onOpen,
  onDelete,
  onMoveToFolder,
  onSettings,
  onClose,
}: {
  reports: ReportSummary[];
  samples: { name: string; category: string; definition: ReportDefinition }[];
  busy: boolean;
  onBlank: (mode: "free" | "banded", page: { size: PageSize; orientation: Orientation }, folderId: string | null) => void;
  onSample: (name: string, page: { size: PageSize; orientation: Orientation }, folderId: string | null) => void;
  onOpen: (id: string) => void;
  onDelete: (id: string) => void;
  onMoveToFolder: (id: string, folderId: string | null) => void;
  onSettings: () => void;
  onClose?: () => void;
}) {
  const [query, setQuery] = useState("");
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [view, setView] = useState<View>("reports");
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

  const [folders, setFolders] = useState<FolderSummary[]>([]);
  const [currentFolderId, setCurrentFolderId] = useState<string | null>(null);
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [newFolderName, setNewFolderName] = useState("");
  const [renamingFolderId, setRenamingFolderId] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [confirmFolderId, setConfirmFolderId] = useState<string | null>(null);
  const [folderError, setFolderError] = useState<string | null>(null);
  const [dragOverFolderId, setDragOverFolderId] = useState<string | null | "root">(null);
  const [menu, setMenu] = useState<{ x: number; y: number; items: MenuItem[] } | null>(null);
  const viewMode = usePrefs((s) => s.folderViewMode);
  const setViewMode = (mode: "grid" | "detail") => usePrefs.getState().set("folderViewMode", mode);

  const refreshFolders = () => {
    api.listFolders().then(setFolders).catch((e) => setFolderError(msg(e)));
  };
  useEffect(refreshFolders, []);

  useEffect(() => {
    if (!onClose) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

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
    { label: "— Root —", disabled: (report.folderId ?? null) === null, onClick: () => onMoveToFolder(report.id, null) },
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
        { label: "Open", icon: FolderOpen, onClick: () => onOpen(r.id) },
        { label: "Preview", icon: Eye, onClick: () => setPreviewReport(r) },
        {
          label: "Export",
          children: [
            { label: "PDF", icon: FileText, onClick: () => void exportReport(r, "pdf") },
            { label: "Excel", icon: FileSpreadsheet, onClick: () => void exportReport(r, "xlsx") },
          ],
        },
        {
          label: "Run in background",
          icon: Clock,
          children: [
            { label: "PDF", icon: FileText, onClick: () => void runInBackground(r, "pdf") },
            { label: "Excel", icon: FileSpreadsheet, onClick: () => void runInBackground(r, "xlsx") },
          ],
        },
        ...(canEdit
          ? ([
              { label: "Move to", children: moveToSubmenu(r) },
              { label: "Share", icon: Share2, onClick: () => setShareReport(r) },
              { label: "Schedule…", icon: CalendarClock, onClick: () => setScheduleReport(r) },
              { sep: true },
              { label: "Delete", icon: Trash2, danger: true, onClick: () => setConfirmId(r.id) },
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
        { label: "Open", icon: FolderOpen, onClick: () => setCurrentFolderId(f.id) },
        ...(canEdit
          ? ([
              {
                label: "Rename",
                icon: Pencil,
                onClick: () => {
                  setRenamingFolderId(f.id);
                  setRenameValue(f.name);
                },
              },
              { sep: true },
              { label: "Delete", icon: Trash2, danger: true, onClick: () => setConfirmFolderId(f.id) },
            ] as MenuItem[])
          : []),
      ],
    });
  };

  return (
    <div className="start-screen">
      <header className="start-head">
        <div className="brand">
          <FileBarChart2 size={18} />
          <span>JetReportDesigner</span>
          {tenant && <span className="start-org">{tenant.name}</span>}
        </div>

        <div className="segmented" role="group" aria-label="Start screen section">
          <button className={view === "reports" ? "on" : ""} onClick={() => setView("reports")}>
            <LayoutGrid size={14} /> Reports
          </button>
          <button className={view === "jobs" ? "on" : ""} onClick={() => setView("jobs")}>
            <Clock size={14} /> Jobs
          </button>
          {canEdit && (
            <>
              <button className={view === "team" ? "on" : ""} onClick={() => setView("team")}>
                <Users size={14} /> Team
              </button>
              <button className={view === "email" ? "on" : ""} onClick={() => setView("email")}>
                <Mail size={14} /> Email
              </button>
              <button className={view === "schedules" ? "on" : ""} onClick={() => setView("schedules")}>
                <CalendarClock size={14} /> Schedules
              </button>
            </>
          )}
        </div>

        <div className="row" style={{ marginLeft: "auto" }}>
          {canEdit && (
            <button className="btn icon" onClick={onSettings} title="Settings" aria-label="Settings">
              <Settings />
            </button>
          )}

          {user && (
            <>
              <button
                className="btn user-chip"
                onClick={(e) =>
                  setUserMenu({
                    x: e.currentTarget.getBoundingClientRect().right,
                    y: e.currentTarget.getBoundingClientRect().bottom + 4,
                  })
                }
                title={user.email}
              >
                <span className="user-avatar">{user.email[0]?.toUpperCase()}</span>
                <span className="user-email">{user.email}</span>
                <ChevronDown size={12} />
              </button>
              {userMenu && (
                <ContextMenu
                  x={userMenu.x}
                  y={userMenu.y}
                  items={[
                    { label: user.email, disabled: true },
                    { label: canEdit ? "Designer" : "Viewer", disabled: true },
                    { sep: true },
                    { label: "Sign out", icon: LogOut, onClick: logout, danger: true },
                  ]}
                  onClose={() => setUserMenu(null)}
                />
              )}
            </>
          )}

          {onClose && (
            <button className="btn icon" onClick={onClose} title="Close" aria-label="Close start screen">
              <X />
            </button>
          )}
        </div>
      </header>

      <div className="start-scroll">
        {view === "team" ? (
          <TeamPage />
        ) : view === "email" ? (
          <EmailSettingsPage />
        ) : view === "jobs" ? (
          <JobsPage />
        ) : view === "schedules" ? (
          <SchedulesPage />
        ) : (
          <>
            <section className="start-section start-new-section">
              {!canEdit ? (
                <p className="hint">Viewer role — sign in as a Designer to create reports.</p>
              ) : (
                <button className="btn primary" onClick={() => setNewReportOpen(true)} disabled={busy}>
                  <Plus /> New report
                </button>
              )}
            </section>

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
              <div className="start-list-head">
                <h3>Reports</h3>
                <label className="start-search">
                  <Search size={14} />
                  <input
                    value={query}
                    placeholder="Search reports"
                    onChange={(e) => setQuery(e.target.value)}
                  />
                </label>
              </div>

              {(folderError || actionError) && (
                <p className="hint" style={{ color: "var(--error)" }}>{folderError ?? actionError}</p>
              )}

              <div className="drive">
                <nav className="drive-sidebar">
                  <div className="tree-row">
                    <span className="tree-spacer" />
                    <button
                      className={`tree-item${currentFolderId === null ? " on" : ""}${dragOverFolderId === "root" ? " drag-over" : ""}`}
                      onClick={() => setCurrentFolderId(null)}
                      {...dragOverProps(null)}
                    >
                      <LayoutGrid /> All reports
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
                      <span className="drive-path">Search results</span>
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
                          title="Grid view"
                          aria-label="Grid view"
                        >
                          <LayoutGrid size={13} />
                        </button>
                        <button
                          className={viewMode === "detail" ? "on" : ""}
                          onClick={() => setViewMode("detail")}
                          title="Detail view"
                          aria-label="Detail view"
                        >
                          <List size={13} />
                        </button>
                      </div>

                      {canEdit && !isSearching && (
                        creatingFolder ? (
                          <form className="row" onSubmit={submitNewFolder}>
                            <input
                              autoFocus
                              value={newFolderName}
                              placeholder="Folder name"
                              onChange={(e) => setNewFolderName(e.target.value)}
                              onKeyDown={(e) => e.key === "Escape" && setCreatingFolder(false)}
                            />
                            <button className="mini" type="submit">Add</button>
                            <button className="mini" type="button" onClick={() => setCreatingFolder(false)}>Cancel</button>
                          </form>
                        ) : (
                          <button className="mini" onClick={() => setCreatingFolder(true)}>
                            <FolderPlus size={13} /> New folder
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
                            ? "No reports yet."
                            : isSearching
                              ? "No reports match your search."
                              : "This folder is empty."}
                        </div>
                        {reports.length === 0 && <p>Start from a blank report or a sample above.</p>}
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
                                  <button className="mini" onClick={() => void commitRename(f.id)}>Save</button>
                                  <button className="mini" onClick={() => setRenamingFolderId(null)}>Cancel</button>
                                </div>
                              </div>
                            ) : confirmFolderId === f.id ? (
                              <div key={f.id} className="drive-tile folder-tile drive-tile-editing">
                                <Folder className="tile-icon" />
                                <span>Delete “{f.name}”?</span>
                                <div className="row">
                                  <button className="mini danger" onClick={() => void doDeleteFolder(f.id)}>Delete</button>
                                  <button className="mini" onClick={() => setConfirmFolderId(null)}>Cancel</button>
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
                                  {reportCountByFolder.get(f.id) ?? 0} report{(reportCountByFolder.get(f.id) ?? 0) === 1 ? "" : "s"}
                                </span>
                              </button>
                            ),
                          )}

                        {reportsShown.map((r) =>
                          confirmId === r.id ? (
                            <div key={r.id} className="drive-tile drive-tile-editing">
                              <FileText className="tile-icon" />
                              <span>Delete “{r.name}”?</span>
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
                                <button className="mini" onClick={() => setConfirmId(null)}>Cancel</button>
                              </div>
                            </div>
                          ) : (
                            <button
                              key={r.id}
                              className="drive-tile"
                              onClick={() => onOpen(r.id)}
                              onContextMenu={(e) => openReportMenu(e, r)}
                              disabled={busy}
                              draggable={canEdit}
                              onDragStart={(e) => startDrag(e, { kind: "report", id: r.id })}
                            >
                              <FileText className="tile-icon" />
                              <span className="drive-tile-name">{r.name}</span>
                              <span className="drive-tile-meta">
                                <span className="chip">{r.layoutMode}</span> {timeAgo(r.updatedAtUtc)}
                              </span>
                            </button>
                          ),
                        )}
                      </div>
                    ) : (
                      <table className="drive-table">
                        <thead>
                          <tr>
                            <th>Name</th>
                            <th>Type</th>
                            <th>Created</th>
                            <th>Created by</th>
                          </tr>
                        </thead>
                        <tbody>
                          {!isSearching &&
                            subfolders.map((f) =>
                              renamingFolderId === f.id ? (
                                <tr key={f.id}>
                                  <td colSpan={4} className="drive-table-editing">
                                    <Folder className="tile-icon" />
                                    <input
                                      autoFocus
                                      value={renameValue}
                                      onChange={(e) => setRenameValue(e.target.value)}
                                      onKeyDown={(e) => e.key === "Enter" && void commitRename(f.id)}
                                    />
                                    <button className="mini" onClick={() => void commitRename(f.id)}>Save</button>
                                    <button className="mini" onClick={() => setRenamingFolderId(null)}>Cancel</button>
                                  </td>
                                </tr>
                              ) : confirmFolderId === f.id ? (
                                <tr key={f.id}>
                                  <td colSpan={4} className="drive-table-editing">
                                    <Folder className="tile-icon" />
                                    <span>Delete “{f.name}”?</span>
                                    <button className="mini danger" onClick={() => void doDeleteFolder(f.id)}>Delete</button>
                                    <button className="mini" onClick={() => setConfirmFolderId(null)}>Cancel</button>
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
                                      {reportCountByFolder.get(f.id) ?? 0} report{(reportCountByFolder.get(f.id) ?? 0) === 1 ? "" : "s"}
                                    </span>
                                  </td>
                                  <td>Folder</td>
                                  <td>{new Date(f.createdAtUtc).toLocaleDateString()}</td>
                                  <td>—</td>
                                </tr>
                              ),
                            )}

                          {reportsShown.map((r) =>
                            confirmId === r.id ? (
                              <tr key={r.id}>
                                <td colSpan={4} className="drive-table-editing">
                                  <FileText className="tile-icon" />
                                  <span>Delete “{r.name}”?</span>
                                  <button
                                    className="mini danger"
                                    onClick={() => {
                                      onDelete(r.id);
                                      setConfirmId(null);
                                    }}
                                  >
                                    Delete
                                  </button>
                                  <button className="mini" onClick={() => setConfirmId(null)}>Cancel</button>
                                </td>
                              </tr>
                            ) : (
                              <tr
                                key={r.id}
                                onClick={() => !busy && onOpen(r.id)}
                                onContextMenu={(e) => openReportMenu(e, r)}
                                draggable={canEdit}
                                onDragStart={(e) => startDrag(e, { kind: "report", id: r.id })}
                              >
                                <td className="drive-table-name"><FileText /> {r.name}</td>
                                <td><span className="chip">{r.layoutMode}</span></td>
                                <td>{new Date(r.createdAtUtc).toLocaleDateString()}</td>
                                <td>{r.createdByEmail ?? "—"}</td>
                              </tr>
                            ),
                          )}
                        </tbody>
                      </table>
                    )}
                  </div>
                </div>
              </div>
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

      {scheduleReport && <ScheduleDialog report={scheduleReport} onClose={() => setScheduleReport(null)} />}
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
          <button className="tree-toggle" onClick={() => setOpen((o) => !o)} aria-label={open ? "Collapse" : "Expand"}>
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
            <button className="mini" onClick={() => onCommitRename(folder.id)}>Save</button>
            <button className="mini" onClick={onCancelRename}>Cancel</button>
          </div>
        ) : confirmFolderId === folder.id ? (
          <div className="tree-item tree-item-editing">
            <span>Delete “{folder.name}”?</span>
            <button className="mini danger" onClick={() => onConfirmDelete(folder.id)}>Delete</button>
            <button className="mini" onClick={onCancelConfirm}>Cancel</button>
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
