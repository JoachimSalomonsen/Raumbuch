// Raumbuch Manager - Trimble Connect Extension
// Backend API URL (change to Azure URL after deployment)
const API_BASE_URL = 'https://raumbuch.azurewebsites.net/api/raumbuch';
// const API_BASE_URL = 'https://localhost:44305/api/raumbuch'; // For local testing

// State
let accessToken = null;
let projectId = null;
let folderId = null;

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    console.log('Raumbuch Manager initialized');
    
    try {
        // Get Trimble Connect context
        accessToken = await getAccessToken();
        projectId = await getProjectId();
        
        // Load files and folders
        await loadFolders();
        await loadFiles();
        
        // Setup event listeners
        setupEventListeners();
        
        showSuccess('App bereit!', 'step1Result');
    } catch (error) {
        console.error('Initialization error:', error);
        showError('Fehler beim Initialisieren: ' + error.message, 'step1Result');
    }
});

// Event Listeners
function setupEventListeners() {
    document.getElementById('btnImportTemplate').addEventListener('click', importTemplate);
    document.getElementById('btnCreateTodo').addEventListener('click', createTodo);
    document.getElementById('btnImportIfc').addEventListener('click', importIfc);
    document.getElementById('btnAnalyzeRooms').addEventListener('click', analyzeRooms);
    document.getElementById('btnResetIfc').addEventListener('click', resetIfc);
}

// Get Access Token from Trimble Connect
async function getAccessToken() {
    // For Trimble Connect Extension
    if (window.parent && window.parent.TC) {
        return await window.parent.TC.getToken();
    }
    
    // For standalone testing
    const token = prompt('Enter Trimble Connect access token:');
    if (!token) throw new Error('Access token required');
    return token;
}

// Get Project ID from Trimble Connect
async function getProjectId() {
    if (window.parent && window.parent.TC) {
        return await window.parent.TC.getProjectId();
    }
    
    const id = prompt('Enter Project ID:');
    if (!id) throw new Error('Project ID required');
    return id;
}

// Load Folders
async function loadFolders() {
    // Simplified: Load root folder
    // In production, fetch from Trimble Connect API
    const folderSelect = document.getElementById('targetFolder');
    folderSelect.innerHTML = '<option value="ROOT">Root Folder</option>';
}

// Load Files
async function loadFiles() {
    try {
        // Fetch files from Trimble Connect
        // This is a placeholder - implement actual file listing
        const templateSelect = document.getElementById('templateFile');
        const ifcSelect = document.getElementById('ifcFile');
        const raumprogrammSelect = document.getElementById('raumprogrammFile');
        
        templateSelect.innerHTML = '<option value="">Select template...</option>';
        ifcSelect.innerHTML = '<option value="">Select IFC file...</option>';
        raumprogrammSelect.innerHTML = '<option value="">Select Raumprogramm...</option>';
        
        // TODO: Fetch actual files from Trimble Connect
        // const files = await fetchFiles();
    } catch (error) {
        console.error('Error loading files:', error);
    }
}

// API Calls

async function importTemplate() {
    const templateFileId = document.getElementById('templateFile').value;
    const targetFolderId = document.getElementById('targetFolder').value;
    
    if (!templateFileId || !targetFolderId) {
        showError('Bitte wählen Sie Vorlage und Zielordner', 'step1Result');
        return;
    }
    
    showLoading('Importiere Vorlage...', 'step1Result');
    
    try {
        const response = await fetch(`${API_BASE_URL}/import-template`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                accessToken,
                projectId,
                templateFileId,
                targetFolderId
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            showSuccess(`✅ ${data.message}<br>Datei: ${data.raumprogrammFileName}`, 'step1Result');
        } else {
            showError(`❌ Fehler: ${data.message}`, 'step1Result');
        }
    } catch (error) {
        showError(`❌ Fehler: ${error.message}`, 'step1Result');
    }
}

async function createTodo() {
    const title = document.getElementById('todoTitle').value;
    const assignees = document.getElementById('todoAssignees').value.split(',').map(e => e.trim());
    
    if (!title) {
        showError('Bitte geben Sie einen Titel ein', 'step2Result');
        return;
    }
    
    showLoading('Erstelle Aufgabe...', 'step2Result');
    
    try {
        const response = await fetch(`${API_BASE_URL}/create-todo`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                accessToken,
                projectId,
                title,
                assignees,
                label: 'Raumbuch'
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            showSuccess(`✅ ${data.message}`, 'step2Result');
        } else {
            showError(`❌ Fehler: ${data.message}`, 'step2Result');
        }
    } catch (error) {
        showError(`❌ Fehler: ${error.message}`, 'step2Result');
    }
}

async function importIfc() {
    const ifcFileId = document.getElementById('ifcFile').value;
    const raumprogrammFileId = document.getElementById('raumprogrammFile').value;
    const targetFolderId = document.getElementById('targetFolder').value;
    
    if (!ifcFileId || !raumprogrammFileId) {
        showError('Bitte wählen Sie IFC und Raumprogramm', 'step3Result');
        return;
    }
    
    showLoading('Importiere IFC und erstelle Raumbuch...', 'step3Result');
    
    try {
        const response = await fetch(`${API_BASE_URL}/import-ifc`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                accessToken,
                projectId,
                ifcFileId,
                raumprogrammFileId,
                targetFolderId
            })
        });
        
        const data = await response.json();
        
        if (data.success) {
            let message = `✅ ${data.message}<br>`;
            message += `Datei: ${data.raumbuchFileName}<br>`;
            message += `<strong>Analyse:</strong><br>`;
            data.analysis.forEach(a => {
                message += `- ${a.roomCategory}: ${a.percentage.toFixed(1)}% ${a.isOverLimit ? '⚠️' : '✅'}<br>`;
            });
            showSuccess(message, 'step3Result');
        } else {
            showError(`❌ Fehler: ${data.message}`, 'step3Result');
        }
    } catch (error) {
        showError(`❌ Fehler: ${error.message}`, 'step3Result');
    }
}

async function analyzeRooms() {
    showError('Bitte implementieren Sie zuerst IFC Import', 'step4Result');
}

async function resetIfc() {
    showError('Bitte implementieren Sie zuerst IFC Import', 'step5Result');
}

// UI Helpers

function showLoading(message, elementId) {
    const element = document.getElementById(elementId);
    element.className = 'result loading';
    element.innerHTML = `⏳ ${message}`;
}

function showSuccess(message, elementId) {
    const element = document.getElementById(elementId);
    element.className = 'result success';
    element.innerHTML = message;
}

function showError(message, elementId) {
    const element = document.getElementById(elementId);
    element.className = 'result error';
    element.innerHTML = message;
}
