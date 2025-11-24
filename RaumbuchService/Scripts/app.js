// Raumbuch Workspace App - forbedret versjon
let API = null;

async function init() {
    console.log("Initializing Raumbuch Workspace App…");

    // --- Try to connect to Trimble Workspace API ---
    try {
        API = await window.WorkspaceAPI.connect(
            window.parent,
            workspaceEventHandler,
            30000
        );
    } catch (err) {
        console.error("Could not connect to Trimble Workspace API:", err);
        return;
    }

    console.log("Connected to Workspace API:", API);

    // --- Build Trimble Connect menu ---
    await API.ui.setMenu({
        title: "Raumbuch",
        icon: "Img/book.png",
        command: "menu_main",
        subMenus: [
            { title: "Raumbuch", icon: "Img/book.png", command: "menu_raumbuch" },
            { title: "Ausstattung", icon: "Img/material.png", command: "menu_ausstattung" },
            { title: "Nachricht", icon: "Img/mail.png", command: "menu_nachricht" }
        ]
    });

    await API.ui.setActiveMenuItem("menu_raumbuch");

    // --- Fetch useful info from Connect ---
    try {
        const token = await API.extension.requestPermission("accesstoken");
        console.log("Access token:", token);

        const project = await API.project.getCurrentProject();
        console.log("Current project:", project);

        const userSettings = await API.user.getUserSettings();
        console.log("User language:", userSettings.language);
    } catch (err) {
        console.warn("Some Workspace API capabilities are not available:", err);
    }
}


// -------------------------------
// Workspace API Event Handler
// -------------------------------
function workspaceEventHandler(event, args) {
    console.log("Workspace event:", event, args);

    if (event === "extension.command") {
        handleCommand(args.data);
    }
    if (event === "extension.accessToken") {
        console.log("Access token event:", args.data);
    }
}


// -------------------------------
// Handle menu commands
// -------------------------------
function handleCommand(command) {
    console.log("Command received:", command);

    switch (command) {
        case "menu_raumbuch":
            activateTab("tab-konfig");
            break;

        case "menu_ausstattung":
            activateTab("tab-ausstattung");
            break;

        case "menu_nachricht":
            activateTab("tab-bcf");
            break;

        default:
            console.warn("Unknown command:", command);
    }
}


// -------------------------------
// Activate correct HTML tab
// -------------------------------
function activateTab(tabId) {
    // Hide all tabs
    document.querySelectorAll(".tc-tab-content").forEach(tab => {
        tab.style.display = "none";
    });

    // Show the selected tab
    const tab = document.getElementById(tabId);
    if (tab) tab.style.display = "block";

    // Update top-tab visuals (your existing UI)
    document.querySelectorAll(".tc-tab").forEach(x => x.classList.remove("active"));
    const activeButton = document.querySelector(`[data-tab="${tabId}"]`);
    if (activeButton) activeButton.classList.add("active");
}


init();
