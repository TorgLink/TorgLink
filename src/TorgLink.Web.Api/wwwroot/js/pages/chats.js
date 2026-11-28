import { del, get, post } from "../api.js";
import { onTick, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function ChatsPage() {
  let rows = [];
  let q = "";

  const listEl = el("div");

  function renderList() {
    clear(listEl);
    const filtered = rows.filter(
      (c) =>
        !q ||
        c.nick.toLowerCase().includes(q.toLowerCase()) ||
        c.networkId.toLowerCase().includes(q.toLowerCase())
    );
    if (filtered.length === 0) {
      listEl.appendChild(el("div", { class: "empty" }, t("chats.empty")));
      return;
    }
    for (const c of filtered) {
      listEl.appendChild(
        el(
          "div",
          {
            class: "item",
            onclick: () => nav(`/app/chats/${c.id}`),
            oncontextmenu: async (e) => {
              e.preventDefault();
              const choice = window.prompt(`${c.nick}\n1 = ${t("delete")}\n2 = ${t("blacklist.add")}`, "1");
              if (choice === "1" && window.confirm(t("chats.delete_body", c.nick))) {
                await del(`/api/chats/${c.id}`);
                rows = rows.filter((x) => x.id !== c.id);
                renderList();
              }
              if (choice === "2" && window.confirm(t(c.isBot ? "blacklist.add_body_bot" : "blacklist.add_body", c.nick))) {
                await post(`/api/chats/${c.id}/block`);
                rows = rows.filter((x) => x.id !== c.id);
                renderList();
              }
            }
          },
          el("div", { class: "avatar", style: { background: c.avatar } }, c.initials),
          el(
            "div",
            { class: "meta" },
            el("div", { class: "title" }, el("span", null, c.nick), el("span", { class: "muted" }, c.time)),
            el("div", { class: "sub" }, c.preview || t("preview.none"))
          ),
          el("span", { class: "dot " + (c.online ? "on" : "off") })
        )
      );
    }
  }

  async function load() {
    try {
      rows = await get("/api/chats");
      console.log("[ChatsPage] loaded", rows.length, "chats", rows);
    } catch (e) {
      console.error("[ChatsPage] load failed", e);
      rows = [];
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
      { class: "search" },
      el("input", {
        placeholder: t("search"),
        value: q,
        oninput: (e) => {
          q = e.target.value;
          renderList();
        }
      })
    ),
    listEl,
    el("button", { class: "fab", title: t("network.add_chat"), onclick: () => nav("/app/add-chat") }, "+")
  );
  return { root, dispose: off };
}
