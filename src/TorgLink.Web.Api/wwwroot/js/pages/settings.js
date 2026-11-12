import { get, post } from "../api.js";
import { languages } from "../i18n.js";
import { session, setLanguage, setThemeId, logout, t } from "../session.js";
import { themes } from "../theme.js";
import { nav, render } from "../router.js";
import { el, clear } from "../dom.js";

const themeKeys = {
  DarkFlame: "theme.dark",
  Night: "theme.night",
  LightFlame: "theme.light",
  ColdBlue: "theme.cold",
  Forest: "theme.forest",
  Mono: "theme.mono"
};

export function SettingsPage() {
  const root = el("div", { class: "screen wide stack" }, el("div", { class: "empty" }, t("chat.state.loading")));

  function themeLabel(id) {
    return t(themeKeys[id] ?? "theme.mono");
  }

  async function save(patch) {
    await post("/api/settings", patch);
    await load();
  }

  async function load() {
    const s = await get("/api/settings");
    clear(root);

    const mb = s.storageBytes / (1024 * 1024);
    const storage = mb >= 1024 ? t("settings.storage_gb", mb / 1024) : t("settings.storage_mb", mb);

    // profile
    root.appendChild(
      el(
        "div",
        { class: "row" },
        el("div", { class: "avatar", style: { background: s.avatar } }, s.initials),
        el("div", null, el("strong", null, s.nick), el("div", { class: "muted" }, s.networkId))
      )
    );

    // appearance
    const langSelect = el(
      "select",
      {
        style: { marginTop: "8px" },
        onchange: async (e) => {
          await setLanguage(e.target.value);
          render();
        }
      },
      languages.map((l) => {
        const o = el("option", { value: l.id }, l.name);
        if (l.id === session.lang) o.selected = true;
        return o;
      })
    );

    root.appendChild(
      el(
        "div",
        { class: "card" },
        el("div", { class: "muted" }, t("settings.appearance")),
        el("label", null, t("lang.section"), langSelect),
        el(
          "div",
          { class: "row", style: { flexWrap: "wrap", marginTop: "12px" } },
          themes.map((th) =>
            el(
              "button",
              {
                class: "btn small",
                style: { background: th.accent, opacity: session.theme === th.id ? "1" : "0.55" },
                onclick: () => void setThemeId(th.id).then(render)
              },
              themeLabel(th.id)
            )
          )
        )
      )
    );

    // toggles
    const sw = (label, checked, key) =>
      el(
        "div",
        { class: "switch" },
        el("span", null, label),
        el("input", {
          type: "checkbox",
          checked,
          onchange: (e) => void save({ [key]: e.target.checked })
        })
      );

    const quality = el(
      "select",
      {
        style: { marginTop: "8px" },
        onchange: (e) => void save({ trafficQuality: e.target.value })
      },
      [
        ["Normal", t("economy.mode.normal")],
        ["Economy", t("economy.mode.economy")],
        ["UltraEconomy", t("economy.mode.ultra")]
      ].map(([v, label]) => {
        const o = el("option", { value: v }, label);
        if (s.trafficQuality === v) o.selected = true;
        return o;
      })
    );

    root.appendChild(
      el(
        "div",
        { class: "card" },
        sw(t("settings.lan"), s.lan, "lan"),
        sw(t("settings.bluetooth"), s.bluetooth, "bluetooth"),
        sw(t("settings.routing"), s.routing, "routing"),
        el("label", null, t("settings.economy"), quality)
      )
    );

    // LoRa / Meshtastic
    try {
      const [lora, jurisdictions, peers, portNames] = await Promise.all([
        get("/api/lora"),
        get("/api/lora/jurisdictions"),
        get("/api/lora/peers"),
        get("/api/lora/ports")
      ]);
      const ports = portNames?.length ? portNames : ["COM1", "COM2", "COM3", "COM4"];
      const com = el(
        "select",
        { style: { marginTop: "8px", width: "100%" } },
        ports.map((name) => {
          const o = el("option", { value: name }, name);
          if (String(name).toLowerCase() === String(lora.comPort || "").toLowerCase()) o.selected = true;
          return o;
        })
      );
      if (!lora.comPort) com.selectedIndex = 0;
      const powerChoices = lora.txPowerChoices?.length
        ? lora.txPowerChoices
        : Array.from({ length: 18 }, (_, i) => 5 + i);
      const defaultPowerFor = (code) => {
        const j = (jurisdictions || []).find((x) => x.code === code);
        if (!j || j.isUnset || !(j.powerMaxDbm > 0)) return 22;
        return Math.min(22, Math.max(5, j.powerMaxDbm));
      };
      const power = el(
        "select",
        { style: { marginTop: "8px", width: "100%" } },
        powerChoices.map((dbm) => {
          const o = el("option", { value: String(dbm) }, `${dbm} dBm`);
          if (Number(dbm) === Number(lora.txPowerDbm)) o.selected = true;
          return o;
        })
      );
      const jur = el(
        "select",
        {
          style: { marginTop: "8px", width: "100%" },
          onchange: () => {
            const next = defaultPowerFor(jur.value);
            power.value = String(next);
          }
        },
        (jurisdictions || []).map((j) => {
          const o = el("option", { value: j.code }, j.code);
          if (j.code === lora.jurisdictionCode) o.selected = true;
          return o;
        })
      );
      const presetChoices = lora.modemPresets || [];
      const rangeOrder = ["Short", "Medium", "Long"];
      const selectedPreset =
        presetChoices.find((c) => c.name === lora.modemPreset) ||
        presetChoices.find((c) => c.name === "LONG_FAST") ||
        presetChoices[0];
      let presetRange = selectedPreset?.range || "Long";
      let presetSpeed = selectedPreset?.speed || "Fast";
      const speedsFor = (range) => presetChoices.filter((c) => c.range === range);
      const snapSpeed = (range, speed) =>
        speedsFor(range).some((c) => c.speed === speed) ? speed : "Fast";
      const currentPreset = () =>
        speedsFor(presetRange).find((c) => c.speed === presetSpeed) || speedsFor(presetRange)[0];
      const rangeLabel = (range) => t(`lora.range.${String(range).toLowerCase()}`);
      const speedLabel = (speed) => t(`lora.speed.${String(speed).toLowerCase()}`);
      const presetSummary = el("div", { class: "muted", style: { marginTop: "6px" } }, "");
      const rangeSel = el("select", { style: { marginTop: "8px", width: "100%" } });
      const speedSel = el("select", { style: { marginTop: "8px", width: "100%" } });
      function paintPresetSpeed() {
        presetSpeed = snapSpeed(presetRange, presetSpeed);
        clear(speedSel);
        for (const c of speedsFor(presetRange)) {
          const o = el(
            "option",
            { value: c.speed },
            t("lora.preset.speed_item", speedLabel(c.speed), c.kilobitsText)
          );
          if (c.speed === presetSpeed) o.selected = true;
          speedSel.appendChild(o);
        }
        const cur = currentPreset();
        presetSummary.textContent = cur
          ? t("lora.preset.summary", rangeLabel(cur.range), speedLabel(cur.speed), cur.kilobitsText)
          : "";
      }
      function paintPresetRange() {
        clear(rangeSel);
        for (const range of rangeOrder) {
          if (!speedsFor(range).length) continue;
          const o = el("option", { value: range }, rangeLabel(range));
          if (range === presetRange) o.selected = true;
          rangeSel.appendChild(o);
        }
      }
      rangeSel.addEventListener("change", () => {
        presetRange = rangeSel.value;
        presetSpeed = snapSpeed(presetRange, presetSpeed);
        paintPresetSpeed();
      });
      speedSel.addEventListener("change", () => {
        presetSpeed = speedSel.value;
        paintPresetSpeed();
      });
      paintPresetRange();
      paintPresetSpeed();
      const enabled = el("input", { type: "checkbox", checked: !!lora.enabled });
      const status = el(
        "div",
        { class: "muted", style: { marginTop: "8px" } },
        lora.running ? "LoRa: running" : "LoRa: off",
        ` · max binary ${Math.round((lora.maxBinaryBytes || 20480) / 1024)} KB`,
        peers?.length ? ` · peers ${peers.length}` : ""
      );
      root.appendChild(
        el(
          "div",
          { class: "card" },
          el("div", { class: "muted" }, "LoRa / Meshtastic"),
          el("div", { class: "switch" }, el("span", null, "Enable"), enabled),
          el("label", null, "COM port", com),
          el("label", null, "Jurisdiction", jur),
          el("label", null, "TX power", power),
          el("label", null, t("lora.range"), rangeSel),
          el("label", null, t("lora.speed"), speedSel),
          presetSummary,
          el("div", { class: "muted" }, t("lora.preset.peers")),
          status,
          el(
            "button",
            {
              class: "btn",
              style: { marginTop: "10px" },
              onclick: async () => {
                await post("/api/lora", {
                  enabled: enabled.checked,
                  comPort: com.value,
                  baudRate: lora.baudRate || 115200,
                  jurisdictionCode: jur.value,
                  txPowerDbm: Number(power.value) || 22,
                  modemPreset: currentPreset()?.name || "LONG_FAST"
                });
                await load();
              }
            },
            "Apply LoRa"
          )
        )
      );
    } catch {
      // LoRa API optional if host failed to start
    }

    // links
    const linkrow = (label, to) => el("div", { class: "linkrow", onclick: () => nav(to) }, label);
    root.appendChild(
      el(
        "div",
        { class: "card" },
        linkrow(t("settings.routing_open"), "/app/routing"),
        linkrow(t("network.servers"), "/app/servers"),
        linkrow(t("blacklist.title"), "/app/blacklist"),
        linkrow(t("settings.logs"), "/app/logs"),
        el("div", { class: "muted" }, `${t("settings.storage")}: ${storage}`)
      )
    );

    // about
    root.appendChild(
      el(
        "div",
        { class: "card" },
        el("div", { class: "muted" }, t("settings.about")),
        el("p", { style: { whiteSpace: "pre-wrap" } }, t("settings.about_body"))
      )
    );

    root.appendChild(
      el(
        "button",
        {
          class: "btn danger",
          onclick: async () => {
            try {
              await logout();
            } finally {
              nav("/login");
            }
          }
        },
        t("settings.logout")
      )
    );
  }

  void load();
  return root;
}
