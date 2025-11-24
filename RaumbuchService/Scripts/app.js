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

    // --- Build Trimble Connect menu ---
    await API.ui.setMenu({
        title: "Raumbuch",
        icon: "https://raumbuch-a5h4f2bhd5dnhhhq.swedencentral-01.azurewebsites.net/Img/book.png",
        command: "menu_main",
    });

    await API.ui.setActiveMenuItem("menu_raumbuch");

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
        case "menu_raumbuch":
            activateTab("tab-konfig");
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

        default:
            console.warn("Unknown command:", command);
    }
}


// -------------------------------
// Activate correct HTML tab
// -------------------------------
function activateTab(tabId) {
    console.log("Activating tab:", tabId);
    
    // Use the existing openTab function from index.html if available
    if (typeof window.openTab === 'function') {
        window.openTab(tabId);
    } else {
        // Fallback to manual tab switching
        // Hide all tabs
        document.querySelectorAll(".tab-content").forEach(tab => {
            tab.classList.remove('active');
        });

        // Show the selected tab
        const tab = document.getElementById(tabId);
        if (tab) {
            tab.classList.add('active');
        }

        // Update top-tab visuals (existing UI)
        document.querySelectorAll(".tc-tab").forEach(btn => {
            btn.classList.remove("active");
        });
        
        // Find and activate the corresponding tab button
        const index = ['tab-konfig', 'tab-analyse', 'tab-ausstattung', 'tab-bcf'].indexOf(tabId);
        const tabButtons = document.querySelectorAll('.tc-tab');
        if (tabButtons[index]) {
            tabButtons[index].classList.add('active');
        }
    }
}


init();
