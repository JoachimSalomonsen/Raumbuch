using System;
using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for discovering available properties from IFC files.
    /// </summary>
    public class DiscoverPropertiesRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// List of IFC file IDs to discover properties from
        /// </summary>
        public List<string> IfcFileIds { get; set; }

        /// <summary>
        /// Partial Pset name to search for (e.g. "Plancal nova")
        /// </summary>
        public string PsetPartialName { get; set; }
    }

    /// <summary>
    /// Response for discovering properties.
    /// </summary>
    public class DiscoverPropertiesResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<PropertyInfo> Properties { get; set; }
    }

    /// <summary>
    /// Information about a discovered property.
    /// </summary>
    public class PropertyInfo
    {
        public string PropertyName { get; set; }
        public string PsetName { get; set; }
        public int OccurrenceCount { get; set; }
    }
}
