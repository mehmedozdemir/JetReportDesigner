import { useEffect } from "react";
import { create } from "zustand";
import { api, type ReportJob } from "./api";

const POLL_MS = 5000;

/** How many of the newest jobs the feed keeps. Everything that reads this feed — the tray's
 * list and the finished-job notifier — only cares about what changed recently; the full
 * history lives on the Jobs page, which runs its own filtered, paged query. */
const WINDOW = 20;

/** How long the tray stays open by itself after you send something to the background. Long
 * enough to see the row appear, short enough not to sit over your work. */
const FLASH_MS = 4000;

const isActive = (j: ReportJob) => j.status === "Queued" || j.status === "Running";

interface JobFeedState {
  jobs: ReportJob[];
  /** True once the first poll has answered — the tray shouldn't claim "no jobs" before then. */
  loaded: boolean;
  trayOpen: boolean;
}

/** The newest jobs, polled once per tab. Before this existed the tray, the nav badge and the
 * notifier each ran their own timer against /api/jobs, so every open tab asked three times per
 * tick for nearly the same rows. */
export const useJobFeed = create<JobFeedState>(() => ({
  jobs: [],
  loaded: false,
  trayOpen: false,
}));

let subscribers = 0;
let timer: number | undefined;
let autoClose: number | undefined;

const refresh = async () => {
  try {
    const page = await api.listJobs({ take: WINDOW });
    useJobFeed.setState({ jobs: page.items, loaded: true });
  } catch {
    // Transient network hiccup — the next tick tries again. Blanking the tray would be worse.
  }
};

const cancelAutoClose = () => {
  if (autoClose !== undefined) {
    window.clearTimeout(autoClose);
    autoClose = undefined;
  }
};

export const setTrayOpen = (open: boolean) => {
  cancelAutoClose();
  useJobFeed.setState({ trayOpen: open });
};

/** Stops the flash from closing the panel out from under someone who is reading it. */
export const holdTrayOpen = cancelAutoClose;

/** Call right after enqueuing: pull the new row in and show the tray briefly, the way a browser
 * flashes its downloads panel. Deliberately not a navigation — the point of running a report in
 * the background is that you get to stay where you are. */
export const announceEnqueued = () => {
  void refresh();
  cancelAutoClose();
  useJobFeed.setState({ trayOpen: true });
  autoClose = window.setTimeout(() => useJobFeed.setState({ trayOpen: false }), FLASH_MS);
};

/** Drives the single shared poller. Every component that reads the feed calls this; the timer
 * starts with the first of them and stops when the last one unmounts. */
export function useJobFeedPolling() {
  useEffect(() => {
    subscribers += 1;
    if (subscribers === 1) {
      void refresh();
      timer = window.setInterval(() => void refresh(), POLL_MS);
    }
    return () => {
      subscribers -= 1;
      if (subscribers === 0) {
        window.clearInterval(timer);
        timer = undefined;
      }
    };
  }, []);
}

/** Unfinished jobs among the newest `WINDOW`. `capped` says the real number may be
 * higher, so the badge can say "20+" rather than quietly under-reporting a busy queue. */
export function useActiveJobCount() {
  const jobs = useJobFeed((s) => s.jobs);
  const count = jobs.filter(isActive).length;
  return { count, capped: count >= WINDOW };
}

export { isActive, WINDOW as JOB_FEED_WINDOW };
