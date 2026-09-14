import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { SharedReportView } from "./components/SharedReportView";
import "./styles.css";

// A share link (/share/:token) is a fully separate, unauthenticated page — it never
// touches the main app shell (auth state, designer store, tenant-scoped API calls).
const shareMatch = /^\/share\/([^/]+)\/?$/.exec(window.location.pathname);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {shareMatch ? <SharedReportView token={shareMatch[1]} /> : <App />}
  </React.StrictMode>,
);
