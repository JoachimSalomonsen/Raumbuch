using RaumbuchService.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaumbuchService.Services
{
    /// <summary>
    /// Service for analyzing SOLL (Raumprogramm) vs IST (Raumbuch) room areas.
    /// Groups by room category (Raumtyp) and calculates percentage.
    /// </summary>
    public class RaumbuchAnalyzer
    {
        /// <summary>
        /// Analyzes SOLL/IST room areas by category.
        /// </summary>
        /// <param name="raumprogrammData">SOLL data: Dictionary[RoomCategory] -> Total Area</param>
        /// <param name="raumbuchData">IST data: List of (RoomCategory, Area) per room</param>
        /// <returns>Analysis per room category</returns>
        public List<RoomCategoryAnalysis> Analyze(
            Dictionary<string, double> raumprogrammData,
            List<(string RoomCategory, double Area)> raumbuchData)
        {
            if (raumprogrammData == null || raumbuchData == null)
                throw new ArgumentNullException("SOLL or IST data is null.");

            var result = new List<RoomCategoryAnalysis>();

            // Group IST data by room category
            var istGrouped = raumbuchData
                .GroupBy(x => x.RoomCategory, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Area),
                    StringComparer.OrdinalIgnoreCase
                );

            // Compare each category
            foreach (var sollEntry in raumprogrammData)
            {
                string category = sollEntry.Key;
                double sollArea = sollEntry.Value;

                double istArea = istGrouped.ContainsKey(category) ? istGrouped[category] : 0.0;

                // Calculate percentage, handle division by zero
                double percentage;
                if (sollArea > 0)
                {
                    percentage = (istArea / sollArea) * 100.0;
                }
                else if (istArea > 0)
                {
                    // SOLL is 0 but IST has area -> 100% (or could be marked as "undefined")
                    percentage = 100.0;
                }
                else
                {
                    // Both 0
                    percentage = 0.0;
                }

                result.Add(new RoomCategoryAnalysis
                {
                    RoomCategory = category,
                    SollArea = sollArea,
                    IstArea = istArea,
                    Percentage = Math.Round(percentage, 2)
                });
            }

            // Add categories that exist in IST but not in SOLL
            foreach (var istEntry in istGrouped)
            {
                if (!raumprogrammData.ContainsKey(istEntry.Key))
                {
                    result.Add(new RoomCategoryAnalysis
                    {
                        RoomCategory = istEntry.Key,
                        SollArea = 0.0,
                        IstArea = istEntry.Value,
                        Percentage = 100.0  // Mark as 100% since SOLL is missing/undefined
                    });
                }
            }

            return result.OrderBy(x => x.RoomCategory).ToList();
        }
    }

    /// <summary>
    /// SOLL/IST analysis per room category.
    /// German Raumbuch standard status values: "OK", "Zu wenig", "Zu viel"
    /// </summary>
    public class RoomCategoryAnalysis
    {
        public string RoomCategory { get; set; }
        public double SollArea { get; set; }
        public double IstArea { get; set; }
        public double Percentage { get; set; }

        /// <summary>
        /// Returns true if IST is less than SOLL (too little area).
        /// This is the condition that requires attention (red highlighting).
        /// </summary>
        public bool IsUnderLimit => !double.IsNaN(Percentage) && Percentage >= 0 && Percentage < 100.0 && SollArea > 0;

        /// <summary>
        /// Returns true if IST exceeds SOLL (too much area).
        /// </summary>
        public bool IsOverLimit => !double.IsNaN(Percentage) && Percentage > 100.0;

        /// <summary>
        /// Returns the German status string according to Raumbuch standard.
        /// "OK" - IST equals SOLL (within tolerance)
        /// "Zu wenig" - IST is less than SOLL (needs attention)
        /// "Zu viel" - IST exceeds SOLL
        /// </summary>
        public string Status
        {
            get
            {
                if (double.IsNaN(Percentage)) return "OK";
                if (SollArea <= 0 && IstArea <= 0) return "OK";
                if (IsUnderLimit) return "Zu wenig";
                if (IsOverLimit) return "Zu viel";
                return "OK";
            }
        }
    }
}
