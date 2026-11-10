import { get, post } from "../api.js";
import { onTick, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function NetworkPage() {
  let nodes = [];

  const countEl = el("h3", { style: { padding: "8px 16px" } });
  const listEl = el("div");

  function renderList() {
    countEl.textContent = t("network.nodes_count", nodes.length);
    clear(listEl);
    for (const n of nodes) {
      listEl.appendChild(
        el(
          "div",
          {
            class: "item",
            onclick: async () => {
              if (!n.networkId) {
                window.alert(`${n.name} · ${n.hops}`);
                return;
              }
              try {
                const r = await post("/api/chats/open-peer", { networkId: n.networkId });
                nav(`/app/chats/${r.id}`);
              } catch (e) {
                window.alert(e instanceof Error ? e.message : t("network.error"));
              }
            }
          },
          el("div", { class: "avatar", style: { background: n.avatar } }, n.initials),
          el(
            "div",
            { class: "meta" },
            el("div", { class: "title" }, n.name),
            el("div", { class: "sub" }, `${n.networkId} · ${n.hops}`)
          ),
          el("span", { class: "dot " + (n.online ? "on" : "off") })
        )
      );
    }
  }

  async function load() {
    try {
      nodes = await get("/api/network");
    } catch {
      nodes = [];
    }
    renderList();
  }
  void load();
  const off = onTick(() => void load());

  const root = el(
    "div",
    null,
    el(
      "div",
      { class: "menu" },
      el("button", { class: "btn small", onclick: () => nav("/app/my-qr") }, t("network.my_qr")),
      el("button", { class: "btn small", onclick: () => nav("/app/add-chat") }, t("network.add_chat")),
      el(
        "button",
        {
          class: "btn small",
          onclick: async () => {
            const r = await get("/api/network/addresses");
            await navigator.clipboard.writeText(r.text);
            window.alert(t("copied.addresses"));
          }
        },
        t("network.my_addresses")
      ),
      el(
        "button",
        {
          class: "btn small",
          onclick: async () => {
            const r = await get("/api/network/keys");
            await navigator.clipboard.writeText(r.text);
            window.alert(t("copied.keys"));
          }
        },
        t("network.copy_key")
      ),
      el("button", { class: "btn small", onclick: () => nav("/app/servers") }, t("network.servers")),
      el("button", { class: "btn small", onclick: () => nav("/app/lan") }, t("lan.title"))
    ),
    countEl,
    listEl
  );
  return { root, dispose: off };
}
