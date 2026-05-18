document.querySelectorAll("[data-rank-toggle]").forEach((button) => {
  button.addEventListener("click", () => {
    const card = button.closest(".rank-card");
    const expanded = card.classList.toggle("is-expanded");

    button.textContent = expanded ? "Show less" : "Show more";
  });
});

document.addEventListener("change", (event) => {
  const clanPicker = event.target.closest("[data-clan-picker]");
  if (!clanPicker) {
    return;
  }

  const form = clanPicker.closest("[data-clan-picker-form]");
  const action = form?.getAttribute("action") || window.location.pathname;
  const url = new URL(action, window.location.origin);
  url.searchParams.set("clanId", clanPicker.value);

  window.location.href = url.toString();
});

document.addEventListener("change", (event) => {
  const filePicker = event.target.closest("[data-file-picker]");
  if (!filePicker) {
    return;
  }

  const fileName = filePicker.closest("form")?.querySelector("[data-file-name]");
  if (!fileName) {
    return;
  }

  fileName.textContent = filePicker.files?.[0]?.name || "No file selected";
});

document.querySelectorAll("[data-streams-panel]").forEach(async (panel) => {
  const statusUrl = panel.getAttribute("data-streams-url");
  if (!statusUrl) {
    return;
  }

  try {
    const response = await fetch(statusUrl, {
      headers: {
        "Accept": "application/json"
      }
    });

    if (!response.ok) {
      throw new Error("Could not load stream status.");
    }

    const streams = await response.json();
    streams.forEach((stream) => {
      const card = panel.querySelector(`[data-streamer-card][data-channel="${CSS.escape(stream.channel)}"]`);
      if (!card) {
        return;
      }

      const state = card.querySelector("[data-streamer-state]");
      const name = card.querySelector("[data-streamer-name]");
      const meta = card.querySelector("[data-streamer-meta]");

      card.classList.remove("is-online", "is-offline", "is-unavailable");
      if (stream.statusUnavailable) {
        card.classList.add("is-unavailable");
        if (state) {
          state.textContent = "Status unavailable";
        }
      } else if (stream.isOnline) {
        card.classList.add("is-online");
        if (state) {
          state.textContent = "Live now";
        }
      } else {
        card.classList.add("is-offline");
        if (state) {
          state.textContent = "Offline";
        }
      }

      if (name && stream.displayName) {
        name.textContent = stream.displayName;
      }

      if (meta) {
        if (stream.isOnline) {
          const parts = [];
          if (stream.gameName) {
            parts.push(stream.gameName);
          }
          if (Number.isInteger(stream.viewerCount)) {
            parts.push(`${stream.viewerCount} viewers`);
          }
          meta.textContent = parts.join(" - ");
          meta.title = stream.title || "";
        } else {
          meta.textContent = "";
          meta.title = "";
        }
      }
    });
  } catch {
    panel.querySelectorAll("[data-streamer-card]").forEach((card) => {
      card.classList.add("is-unavailable");
      const state = card.querySelector("[data-streamer-state]");
      if (state) {
        state.textContent = "Status unavailable";
      }
    });
  }
});
