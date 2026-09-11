import { useEffect, useMemo, useState } from "react";
import {
  ChevronRight,
  FileBarChart2,
  FileText,
  Folder,
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
import { TeamPage } from "./TeamPage";

type View = "reports" | "team";
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

  const breadcrumb = useMemo(() => {
    const chain: FolderSummary[] = [];
    let cur = currentFolderId ? folderById.get(currentFolderId) : undefined;
    while (cur) {
      chain.unshift(cur);
      cur = cur.parentFolderId ? folderById.get(cur.parentFolderId) : undefined;
    }
    return chain;
  }, [currentFolderId, folderById]);

  const subfolders = useMemo(
    () =>
      [...folders]
        .filter((f) => (f.parentFolderId ?? null) === currentFolderId)
        .sort((a, b) => a.name.localeCompare(b.name)),
    [folders, currentFolderId],
  );

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

              {!isSearching && (
                <div className="folder-bar">
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

                  {canEdit &&
                    (creatingFolder ? (
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
                    ))}
                </div>
              )}

              {folderError && (
                <p className="hint" style={{ color: "var(--error)" }}>{folderError}</p>
              )}

              {!isSearching && subfolders.length > 0 && (
                <ul className="start-list">
                  {subfolders.map((f) => (
                    <li key={f.id} className="start-row">
                      {renamingFolderId === f.id ? (
                        <div className="start-row-confirm">
                          <input
                            autoFocus
                            value={renameValue}
                            onChange={(e) => setRenameValue(e.target.value)}
                            onKeyDown={(e) => e.key === "Enter" && void commitRename(f.id)}
                          />
                          <button className="mini" onClick={() => void commitRename(f.id)}>Save</button>
                          <button className="mini" onClick={() => setRenamingFolderId(null)}>Cancel</button>
                        </div>
                      ) : confirmFolderId === f.id ? (
                        <div className="start-row-confirm">
                          <span>Delete “{f.name}”?</span>
                          <button className="mini danger" onClick={() => void doDeleteFolder(f.id)}>Delete</button>
                          <button className="mini" onClick={() => setConfirmFolderId(null)}>Cancel</button>
                        </div>
                      ) : (
                        <>
                          <button className="start-row-main" onClick={() => setCurrentFolderId(f.id)}>
                            <Folder />
                            <span className="start-row-name">{f.name}</span>
                          </button>
                          {canEdit && (
                            <>
                              <button
                                className="mini"
                                title="Rename folder"
                                aria-label={`Rename ${f.name}`}
                                onClick={() => {
                                  setRenamingFolderId(f.id);
                                  setRenameValue(f.name);
                                }}
                              >
                                <Pencil />
                              </button>
                              <button
                                className="mini danger"
                                title="Delete folder"
                                aria-label={`Delete ${f.name}`}
                                onClick={() => setConfirmFolderId(f.id)}
                              >
                                <Trash2 />
                              </button>
                            </>
                          )}
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}

              {reportsShown.length === 0 ? (
                <div className="start-empty">
                  <FileText />
                  <div>
                    {reports.length === 0
                      ? "No reports yet."
                      : isSearching
                        ? "No reports match your search."
                        : "No reports in this folder."}
                  </div>
                  {reports.length === 0 && <p>Start from a blank report or a sample above.</p>}
                </div>
              ) : (
                <ul className="start-list">
                  {reportsShown.map((r) => (
                    <li key={r.id} className="start-row">
                      {confirmId === r.id ? (
                        <div className="start-row-confirm">
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
                          <button className="mini" onClick={() => setConfirmId(null)}>
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <>
                          <button className="start-row-main" onClick={() => onOpen(r.id)} disabled={busy}>
                            <FileText />
                            <span className="start-row-name">{r.name}</span>
                            <span className="start-row-meta">
                              <span className="chip">{r.layoutMode}</span>
                              <span>{timeAgo(r.updatedAtUtc)}</span>
                            </span>
                          </button>
                          {canEdit && (
                            <>
                              <select
                                className="folder-move-select"
                                title="Move to folder"
                                aria-label={`Move ${r.name} to folder`}
                                value={r.folderId ?? ""}
                                onChange={(e) => onMoveToFolder(r.id, e.target.value || null)}
                              >
                                <option value="">— Root —</option>
                                {folderOptions.map((f) => (
                                  <option key={f.id} value={f.id}>{f.label}</option>
                                ))}
                              </select>
                              <button
                                className="mini danger"
                                title="Delete report"
                                aria-label={`Delete ${r.name}`}
                                onClick={() => setConfirmId(r.id)}
                              >
                                <Trash2 />
                              </button>
                            </>
                          )}
                        </>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>
    </div>
  );
}
