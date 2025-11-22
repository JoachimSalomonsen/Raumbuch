# Quick Start - Test Raumbuch API på 2 minutter

## ? Raskeste vei til testing

### **1. Start app (30 sekunder)**
```
Visual Studio ? F5
```
Vent til browser åpner.

---

### **2. Test i browser (30 sekunder)**

Åpne:
```
http://localhost:5005/api/values
```

**Ser du `["value1","value2"]`?** 
? API kjører!

---

### **3. Test med Postman (1 minutt)**

#### **Installer Postman hvis du ikke har det:**
https://www.postman.com/downloads/

#### **Test enkelt endpoint:**

```
POST http://localhost:5005/api/raumbuch/import-template
Content-Type: application/json

Body (raw JSON):
{
  "accessToken": "test",
  "projectId": "test",
  "templateFileId": "test",
  "targetFolderId": "test"
}
```

**Forventet:** Feilmelding om ugyldig token (dette er OK! Det betyr API-en fungerer)

---

## ?? Neste steg

**For å teste med EKTE data:**

1. **Få Trimble token** (se `LOCAL_TESTING_GUIDE.md` ? Alternativ A eller B)
2. **Finn file IDs** i Trimble Connect web
3. **Send request** med ekte verdier

---

## ? Problemer?

### **Browser viser 404**
? Sjekk porten i URL (se IIS Express icon i taskbar)

### **Postman får connection error**
? Er app startet i Visual Studio? (Se "IIS Express" i taskbar)

### **API returnerer 500 error**
? Se **Output** vindu i Visual Studio for stack trace

---

## ?? Mer info

- **Full testing guide:** `LOCAL_TESTING_GUIDE.md`
- **Trimble config:** `TRIMBLE_CONFIG_GUIDE.md`
- **Postman collection:** `Postman_Collection_Raumbuch_Local.json`

Lykke til! ??
