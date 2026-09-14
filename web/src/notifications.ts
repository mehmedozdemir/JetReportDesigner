/** Thin wrapper over the browser's built-in Notification API — no service worker, no push
 * subscription: this only fires while the app's tab is open (not necessarily focused).
 * Anything beyond that (notifications with the tab fully closed) needs real Web Push
 * infrastructure, which is a separate, bigger piece of work. */

export function notificationsSupported(): boolean {
  return typeof window !== "undefined" && "Notification" in window;
}

export function notificationPermission(): NotificationPermission | "unsupported" {
  return notificationsSupported() ? Notification.permission : "unsupported";
}

/** Must be called from a user gesture (a click handler) the first time, or the browser
 * silently ignores it. Safe to call repeatedly — once the user has answered, the browser
 * remembers and this just resolves immediately with that answer. */
export async function requestNotificationPermission(): Promise<NotificationPermission> {
  if (!notificationsSupported()) return "denied";
  try {
    return await Notification.requestPermission();
  } catch {
    return "denied";
  }
}

/** Fires an OS-level notification only when it would actually add information — permission
 * granted, and the tab isn't the one currently in front of the user (otherwise the in-app
 * toast already covers it). */
export function notifyIfBackgrounded(title: string, body: string): void {
  if (!notificationsSupported() || Notification.permission !== "granted") return;
  if (!document.hidden) return;
  try {
    new Notification(title, { body, tag: title });
  } catch {
    // Some browsers (mobile Safari, some locked-down contexts) throw rather than no-op — a
    // missed OS notification isn't worth surfacing an error for.
  }
}
