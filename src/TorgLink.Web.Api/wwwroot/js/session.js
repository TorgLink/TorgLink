import { get, post } from "./api.js";
import { connectHub } from "./hub.js";
import { setLang, getLang, t } from "./i18n.js";
import { applyTheme } from "./theme.js";
import {
  ensureNotificationPermission,
  notifyIncomingMessage,
  notifyChatCreated
} from "./notifications.js";

export const session = {
  ready: false,
  languageChosen: false,
  lang: "Russian",
  theme: "DarkFlame",
  user: null,
  tick: 0
};

const listeners = new Set();
let hubConn = null;

export function onTick(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}

export function bump() {
  session.tick++;
  for (const fn of [...listeners]) fn(session.tick);
}

export async function refresh() {
  const b = await get("/api/bootstrap");
  setLang(b.language);
  applyTheme(b.theme);
  session.lang = b.language;
  session.theme = b.theme;
  session.languageChosen = b.languageChosen;
  const prevUserId = session.user?.id ?? null;
  session.user = b.user;
  session.ready = true;
  if (session.user && prevUserId !== session.user.id) startHub();
  if (!session.user) stopHub();
  bump();
}

function startHub() {
  stopHub();
  ensureNotificationPermission();
  hubConn = connectHub({
    chatsChanged: bump,
    messagesChanged: bump,
    presenceChanged: bump,
    incomingMessage: notifyIncomingMessage,
    chatCreated: notifyChatCreated,
    keyChanged: (p) =>
      window.alert(t("safety.key_change_body", p.peerNickname, p.previousSafetyNumber, p.newSafetyNumber)),
    trustThreat: (p) =>
      window.alert(t("security.threat_body", p.baseUrl, p.expectedFingerprint, p.actualFingerprint)),
    meshFailover: () => window.alert(t("safety.mesh_body"))
  });
}

function stopHub() {
  if (hubConn) {
    void hubConn.stop();
    hubConn = null;
  }
}

export async function setLanguage(next) {
  await post("/api/prefs/language", { language: next });
  setLang(next);
  session.lang = next;
  session.languageChosen = true;
  bump();
}

export async function setThemeId(next) {
  await post("/api/prefs/theme", { theme: next });
  applyTheme(next);
  session.theme = next;
  bump();
}

export async function logout() {
  try {
    await post("/api/auth/logout");
  } finally {
    session.user = null;
    stopHub();
    bump();
  }
}

/**
 * Call from beforeunload/pagehide: best-effort logout that survives page close.
 * Uses sendBeacon (browser delivers it even after unload starts).
 * Only fires when the page is actually being unloaded/hidden, not on internal navigation.
 */
export function logoutOnClose() {
  if (!session.user) return;
  session.user = null;
  stopHub();
  const blob = new Blob(["{}"], { type: "application/json" });
  navigator.sendBeacon("/api/auth/logout", blob);
  bump();
}

export { getLang, t };