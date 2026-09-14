import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { App } from "./App";
import { SharedReportView } from "./components/SharedReportView";
import "./styles.css";

// A share link (/share/:token) is a fully separate, unauthenticated page — it never
// touches the main app shell (auth state, designer store, tenant-scoped API calls).
const shareMatch = /^\/share\/([^/]+)\/?$/.exec(window.location.pathname);

// Every path below renders the same <App/> — it branches on useLocation()/useParams() itself
// (see App.tsx) rather than each route owning its own component, since most of the app's
// handlers (save, export, create, …) are shared across every screen regardless of which one
// is showing.
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {shareMatch ? (
      <SharedReportView token={shareMatch[1]} />
    ) : (
      <BrowserRouter>
        <Routes>
          <Route path="/reports/:id/design" element={<App />} />
          <Route path="/reports/:id/preview" element={<App />} />
          <Route path="/settings" element={<App />} />
          <Route path="/reports" element={<App />} />
          <Route path="/jobs" element={<App />} />
          <Route path="/team" element={<App />} />
          <Route path="/email-settings" element={<App />} />
          <Route path="/schedules" element={<App />} />
          <Route path="*" element={<Navigate to="/reports" replace />} />
        </Routes>
      </BrowserRouter>
    )}
  </React.StrictMode>,
);
