using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for creating a TODO in Trimble Connect after Raumprogramm is created.
    /// </summary>
    public class CreateTodoRequest
    {
        /// <summary>
        /// Trimble Connect access token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Project ID in Trimble Connect
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// List of assignee email addresses
        /// </summary>
        public List<string> Assignees { get; set; }

        /// <summary>
        /// Optional: TODO title (default: "Raumprogramm wurde erstellt")
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Optional: TODO label
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Optional: Due date (ISO 8601 format: yyyy-MM-dd)
        /// </summary>
        public string DueDate { get; set; }
    }

    public class CreateTodoResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TodoId { get; set; }
    }
}
