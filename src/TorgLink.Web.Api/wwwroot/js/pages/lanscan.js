import { get, post } from "../api.js";
import { onTick, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function LanScanPage() {
  let rows = [];
  let busy = false;

  const listEl = el("div");
  const scanBtn = el("button", { class: "btn small" }, t("lan.scan"));

  function renderList() {
    clear(listEl);
    for (const r of rows) {
      listEl.appendChild(
        el(
          "div",
          {
            class: "item",
            ondblclick: async () => {
              if (!r.networkId) {
                window.alert(`${r.nickname} · ${r.transport}`);
                return;
              }
              try {
                const chat = await post("/api/chats/open-peer", { networkId: r.networkId });
                nav(`/app/chats/${chat.id}`);
              } catch (e) {
                window.alert(e instanceof Error ? e.message : t("network.error"));
              }
            }
          },
          el(
            "div",
            { class: "meta" },
            el(
              "div",
              { class: "title" },
              r.nickname,
              el("span", { class: "dot " + (r.online ? "on" : "off") })
            ),
            el("div", { class: "sub" }, t("lan.detail", r.networkId, r.transport, t("lan.last", r.lastSeen)))
          )
        )
      );
    }
  }

  async function load() {
    try {
      rows = await get("/api/lan");
    } catch {
      rows = [];
    }
    renderList();
  }
  void load();
  const off = onTick(() => void load());

  scanBtn.addEventListener("click", async () => {
    busy = true;
    scanBtn.disabled = true;
    try {
      await post("/api/lan/scan");
      await load();
    } finally {
      busy = false;
      scanBtn.disabled = false;
    }
  });

  const root = el(
    "div",
    null,
    el(
      "div",
      { class: "search row" },
      el("p", { class: "muted", style: { flex: 1 } }, t("lan.hint")),
      scanBtn
    ),
    listEl
  );
  return { root, dispose: off };
}
