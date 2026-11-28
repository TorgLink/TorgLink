import { del, get, post, postFile } from "../api.js";
import { onTick, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function ChatPage(chatId) {
  let chat = null;
  let items = [];
  let err = "";
  let lastCount = 0;

  const headerEl = el("div", { class: "header", style: { position: "sticky", top: 0, zIndex: 1 } });
  const safetyEl = el("div", {
    class: "muted",
    style: { padding: "8px 16px", fontSize: "12px", whiteSpace: "pre-wrap" },
    hidden: true
  });
  const msgsEl = el("div", { class: "msgs" });
  const bottom = el("div");
  const fileInput = el("input", { type: "file", hidden: true });
  const textarea = el("textarea", { placeholder: t("chat.message_ph"), rows: "1" });
  const errEl = el("div", { class: "empty err", hidden: true });

  function setErr(msg) {
    err = msg || "";
    if (!err) {
      errEl.hidden = true;
      return;
    }
    errEl.hidden = false;
    errEl.textContent = err.includes(".") ? t(err) : err;
  }

  function formatCommandLine(cmd) {
    const name = (cmd.command || "").trim();
    const args = Array.isArray(cmd.args) ? cmd.args : [];
    const argText = args
      .map((a) => {
        const n = (a.argName || "").trim();
        const ty = (a.argType || "").trim();
        if (!n) return ty ? `<${ty}>` : "?";
        if (!ty) return `<${n}>`;
        return `<${n}:${ty}>`;
      })
      .join(" ");
    const description = (cmd.description || "").trim();
    let line = name;
    if (argText) line += ` ${argText}`;
    if (description) line += ` — ${description}`;
    return line;
  }

  async function showBotCommands() {
    try {
      const data = await get(`/api/chats/${chatId}/commands`);
      const list = Array.isArray(data.commands) ? data.commands : [];
      const body =
        list.length === 0
          ? t("chat.commands_empty")
          : list.map(formatCommandLine).join("\n");
      window.alert(`${t("chat.commands_title")}\n\n${body}`);
    } catch (e) {
      window.alert(e instanceof Error ? e.message : t("chat.commands_fail"));
    }
  }

  function renderHeader() {
    clear(headerEl);
    if (!chat) return;
    headerEl.appendChild(
      el(
        "div",
        { class: "row" },
        el("div", { class: "avatar", style: { width: "36px", height: "36px", background: chat.avatar } }, chat.initials),
        el(
          "div",
          null,
          el("strong", null, chat.nick),
          el(
            "div",
            { class: "muted", style: { fontSize: "12px" } },
            `${chat.online ? t("online") : t("offline")} · ${t("chat.node", chat.networkId)}`
          )
        )
      )
    );
    const actions = el("div", { class: "row", style: { gap: "8px" } });
    if (chat.isBot) {
      actions.appendChild(
        el(
          "button",
          { class: "btn ghost small", onclick: () => void showBotCommands() },
          t("chat.commands")
        )
      );
    }
    actions.appendChild(
      el(
        "button",
        {
          class: "btn ghost small",
          onclick: () => {
            const menu = window.prompt(
              `${t("chat.clear_title")}=1\n${t("safety.emergency")}=2\n${t("chat.delete")}=3`,
              ""
            );
            void (async () => {
              if (menu === "1" && window.confirm(t("chat.clear_body"))) await post(`/api/chats/${chatId}/clear`);
              if (menu === "2" && window.confirm(t("safety.untrust_hint"))) await post(`/api/chats/${chatId}/untrust`);
              if (menu === "3" && window.confirm(t("chats.delete_body", chat.nick))) {
                await del(`/api/chats/${chatId}`);
                nav("/app/chats");
                return;
              }
              await load();
            })();
          }
        },
        "⋯"
      )
    );
    headerEl.appendChild(actions);
    safetyEl.hidden = !chat.safety;
    safetyEl.textContent = chat.safety || "";
  }

  function renderMessages() {
    // Reuse message DOM nodes by message id so media players keep their state.
    const existing = new Map();
    for (const child of Array.from(msgsEl.children)) {
      if (child === bottom) continue;
      const id = child.dataset && child.dataset.mid;
      if (id) existing.set(id, child);
    }

    const newItems = [];
    for (const m of items) {
      const key = String(m.id);
      let bubble = existing.get(key);
      if (bubble) {
        existing.delete(key);
        // Update only dynamic parts (time/delivery) without touching media elements.
        const timeEl = bubble.querySelector(".time");
        if (timeEl) timeEl.textContent = `${m.time} ${m.outgoing ? (m.delivery === "failed" ? "!" : m.delivery === "pending" ? "…" : "✓") : ""}`;
        // Failed button visibility may change
        const btn = bubble.querySelector("button.retry");
        if (btn) btn.style.display = m.delivery === "failed" ? "" : "none";
        newItems.push(bubble);
      } else {
        bubble = buildBubble(m);
        bubble.dataset.mid = key;
        newItems.push(bubble);
      }
    }

    // Remove bubbles for messages that disappeared (e.g. after clear)
    for (const old of existing.values()) old.remove();
    // Remove transient placeholders (loading / error) that have no mid
    for (const child of Array.from(msgsEl.children)) {
      if (child === bottom) continue;
      if (!(child.dataset && child.dataset.mid)) child.remove();
    }

    // Reorder / insert new items before bottom
    for (const node of newItems) {
      if (node.parentNode === msgsEl && node.nextSibling === bottom) continue;
      msgsEl.insertBefore(node, bottom);
    }

    if (items.length !== lastCount) {
      lastCount = items.length;
      requestAnimationFrame(() => bottom.scrollIntoView({ behavior: "smooth" }));
    }
  }

  function buildBubble(m) {
    const isMedia = (m.kind === "image" || m.kind === "voice" || m.kind === "video") && m.hasBlob;
    const bubble = el("div", {
      class: "bubble " + (m.outgoing ? "out" : "in") + (isMedia ? " media" : "")
    });
    if (m.kind === "image" && m.hasBlob) {
      bubble.appendChild(el("img", { class: "att", src: `/api/chats/${chatId}/messages/${m.id}/file`, alt: "" }));
    } else if (m.kind === "voice" && m.hasBlob) {
      bubble.appendChild(
        el(
          "div",
          null,
          el("div", { class: "muted", style: { fontSize: "12px" } },
            t("chat.caption.voice") + (m.fileName ? ` · ${m.fileName}` : "")),
          el("audio", {
            class: "att",
            controls: true,
            preload: "metadata",
            src: `/api/chats/${chatId}/messages/${m.id}/file`
          })
        )
      );
    } else if (m.kind === "video" && m.hasBlob) {
      bubble.appendChild(
        el(
          "div",
          null,
          el("div", { class: "muted", style: { fontSize: "12px" } },
            t("chat.caption.video") + (m.fileName ? ` · ${m.fileName}` : "")),
          el("video", {
            class: "att",
            controls: true,
            preload: "metadata",
            playsinline: true,
            src: `/api/chats/${chatId}/messages/${m.id}/file`
          })
        )
      );
    } else if (m.kind !== "text") {
      bubble.appendChild(
        el(
          "div",
          null,
          el(
            "div",
            null,
            (m.kind === "voice" ? t("chat.caption.voice") : m.kind === "video" ? t("chat.caption.video") : t("chat.caption.file")) +
              (m.fileName ? ` · ${m.fileName}` : "")
          ),
          m.hasBlob
            ? el("a", { href: `/api/chats/${chatId}/messages/${m.id}/file`, download: "" }, t("chat.save_doc"))
            : el(
                "button",
                {
                  class: "btn small",
                  onclick: async () => {
                    await post(`/api/chats/${chatId}/messages/${m.id}/download`);
                  }
                },
                t("chat.state.tap_download")
              )
        )
      );
    } else {
      bubble.appendChild(el("div", null, m.text));
    }
    bubble.appendChild(
      el(
        "div",
        { class: "time" },
        `${m.time} ${m.outgoing ? (m.delivery === "failed" ? "!" : m.delivery === "pending" ? "…" : "✓") : ""}`
      )
    );
    if (m.delivery === "failed") {
      bubble.appendChild(
        el(
          "button",
          {
            class: "btn small retry",
            onclick: () => post(`/api/chats/${chatId}/messages/${m.id}/retry`)
          },
          t("chat.state.failed")
        )
      );
    }
    return bubble;
  }

  async function load() {
    try {
      chat = await get(`/api/chats/${chatId}`);
      const page = await get(`/api/chats/${chatId}/messages?limit=80`);
      items = page.items;
      setErr("");
      renderHeader();
      renderMessages();
    } catch (e) {
      setErr(e instanceof Error ? e.message : "chats.open_failed");
      if (!chat) {
        clear(msgsEl);
        msgsEl.appendChild(el("div", { class: "empty" }, t("chat.state.loading")));
      }
    }
  }

  async function send() {
    const msg = textarea.value.trim();
    if (!msg) return;
    textarea.value = "";
    try {
      await post(`/api/chats/${chatId}/messages`, { text: msg });
      // Soft refresh: list API no longer pulls blobs, but avoid blocking UI on a full reload race.
      await load();
    } catch (e) {
      setErr(e instanceof Error ? e.message : t("error"));
      textarea.value = msg;
    }
  }

  textarea.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      void send();
    }
  });

  const kb = 1024;
  const mb = 1024 * 1024;
  function attachmentKind(file) {
    const type = (file.type || "").toLowerCase();
    const name = (file.name || "").toLowerCase();
    if (type.startsWith("image/") || /\.(jpe?g|png|gif)$/.test(name)) return "image";
    if (type.startsWith("video/") || /\.(mp4|mov|avi|wmv|webm|ogv)$/.test(name)) return "video";
    if (type.startsWith("audio/") || name.endsWith(".ogg")) return "audio";
    return "document";
  }

  function attachmentLimit(file, ultra) {
    const kind = attachmentKind(file);
    if (ultra) {
      if (kind === "image") return 50 * kb;
      return 200 * kb;
    }
    if (kind === "image") return 10 * mb;
    if (kind === "video") return 60 * mb;
    if (kind === "audio") return mb;
    return 20 * mb;
  }

  fileInput.addEventListener("change", async () => {
    const f = fileInput.files?.[0];
    fileInput.value = "";
    if (!f) return;
    let ultra = false;
    try {
      const settings = await get("/api/settings");
      ultra = settings?.trafficQuality === "UltraEconomy";
    } catch {
      ultra = false;
    }
    const limit = attachmentLimit(f, ultra);
    if (f.size > limit) {
      const limitMb = limit % mb === 0 ? limit / mb : Math.round((limit / mb) * 100) / 100;
      window.alert(t("chat.size_over", limitMb));
      return;
    }
    try {
      await postFile(`/api/chats/${chatId}/files`, f);
      await load();
    } catch (ex) {
      window.alert(ex instanceof Error ? ex.message : t("error"));
    }
  });

  let loadGen = 0;
  async function loadSafe() {
    const gen = ++loadGen;
    await load();
    // Drop overlapping reloads from SignalR ticks while a slow request is in flight.
    if (gen !== loadGen) return;
  }

  void loadSafe();
  const off = onTick(() => void loadSafe());

  // bottom sentinel must exist before first render (scroll target / insert anchor)
  msgsEl.appendChild(bottom);
  msgsEl.appendChild(el("div", { class: "empty" }, t("chat.state.loading")));

  const root = el(
    "div",
    { style: { display: "flex", flexDirection: "column", minHeight: "100%" } },
    errEl,
    headerEl,
    safetyEl,
    msgsEl,
    el(
      "div",
      { class: "composer" },
      el("button", { class: "btn small", onclick: () => fileInput.click() }, "+"),
      fileInput,
      textarea,
      el("button", { class: "btn small", onclick: () => void send() }, "→")
    )
  );
  return { root, dispose: off };
}
