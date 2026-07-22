const cloud = document.getElementById("cloud");
const linked = document.getElementById("linked");
const agent = document.getElementById("agent");
const volume = document.getElementById("volume");
const volumeValue = document.getElementById("volumeValue");
const applyVolume = document.getElementById("applyVolume");
const toast = document.getElementById("toast");
let toastTimer;

function notify(message, error = false) {
  clearTimeout(toastTimer);
  toast.textContent = message;
  toast.className = `toast visible${error ? " error" : ""}`;
  toastTimer = setTimeout(() => { toast.className = "toast"; }, 3600);
}

async function postWithFeedback(form, pendingText, successText) {
  const button = form.querySelector("button");
  const label = button.textContent;
  button.disabled = true;
  button.textContent = pendingText;
  try {
    const response = await fetch(form.action, { method: "POST" });
    if (!response.ok) throw new Error(await response.text());
    notify(successText);
  } catch (error) {
    notify(error.message || "The operation failed.", true);
  } finally {
    button.disabled = false;
    button.textContent = label;
  }
}

fetch("/api/cloud")
  .then(response => response.json())
  .then(status => {
    const connected = status.linked && status.agent?.online;
    linked.textContent = status.linked ? "Linked" : "Not linked";
    agent.textContent = status.agent?.online ? "Online" : "Offline";
    linked.className = status.linked ? "ok" : "warn";
    agent.className = status.agent?.online ? "ok" : "warn";
    cloud.innerHTML = `<span class="status-dot"></span>${connected ? "Connected" : "Attention needed"}`;
    cloud.classList.toggle("attention", !connected);
  })
  .catch(() => {
    cloud.innerHTML = '<span class="status-dot"></span>Cloud unavailable';
    cloud.classList.add("attention");
  });

document.querySelectorAll(".action-form").forEach(form => {
  form.addEventListener("submit", event => {
    event.preventDefault();
    postWithFeedback(form, "Running…", "Action completed successfully.");
  });
});

document.querySelectorAll(".permission-form").forEach(form => {
  form.addEventListener("submit", event => {
    event.preventDefault();
    postWithFeedback(form, "Saving…", "Permission saved. Restart HomePC to apply it.");
  });
});

document.querySelectorAll("[data-confirm]").forEach(form => {
  form.addEventListener("submit", event => {
    if (!confirm(form.dataset.confirm)) event.preventDefault();
  });
});

volume.addEventListener("input", () => { volumeValue.value = `${volume.value}%`; });
applyVolume.addEventListener("click", async () => {
  applyVolume.disabled = true;
  applyVolume.textContent = "Applying…";
  try {
    const response = await fetch(`/volume/${volume.value}`, { method: "POST" });
    if (!response.ok) throw new Error(await response.text());
    notify(`Volume set to ${volume.value}%.`);
  } catch (error) {
    notify(error.message || "Volume update failed.", true);
  } finally {
    applyVolume.disabled = false;
    applyVolume.textContent = "Apply";
  }
});
