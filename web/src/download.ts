/** Triggers a browser save-as for in-memory content — the export/render blobs that come
 * back from the API rather than a real file on disk. */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
