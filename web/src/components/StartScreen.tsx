import { useEffect, useMemo, useState } from "react";
import {
  ChevronDown,
  ChevronRight,
  FileBarChart2,
  FileText,
  Folder,
  FolderOpen,
  FolderPlus,
  LayoutGrid,
  Pencil,
  Rows3,
  Search,
  SquareDashed,
  Trash2,
  Users,
  X,
} from "lucide-react";
import { api } from "../api";
import { isDesigner, useAuth } from "../auth";
import { timeAgo } from "../time";
import type { FolderSummary, ReportDefinition, ReportSummary } from "../types";
import { ContextMenu, type MenuItem } from "./ContextMenu";
import { TeamPage } from "./TeamPage";

type View = "reports" | "team";
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
  onClose,
}: {
  reports: ReportSummary[];
  samples: { name: string; definition: ReportDefinition }[];
  busy: boolean;
  onBlank: (mode: "free" | "banded") => void;
  onSample: (name: string) => void;
  onOpen: (id: string) => void;
  onDelete: (id: string) => void;
  onMoveToFolder: (id: string, folderId: string | null) => void;
  onClose?: () => void;
}) {
  const [query, setQuery] = useState("");
  const [confirmId, setConfirmId] = useState<string | null>(null);
  const [view, setView] = useState<View>("reports");
  const canEdit = isDesigner(useAuth((s) => s.user));

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

  const openReportMenu = (e: React.MouseEvent, r: ReportSummary) => {
    e.preventDefault();
    setMenu({
      x: e.clientX,
      y: e.clientY,
      items: [
        { label: "Open", icon: FolderOpen, onClick: () => onOpen(r.id) },
        ...(canEdit
          ? ([
              { label: "Move to", children: moveToSubmenu(r) },
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
        </div>

        {canEdit && (
          <div className="segmented" role="group" aria-label="Start screen section">
            <button className={view === "reports" ? "on" : ""} onClick={() => setView("reports")}>
              <LayoutGrid size={14} /> Reports
            </button>
            <button className={view === "team" ? "on" : ""} onClick={() => setView("team")}>
              <Users size={14} /> Team
            </button>
          </div>
        )}

        {onClose && (
          <button className="btn icon" onClick={onClose} title="Close" aria-label="Close start screen">
            <X />
          </button>
        )}
      </header>

      <div className="start-scroll">
        {view === "team" ? (
          <TeamPage />
        ) : (
          <>
            <section className="start-section">
              <h3>New</h3>
              {!canEdit && <p className="hint">Viewer role — sign in as a Designer to create reports.</p>}
              <div className="start-cards">
                <button className="start-card" onClick={() => onBlank("free")} disabled={busy || !canEdit}>
                  <span className="start-card-icon"><SquareDashed /></span>
                  <span className="start-card-title">Blank — Free layout</span>
                  <span className="start-card-sub">Place elements anywhere on a fixed canvas</span>
                </button>
                <button className="start-card" onClick={() => onBlank("banded")} disabled={busy || !canEdit}>
                  <span className="start-card-icon"><Rows3 /></span>
                  <span className="start-card-title">Blank — Banded report</span>
                  <span className="start-card-sub">Header / detail / footer bands that repeat per row</span>
                </button>
                {samples.map((s) => (
                  <button
                    key={s.name}
                    className="start-card tpl"
                    onClick={() => onSample(s.name)}
                    disabled={busy || !canEdit}
                  >
                    <span className="start-card-icon"><FileBarChart2 /></span>
                    <span className="start-card-title">{s.name}</span>
                    <span className="start-card-sub">Sample template</span>
                    <span className="start-card-tag">Sample</span>
                  </button>
                ))}
              </div>
            </section>

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

              {folderError && (
                <p className="hint" style={{ color: "var(--error)" }}>{folderError}</p>
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
                    ) : (
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
                    )}
                  </div>
                </div>
              </div>
            </section>
          </>
        )}
      </div>

      {menu && <ContextMenu x={menu.x} y={menu.y} items={menu.items} onClose={() => setMenu(null)} />}
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
