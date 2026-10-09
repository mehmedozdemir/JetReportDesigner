import { useEffect } from "react";
import { create } from "zustand";
import { api, type ReportJob } from "./api";

const POLL_MS = 5000;

/** How many of the newest jobs the feed keeps. Everything that reads this feed — the job panel,
 * the Jobs nav badge and the finished-job notifier — only cares about what changed recently; the
 * full history lives on the Jobs page, which runs its own filtered, paged query. */
const WINDOW = 20;

const isActive = (j: ReportJob) => j.status === "Queued" || j.status === "Running";

interface JobFeedState {
  jobs: ReportJob[];
  /** True once the first poll has answered. */
  loaded: boolean;
  /**
   * The jobs the bottom-right panel is following: every job seen running in this tab, until the
   * panel is closed. Finished ones stay listed (with their Download button) so the result is one
   * click away; closing the panel lets them go.
   */
  watched: string[];
  /** The panel is showing (it opens by itself when a job starts). */
  dockOpen: boolean;
  /** Shrunk to its header bar. */
  dockCollapsed: boolean;
}

/** The newest jobs, polled once per tab and shared by everything that shows job status. */
export const useJobFeed = create<JobFeedState>(() => ({
  jobs: [],
  loaded: false,
  watched: [],
  dockOpen: false,
  dockCollapsed: false,
}));

let subscribers = 0;
let timer: number | undefined;

const refresh = async () => {
  try {
    const page = await api.listJobs({ take: WINDOW });
    const { watched, dockOpen } = useJobFeed.getState();
    // A job running that the panel isn't following yet (started here, in another tab, or by a
    // schedule): follow it and bring the panel up.
    const fresh = page.items.filter((j) => isActive(j) && !watched.includes(j.id)).map((j) => j.id);
    useJobFeed.setState({
      jobs: page.items,
      loaded: true,
      watched: fresh.length ? [...fresh, ...watched] : watched,
      dockOpen: dockOpen || fresh.length > 0,
      ...(fresh.length ? { dockCollapsed: false } : {}),
    });
  } catch {
    // Transient network hiccup — the next tick tries again.
  }
};

/** Call right after enqueuing: pull the new row in straight away so the panel shows it now
 * rather than on the next tick. Deliberately not a navigation — the point of running a report
 * in the background is that you get to stay where you are. */
export const announceEnqueued = () => {
  useJobFeed.setState({ dockOpen: true, dockCollapsed: false });
  void refresh();
};

export const setDockCollapsed = (collapsed: boolean) => useJobFeed.setState({ dockCollapsed: collapsed });

/** Closing forgets the finished jobs. Anything still running stays followed, so it is listed again the next time the panel opens (when another job starts). */
export const closeDock = () => {
  const { jobs, watched } = useJobFeed.getState();
  const stillRunning = watched.filter((id) => jobs.some((j) => j.id === id && isActive(j)));
  useJobFeed.setState({ dockOpen: false, watched: stillRunning });
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
 * higher, so a badge can say "20+" rather than quietly under-reporting a busy queue. */
export function useActiveJobCount() {
  const jobs = useJobFeed((s) => s.jobs);
  const count = jobs.filter(isActive).length;
  return { count, capped: count >= WINDOW };
}

export { isActive, WINDOW as JOB_FEED_WINDOW };
