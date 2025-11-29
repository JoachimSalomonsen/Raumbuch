// Raumbuch Workspace App - Integrated with Extension API
let API = null;
let workspaceToken = null;
let workspaceProjectId = null;

async function init() {
    console.log("Initializing Raumbuch Workspace App…");

    // --- Try to connect to Trimble Workspace API ---
    try {
        API = await window.TrimbleConnectWorkspace.connect(
            window.parent,
            workspaceEventHandler,
            30000
        );
    } catch (err) {
        console.error("Could not connect to Trimble Workspace API:", err);
        // Not running as Workspace App, might be running as extension
        return;
    }

    console.log("Connected to Workspace API:", API);

    // --- Build Trimble Connect menu (if UI API available) ---
    if (API && API.ui && typeof API.ui.setMenu === 'function') {
        await API.ui.setMenu({
            title: "Raumbuch",
            icon: "https://raumbuch-a5h4f2bhd5dnhhhq.swedencentral-01.azurewebsites.net/Img/book.png",
            command: "menu_main",
        });

        if (typeof API.ui.setActiveMenuItem === 'function') {
            await API.ui.setActiveMenuItem("menu_raumbuch");
        }
    } else {
        console.log("UI API not available, skipping menu setup");
    }

    // --- Fetch useful info from Connect ---
    try {
        // Request access token using Workspace API
        const token = await API.extension.requestPermission("accesstoken");
        console.log("Access token received from Workspace API");
        workspaceToken = token;
        
        // Store token in the global trimbleConnect object if it exists
        if (typeof window.setWorkspaceToken === 'function') {
            window.setWorkspaceToken(token);
        }

        const project = await API.project.getCurrentProject();
        console.log("Current project:", project);
        
        if (project && project.id) {
            workspaceProjectId = project.id;
            console.log("Project ID from Workspace API:", workspaceProjectId);
            
            // Store project ID in the global trimbleConnect object if it exists
            if (typeof window.setWorkspaceProjectId === 'function') {
                window.setWorkspaceProjectId(project.id);
            }
        }

        const userSettings = await API.user.getUserSettings();
        console.log("User language:", userSettings.language);
        
        // Auto-load project data if function exists
        if (typeof window.loadProjectData === 'function' && workspaceToken && workspaceProjectId) {
            console.log("Auto-loading project data with Workspace API credentials");
            window.loadProjectData().catch(error => {
                console.error('Error auto-loading project data:', error);
            });
        }
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
       
        case "menu_raumprogramm":
            activateTab("tab-raumprogramm");
            break;

        case "menu_ausgefuehrt":
            activateTab("tab-ausgefuehrt");
            break;

        case "menu_analyse":
            activateTab("tab-analyse");
            break;

        case "menu_ausstattung":
            activateTab("tab-ausstattung");
            break;

        case "menu_nachricht":
            activateTab("tab-bcf");
            break;

        case "menu_konfig":
            activateTab("tab-konfig");
            break;

        default:
            console.warn("Unknown command:", command);
    }
}





init();
