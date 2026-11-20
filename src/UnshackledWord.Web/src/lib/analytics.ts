/**
 * Privacy-friendly usage statistics (roadmap 0021, Umami, cookieless). Only the events below exist, with
 * bucketed or enumerated values and never free text (no search query, no word, no user data).
 */

type Count = "0" | "1-10" | ">10";
type LevelBucket = "1-99" | "100-199" | "200" | "201-500" | ">500";

export interface AnalyticsEvents {
  search: { kind: "verses" | "originals" | "references"; results: Count };
  "revision-level": { level: LevelBucket };
  "word-detail": { language: "grc" | "hbo" };
  "chapter-open": { testament: "ot" | "nt" };
  login: Record<string, never>;
}

interface UmamiTracker {
  track: (name: string, data?: Record<string, unknown>) => unknown;
}

interface BeforeSendPayload {
  url?: string;
}

declare global {
  interface Window {
    umami?: UmamiTracker;
    uwBeforeSend?: (type: string, payload: BeforeSendPayload) => BeforeSendPayload | false;
  }
}

export function resultsBucket(count: number): Count {
  if (count <= 0) return "0";
  return count <= 10 ? "1-10" : ">10";
}

export function levelBucket(level: number): LevelBucket {
  if (level < 100) return "1-99";
  if (level < 200) return "100-199";
  if (level === 200) return "200";
  return level <= 500 ? "201-500" : ">500";
}

/** No-op when the tracker is missing (dev build, blocker, Do Not Track). */
export function track<K extends keyof AnalyticsEvents>(name: K, data?: AnalyticsEvents[K]): void {
  try {
    window.umami?.track(name, data);
  } catch {
    // statistics must never break the app
  }
}

const REVISION_LEVEL_DELAY_MS = 1500;
let levelTimer: ReturnType<typeof setTimeout> | undefined;

/** The slider fires on every tick; only the level it rests on counts. */
export function trackRevisionLevel(level: number): void {
  clearTimeout(levelTimer);
  levelTimer = setTimeout(
    () => track("revision-level", { level: levelBucket(level) }),
    REVISION_LEVEL_DELAY_MS,
  );
}

/** Maintainer and admin areas: their clicks must not skew reader numbers. */
const STAFF_PREFIXES = ["/wartung", "/admin", "/edit"];

const isStaffPath = (url: string | undefined): boolean => {
  if (!url) return false;
  const path = url.replace(/^[a-z][a-z0-9+.-]*:\/\/[^/]+/i, "").split(/[?#]/)[0] ?? "";
  return STAFF_PREFIXES.some((p) => path === p || path.startsWith(`${p}/`));
};

/** Remembered per browser, so the first page view after a reload is dropped before the session check returns. */
const STAFF_KEY = "uw.analytics.staff";

const readStaffFlag = (): boolean => {
  try {
    return window.localStorage.getItem(STAFF_KEY) === "1";
  } catch {
    return false;
  }
};

let staffSession = readStaffFlag();

/** Called by the auth layer: true when the signed-in user is Maintainer or Admin. */
export function setStaffSession(staff: boolean): void {
  staffSession = staff;
  try {
    if (staff) window.localStorage.setItem(STAFF_KEY, "1");
    else window.localStorage.removeItem(STAFF_KEY);
  } catch {
    // storage blocked: the in-memory flag still applies
  }
}

/** Umami `data-before-send` hook: returning false drops the event. */
export function uwBeforeSend(_type: string, payload: BeforeSendPayload): BeforeSendPayload | false {
  if (staffSession) return false;
  if (isStaffPath(payload.url) || isStaffPath(window.location.pathname)) return false;
  return payload;
}

window.uwBeforeSend = uwBeforeSend;
