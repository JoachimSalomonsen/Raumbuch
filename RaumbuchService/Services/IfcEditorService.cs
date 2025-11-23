using GeometryGym.Ifc;
using RaumbuchService.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaumbuchService.Services
{
    /// <summary>
    /// IFC editor service for reading and modifying IFC files.
    /// Supports creating Pset "Überprüfung der Raumkategorie" on IfcSpace objects.
    /// Uses GeometryGymIFC 0.1.21
    /// </summary>
    public class IfcEditorService
    {
        private const string VERIFICATION_PSET_NAME = "Überprüfung der Raumkategorie";
        private const string PROP_PERCENTAGE = "Prozentuale Fläche";
        private const string PROP_OVER_LIMIT = "Über angegebener Raumfläche";

        private const string RAUMBUCH_PSET_NAME = "Raumbuch";
        private const string PROP_DIFFERENZ = "Differenzfläche zur Raumkategorie";
        private const string PROP_GEMAESS_RAUMPROGRAMM = "Gemäss Raumprogramm";

        /// <summary>
        /// Reads all IfcSpace objects from an IFC file.
        /// Returns: List of (RoomCategory=LongName, RoomId=Name, Name=Name, Area, Pset properties)
        /// </summary>
        public List<RoomData> ReadSpaces(string ifcFilePath)
        {
            var db = new DatabaseIfc(ifcFilePath);
            var result = new List<RoomData>();

            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null)
                    continue;

                string longName = ExtractIfcLabelValue(space.LongName ?? "");
                string name = ExtractIfcLabelValue(space.Name ?? "");
                double area = GetSpaceArea(space);

                // Read Pset properties and clean them
                string siaCategory = ExtractIfcLabelValue(GetPsetProperty(space, "Pset_Nutzungsarten_Raum_SIA", "Raumkategorie SIA d 0165"));
                string floorCovering = ExtractIfcLabelValue(GetPsetProperty(space, "Pset_SpaceCommon", "FloorCovering"));
                string spaceCategory = ExtractIfcLabelValue(GetPsetProperty(space, "Pset_SpaceCommon", "Category"));

                result.Add(new RoomData
                {
                    RoomCategory = longName.Length > 0 ? longName : name,  // LongName as category
                    RoomId = name,         // Name for backward compatibility
                    Name = name,           // Explicit Name field
                    Area = area,
                    GlobalId = space.GlobalId,
                    SiaCategory = siaCategory,
                    FloorCovering = floorCovering,
                    SpaceCategory = spaceCategory
                });
            }

            return result;
        }

        /// <summary>
        /// Marks rooms with Pset "Überprüfung der Raumkategorie" based on analysis.
        /// Only marks rooms where category percentage > 100%.
        /// </summary>
        public MarkRoomsResult MarkRoomsOverLimit(
            string ifcFilePath,
            string outputPath,
            List<Models.RoomCategoryAnalysis> analysis)
        {
            var db = new DatabaseIfc(ifcFilePath);

            // Build lookup: category -> percentage
            var categoryPercentage = analysis
                .Where(a => a.Percentage > 100)
                .ToDictionary(a => a.RoomCategory, a => a.Percentage, StringComparer.OrdinalIgnoreCase);

            if (categoryPercentage.Count == 0)
            {
                db.WriteFile(outputPath);
                return new MarkRoomsResult
                {
                    RoomsMarked = 0,
                    MarkedRoomNames = new List<string>()
                };
            }

            var markedRooms = new List<string>();

            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null)
                    continue;

                string roomCategory = space.LongName ?? space.Name ?? "";

                if (!categoryPercentage.ContainsKey(roomCategory))
                    continue;

                double percentage = categoryPercentage[roomCategory];

                // Create or update Pset
                var pset = GetOrCreatePset(space, VERIFICATION_PSET_NAME);

                // Add properties
                var propPercentage = new IfcPropertySingleValue(
                    db,
                    PROP_PERCENTAGE,
                    new IfcReal(percentage)
                );

                var propOverLimit = new IfcPropertySingleValue(
                    db,
                    PROP_OVER_LIMIT,
                    new IfcBoolean(true)
                );

                pset.HasProperties[PROP_PERCENTAGE] = propPercentage;
                pset.HasProperties[PROP_OVER_LIMIT] = propOverLimit;

                markedRooms.Add(space.Name ?? space.GlobalId);
            }

            db.WriteFile(outputPath);

            return new MarkRoomsResult
            {
                RoomsMarked = markedRooms.Count,
                MarkedRoomNames = markedRooms
            };
        }

        /// <summary>
        /// Removes Pset "Überprüfung der Raumkategorie" from all IfcSpace objects.
        /// </summary>
        public ResetIfcResult ResetVerificationPset(string ifcFilePath, string outputPath)
        {
            var db = new DatabaseIfc(ifcFilePath);
            int psetsRemoved = 0;

            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null)
                    continue;

                // Find the Pset
                IfcPropertySet targetPset = null;
                IfcRelDefinesByProperties targetRel = null;

                foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
                {
                    foreach (var def in rel.RelatingPropertyDefinition)
                    {
                        var pset = def as IfcPropertySet;
                        if (pset != null &&
                            pset.Name != null &&
                            pset.Name.Equals(VERIFICATION_PSET_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            targetPset = pset;
                            targetRel = rel;
                            break;
                        }
                    }
                    if (targetPset != null)
                        break;
                }

                if (targetPset == null)
                    continue;

                // Remove space from relation
                if (targetRel != null)
                {
                    targetRel.RelatedObjects.Remove(space);

                    // Remove empty relation
                    if (targetRel.RelatedObjects.Count == 0)
                    {
                        targetRel.Dispose(false);
                    }
                }

                // Check if Pset is still used
                bool psetStillUsed = false;
                foreach (var rel in db.OfType<IfcRelDefinesByProperties>())
                {
                    if (rel.RelatingPropertyDefinition.Contains(targetPset))
                    {
                        psetStillUsed = true;
                        break;
                    }
                }

                if (!psetStillUsed)
                {
                    targetPset.Dispose(false);
                    psetsRemoved++;
                }
            }

            db.WriteFile(outputPath);

            return new ResetIfcResult
            {
                PsetsRemoved = psetsRemoved
            };
        }

        // --------------------------------------------------------------------
        //  RAUMBUCH PSET OPERATIONS (STEP 4)
        // --------------------------------------------------------------------

        /// <summary>
        /// Writes Pset "Raumbuch" to IFC spaces based on Raumbuch Excel data.
        /// Creates NEW Psets (fails if Pset already exists on a space).
        /// Each space gets its own dedicated Pset (no shared relations).
        /// </summary>
        public WritePsetRaumbuchResult WritePsetRaumbuch(
            string ifcFilePath,
            string outputPath,
            Dictionary<string, RaumbuchPsetData> raumbuchData)
        {
            var db = new DatabaseIfc(ifcFilePath);
            var result = new WritePsetRaumbuchResult
            {
                RoomsUpdated = 0,
                RoomsSkipped = 0,
                Warnings = new List<string>()
            };

            // Build index: IfcSpace.Name/LongName -> IfcSpace
            var spaceIndex = new Dictionary<string, IfcSpace>(StringComparer.OrdinalIgnoreCase);
            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null) continue;

                if (!string.IsNullOrWhiteSpace(space.Name))
                    spaceIndex[space.Name.Trim()] = space;

                if (!string.IsNullOrWhiteSpace(space.LongName))
                    spaceIndex[space.LongName.Trim()] = space;
            }

            // Process each room from Raumbuch data
            foreach (var kvp in raumbuchData)
            {
                string raumName = kvp.Key;
                var data = kvp.Value;

                // Find matching space
                if (!spaceIndex.TryGetValue(raumName, out IfcSpace space))
                {
                    result.Warnings.Add($"Raum '{raumName}' nicht in IFC gefunden - übersprungen");
                    result.RoomsSkipped++;
                    continue;
                }

                // Check if Pset "Raumbuch" already exists
                bool psetExists = false;
                foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
                {
                    foreach (var def in rel.RelatingPropertyDefinition)
                    {
                        var pset = def as IfcPropertySet;
                        if (pset != null && 
                            pset.Name != null && 
                            pset.Name.Equals(RAUMBUCH_PSET_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            psetExists = true;
                            break;
                        }
                    }
                    if (psetExists) break;
                }

                if (psetExists)
                {
                    result.Warnings.Add($"Raum '{raumName}': Pset 'Raumbuch' existiert bereits - bitte aktualisieren");
                    result.RoomsSkipped++;
                    continue;
                }

                // Create new Pset "Raumbuch" (dedicated to this space only)
                var newPset = new IfcPropertySet(db, RAUMBUCH_PSET_NAME);

                // Add property: Differenzfläche zum Raumprogramm (IfcAreaMeasure)
                var propDifferenz = new IfcPropertySingleValue(
                    db,
                    PROP_DIFFERENZ,
                    new IfcAreaMeasure(data.Differenz)
                );
                newPset.HasProperties[PROP_DIFFERENZ] = propDifferenz;

                // Add property: Gemäss Raumprogramm (IfcText: "Ja" / "Nein")
                var propGemaess = new IfcPropertySingleValue(
                    db,
                    PROP_GEMAESS_RAUMPROGRAMM,
                    new IfcText(data.GemaessRaumprogramm)
                );
                newPset.HasProperties[PROP_GEMAESS_RAUMPROGRAMM] = propGemaess;

                // Create NEW relation (dedicated to this space only)
                var newRel = new IfcRelDefinesByProperties(newPset);
                newRel.RelatedObjects.Add(space);

                result.RoomsUpdated++;
            }

            db.WriteFile(outputPath);

            return result;
        }

        /// <summary>
        /// Updates Pset "Raumbuch" on IFC spaces.
        /// Overwrites existing properties if Pset exists.
        /// Creates NEW Pset if it doesn't exist.
        /// Each space gets its own dedicated Pset (no shared relations).
        /// </summary>
        public WritePsetRaumbuchResult UpdatePsetRaumbuch(
            string ifcFilePath,
            string outputPath,
            Dictionary<string, RaumbuchPsetData> raumbuchData)
        {
            var db = new DatabaseIfc(ifcFilePath);
            var result = new WritePsetRaumbuchResult
            {
                RoomsUpdated = 0,
                RoomsSkipped = 0,
                Warnings = new List<string>()
            };

            // Build index: IfcSpace.Name/LongName -> IfcSpace
            var spaceIndex = new Dictionary<string, IfcSpace>(StringComparer.OrdinalIgnoreCase);
            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null) continue;

                if (!string.IsNullOrWhiteSpace(space.Name))
                    spaceIndex[space.Name.Trim()] = space;

                if (!string.IsNullOrWhiteSpace(space.LongName))
                    spaceIndex[space.LongName.Trim()] = space;
            }

            // Process each room from Raumbuch data
            foreach (var kvp in raumbuchData)
            {
                string raumName = kvp.Key;
                var data = kvp.Value;

                // Find matching space
                if (!spaceIndex.TryGetValue(raumName, out IfcSpace space))
                {
                    result.Warnings.Add($"Raum '{raumName}' nicht in IFC gefunden - übersprugen");
                    result.RoomsSkipped++;
                    continue;
                }

                // Find existing Pset "Raumbuch"
                IfcPropertySet existingPset = null;
                IfcRelDefinesByProperties existingRel = null;

                foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
                {
                    foreach (var def in rel.RelatingPropertyDefinition)
                    {
                        var pset = def as IfcPropertySet;
                        if (pset != null && 
                            pset.Name != null && 
                            pset.Name.Equals(RAUMBUCH_PSET_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            existingPset = pset;
                            existingRel = rel;
                            break;
                        }
                    }
                    if (existingPset != null) break;
                }

                IfcPropertySet targetPset;

                if (existingPset != null)
                {
                    // CLONE existing Pset to avoid shared relations
                    targetPset = new IfcPropertySet(db, RAUMBUCH_PSET_NAME);

                    // Copy existing properties (if any) that are NOT being updated
                    foreach (var kvpProp in existingPset.HasProperties)
                    {
                        var prop = kvpProp.Value as IfcPropertySingleValue;
                        if (prop == null) continue;

                        // Skip properties we're about to update
                        if (prop.Name.Equals(PROP_DIFFERENZ, StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals(PROP_GEMAESS_RAUMPROGRAMM, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Clone other properties
                        var clonedProp = ClonePropertySingleValue(prop, db);
                        if (clonedProp != null)
                        {
                            targetPset.HasProperties[clonedProp.Name] = clonedProp;
                        }
                    }

                    // Remove space from old relation
                    if (existingRel != null)
                    {
                        existingRel.RelatedObjects.Remove(space);

                        // Remove empty relation
                        if (existingRel.RelatedObjects.Count == 0)
                        {
                            existingRel.Dispose(false);
                        }
                    }

                    // Check if old Pset is still used
                    bool psetStillUsed = false;
                    foreach (var rel in db.OfType<IfcRelDefinesByProperties>())
                    {
                        if (rel.RelatingPropertyDefinition.Contains(existingPset))
                        {
                            psetStillUsed = true;
                            break;
                        }
                    }

                    if (!psetStillUsed)
                    {
                        existingPset.Dispose(false);
                    }

                    // Create NEW relation for cloned Pset
                    var newRel = new IfcRelDefinesByProperties(targetPset);
                    newRel.RelatedObjects.Add(space);
                }
                else
                {
                    // Create NEW Pset if it doesn't exist
                    targetPset = new IfcPropertySet(db, RAUMBUCH_PSET_NAME);

                    // Create NEW relation
                    var newRel = new IfcRelDefinesByProperties(targetPset);
                    newRel.RelatedObjects.Add(space);
                }

                // Add/Update properties
                var propDifferenz = new IfcPropertySingleValue(
                    db,
                    PROP_DIFFERENZ,
                    new IfcAreaMeasure(data.Differenz)
                );
                targetPset.HasProperties[PROP_DIFFERENZ] = propDifferenz;

                var propGemaess = new IfcPropertySingleValue(
                    db,
                    PROP_GEMAESS_RAUMPROGRAMM,
                    new IfcText(data.GemaessRaumprogramm)
                );
                targetPset.HasProperties[PROP_GEMAESS_RAUMPROGRAMM] = propGemaess;

                result.RoomsUpdated++;
            }

            db.WriteFile(outputPath);

            return result;
        }

        /// <summary>
        /// Removes Pset "Raumbuch" from all IFC spaces.
        /// </summary>
        public RemovePsetRaumbuchResult RemovePsetRaumbuch(string ifcFilePath, string outputPath)
        {
            // Check if trying to write to same file (GeometryGym limitation)
            bool isSameFile = string.Equals(
                System.IO.Path.GetFullPath(ifcFilePath),
                System.IO.Path.GetFullPath(outputPath),
                StringComparison.OrdinalIgnoreCase
            );
            
            if (isSameFile)
            {
                throw new InvalidOperationException("Cannot write to the same file that is being read. Use a different output path.");
            }
            
            var db = new DatabaseIfc(ifcFilePath);
            int psetsRemoved = 0;

            // Step 1: Collect all Psets and Relations to remove (avoid modifying collection during iteration)
            var psetsToRemove = new List<IfcPropertySet>();
            var relationsToClean = new List<(IfcRelDefinesByProperties rel, IfcSpace space)>();

            foreach (var space in db.OfType<IfcSpace>())
            {
                if (space == null) continue;

                // Find Pset "Raumbuch"
                foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>().ToList())
                {
                    foreach (var def in rel.RelatingPropertyDefinition)
                    {
                        var pset = def as IfcPropertySet;
                        if (pset != null && 
                            pset.Name != null && 
                            pset.Name.Equals(RAUMBUCH_PSET_NAME, StringComparison.OrdinalIgnoreCase))
                        {
                            // Mark for removal
                            relationsToClean.Add((rel, space));
                            
                            if (!psetsToRemove.Contains(pset))
                            {
                                psetsToRemove.Add(pset);
                            }
                        }
                    }
                }
            }

            // Step 2: Remove spaces from relations
            foreach (var (rel, space) in relationsToClean)
            {
                rel.RelatedObjects.Remove(space);
            }

            // Step 3: Dispose empty relations
            var relationsToDispose = new List<IfcRelDefinesByProperties>();
            
            foreach (var rel in db.OfType<IfcRelDefinesByProperties>())
            {
                if (rel.RelatedObjects.Count == 0)
                {
                    relationsToDispose.Add(rel);
                }
            }
            
            foreach (var rel in relationsToDispose)
            {
                rel.Dispose(false);
            }

            // Step 4: Check which Psets are still in use, dispose unused ones
            foreach (var pset in psetsToRemove)
            {
                bool psetStillUsed = false;
                
                foreach (var rel in db.OfType<IfcRelDefinesByProperties>())
                {
                    if (rel.RelatingPropertyDefinition.Contains(pset))
                    {
                        psetStillUsed = true;
                        break;
                    }
                }

                if (!psetStillUsed)
                {
                    pset.Dispose(false);
                    psetsRemoved++;
                }
            }

            db.WriteFile(outputPath);

            return new RemovePsetRaumbuchResult
            {
                PsetsRemoved = psetsRemoved
            };
        }

        // --------------------------------------------------------------------
        //  INVENTORY OPERATIONS (STEP 5)
        // --------------------------------------------------------------------

        /// <summary>
        /// Reads all elements from IFC that have a specific property (e.g. "Room Nbr") in Psets matching a partial name.
        /// Returns elements grouped by room number.
        /// Uses backward search: Find property first, then get elements from Pset relations.
        /// </summary>
        /// <param name="ifcFilePath">Path to IFC file</param>
        /// <param name="psetPartialName">Partial Pset name to search for (e.g. "Plancal nova" matches "Pset Plancal nova - Electrical")</param>
        /// <param name="roomPropertyName">Property name for room identification (e.g. "Room Nbr")</param>
        /// <returns>Dictionary: RoomNumber -> List of InventoryItem</returns>
        public Dictionary<string, List<InventoryItem>> ReadInventoryByRoom(
            string ifcFilePath,
            string psetPartialName,
            string roomPropertyName)
        {
            var db = new DatabaseIfc(ifcFilePath);
            var result = new Dictionary<string, List<InventoryItem>>(StringComparer.OrdinalIgnoreCase);
            
            // Extract filename from path
            string ifcFileName = System.IO.Path.GetFileName(ifcFilePath);

            System.Diagnostics.Debug.WriteLine($"ReadInventoryByRoom - Searching for Psets containing: '{psetPartialName}', Property: '{roomPropertyName}'");
            System.Diagnostics.Debug.WriteLine($"ReadInventoryByRoom - IFC file: '{ifcFileName}'");

            // Iterate through all IfcRelDefinesByProperties to find matching Psets
            foreach (var rel in db.OfType<IfcRelDefinesByProperties>())
            {
                foreach (var def in rel.RelatingPropertyDefinition)
                {
                    var pset = def as IfcPropertySet;
                    if (pset == null || string.IsNullOrWhiteSpace(pset.Name))
                        continue;

                    // Check if Pset name contains the partial search string (case-insensitive)
                    if (pset.Name.IndexOf(psetPartialName, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    System.Diagnostics.Debug.WriteLine($"  Found matching Pset: {pset.Name}");

                    // Look for the room property in this Pset
                    string roomNumber = null;
                    foreach (var prop in pset.HasProperties.Values)
                    {
                        if (prop.Name.Equals(roomPropertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            var singleValue = prop as IfcPropertySingleValue;
                            if (singleValue != null && singleValue.NominalValue != null)
                            {
                                string rawValue = singleValue.NominalValue.ToString().Trim();
                                roomNumber = ExtractIfcLabelValue(rawValue);
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(roomNumber))
                        continue; // No room number found in this Pset

                    System.Diagnostics.Debug.WriteLine($"    Found {roomPropertyName}: {roomNumber}");

                    // Now get all elements related to this Pset
                    foreach (var element in rel.RelatedObjects)
                    {
                        var ifcElement = element as IfcElement;
                        if (ifcElement == null)
                            continue;

                        // Extract element information and clean values
                        string elementName = ExtractIfcLabelValue(ifcElement.Name ?? "");
                        string elementDescription = "";
                        string elementGlobalId = ifcElement.GlobalId ?? "";

                        // Try to get description from Pset_Common or similar
                        string rawDescription = GetElementDescription(ifcElement);
                        elementDescription = ExtractIfcLabelValue(rawDescription);

                        var inventoryItem = new InventoryItem
                        {
                            Name = elementName,
                            Description = elementDescription,
                            GlobalId = elementGlobalId,
                            ElementType = ifcElement.GetType().Name,
                            PsetName = pset.Name,
                            RoomNumber = roomNumber,
                            IfcFileName = ifcFileName  // Add filename
                        };

                        // Add to result dictionary
                        if (!result.ContainsKey(roomNumber))
                        {
                            result[roomNumber] = new List<InventoryItem>();
                        }

                        result[roomNumber].Add(inventoryItem);

                        System.Diagnostics.Debug.WriteLine($"      Added element: {elementName} ({elementGlobalId}) from {ifcFileName}");
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"ReadInventoryByRoom - Found {result.Count} rooms with inventory");
            
            return result;
        }

        /// <summary>
        /// Helper method to get element description from various Psets.
        /// Tries Pset_Common, Description property, or ObjectType.
        /// </summary>
        private string GetElementDescription(IfcElement element)
        {
            // Try to get description from Psets
            foreach (var rel in element.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
            {
                foreach (var def in rel.RelatingPropertyDefinition)
                {
                    var pset = def as IfcPropertySet;
                    if (pset == null)
                        continue;

                    // Look for common description properties
                    foreach (var prop in pset.HasProperties.Values)
                    {
                        if (prop.Name.Equals("Description", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("Beschreibung", StringComparison.OrdinalIgnoreCase))
                        {
                            var singleValue = prop as IfcPropertySingleValue;
                            if (singleValue != null && singleValue.NominalValue != null)
                            {
                                return singleValue.NominalValue.ToString();
                            }
                        }
                    }
                }
            }

            // Fallback to ObjectType
            if (!string.IsNullOrWhiteSpace(element.ObjectType))
                return element.ObjectType;

            return "";
        }

        // --------------------------------------------------------------------
        //  HELPER METHODS
        // --------------------------------------------------------------------

        /// <summary>
        /// Extracts clean value from IFC label format.
        /// Examples:
        ///   IFCLABEL('TT U1.672') -> TT U1.672
        ///   IFCLABEL('Room 123') -> Room 123
        ///   Normal text -> Normal text
        /// </summary>
        private string ExtractIfcLabelValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            // Check if value is in IFCLABEL('...') format
            if (value.StartsWith("IFCLABEL('", StringComparison.OrdinalIgnoreCase) && value.EndsWith("')"))
            {
                // Extract content between quotes
                int startIndex = "IFCLABEL('".Length;
                int length = value.Length - startIndex - 2; // Remove ending ')
                return value.Substring(startIndex, length);
            }

            // Return as-is if not in label format
            return value;
        }

        private double GetSpaceArea(IfcSpace space)
        {
            // Try to get area from BaseQuantities
            foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
            {
                foreach (var def in rel.RelatingPropertyDefinition)
                {
                    var qset = def as IfcElementQuantity;
                    if (qset == null || !qset.Name.Contains("BaseQuantities"))
                        continue;

                    foreach (var qty in qset.Quantities.Values)
                    {
                        var areaQty = qty as IfcQuantityArea;
                        if (areaQty != null && areaQty.Name.Contains("Area"))
                        {
                            return areaQty.AreaValue;
                        }
                    }
                }
            }

            return 0.0;
        }

        private IfcPropertySet GetOrCreatePset(IfcSpace space, string psetName)
        {
            DatabaseIfc db = space.Database;

            // Check if Pset already exists on this space
            foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
            {
                foreach (var def in rel.RelatingPropertyDefinition)
                {
                    var pset = def as IfcPropertySet;
                    if (pset != null &&
                        pset.Name != null &&
                        pset.Name.Equals(psetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return pset;
                    }
                }
            }

            // Create new Pset
            var newPset = new IfcPropertySet(db, psetName);
            var newRel = new IfcRelDefinesByProperties(newPset);
            newRel.RelatedObjects.Add(space);

            return newPset;
        }

        /// <summary>
        /// Reads a property value from a specific Pset on an IfcSpace.
        /// Returns empty string if Pset or property not found.
        /// </summary>
        private string GetPsetProperty(IfcSpace space, string psetName, string propertyName)
        {
            foreach (var rel in space.IsDefinedBy.OfType<IfcRelDefinesByProperties>())
            {
                foreach (var def in rel.RelatingPropertyDefinition)
                {
                    var pset = def as IfcPropertySet;
                    if (pset == null || !pset.Name.Equals(psetName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Find property by name
                    foreach (var prop in pset.HasProperties.Values)
                    {
                        if (prop.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                        {
                            var singleValue = prop as IfcPropertySingleValue;
                            if (singleValue != null && singleValue.NominalValue != null)
                            {
                                return singleValue.NominalValue.ToString();
                            }
                        }
                    }
                }
            }

            return "";
        }

        private IfcPropertySingleValue ClonePropertySingleValue(IfcPropertySingleValue src, DatabaseIfc db)
        {
            if (src == null || db == null)
                return null;

            // Clone NominalValue – attempt same type
            IfcValue clonedValue = null;
            if (src.NominalValue != null)
            {
                var t = src.NominalValue.GetType();
                try
                {
                    clonedValue = (IfcValue)Activator.CreateInstance(t, new object[] { src.NominalValue.Value });
                }
                catch
                {
                    // fallback to text
                    clonedValue = new IfcText(src.NominalValue.Value.ToString());
                }
            }

            // Create brand new property instance
            var clone = new IfcPropertySingleValue(
                db,
                src.Name,
                clonedValue
            );

            return clone;
        }
    }

    // ------------------------------------------------------------------------
    //  HELPER CLASSES
    // ------------------------------------------------------------------------

    public class RoomData
    {
        public string RoomCategory { get; set; }  // LongName
        public string RoomId { get; set; }        // Name (for backward compatibility)
        public string Name { get; set; }          // IfcSpace.Name
        public double Area { get; set; }
        public string GlobalId { get; set; }
        
        // Pset properties
        public string SiaCategory { get; set; }        // Pset_Nutzungsarten_Raum_SIA.Raumkategorie SIA d 0165
        public string FloorCovering { get; set; }      // Pset_SpaceCommon.FloorCovering
        public string SpaceCategory { get; set; }      // Pset_SpaceCommon.Category
    }

    public class MarkRoomsResult
    {
        public int RoomsMarked { get; set; }
        public List<string> MarkedRoomNames { get; set; }
    }

    public class ResetIfcResult
    {
        public int PsetsRemoved { get; set; }
    }

    /// <summary>
    /// Data for Pset "Raumbuch" per room.
    /// </summary>
    public class RaumbuchPsetData
    {
        public double Differenz { get; set; }               // Differenzfläche zum Raumprogramm (m²)
        public string GemaessRaumprogramm { get; set; }     // "Ja" or "Nein"
    }

    public class WritePsetRaumbuchResult
    {
        public int RoomsUpdated { get; set; }
        public int RoomsSkipped { get; set; }
        public List<string> Warnings { get; set; }
    }

    public class RemovePsetRaumbuchResult
    {
        public int PsetsRemoved { get; set; }
    }

    /// <summary>
    /// Inventory item found in IFC for a specific room.
    /// </summary>
    public class InventoryItem
    {
        public string Name { get; set; }            // IfcElement.Name
        public string Description { get; set; }     // From Pset Description property or ObjectType
        public string GlobalId { get; set; }        // IfcElement.GlobalId
        public string ElementType { get; set; }     // Type name (e.g. "IfcFurniture", "IfcDoor")
        public string PsetName { get; set; }        // Pset where Room Nbr was found
        public string RoomNumber { get; set; }      // Room number from property
        public string IfcFileName { get; set; }     // IFC file where element was found
    }
}
