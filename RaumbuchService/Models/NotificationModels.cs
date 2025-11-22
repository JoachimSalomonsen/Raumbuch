using System.Collections.Generic;

namespace RaumbuchService.Models
{
    /// <summary>
    /// Request for getting list of project users.
    /// </summary>
    public class GetProjectUsersRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
    }

    public class GetProjectUsersResponse
    {
        public bool Success { get; set; }
        public List<UserInfo> Users { get; set; }
    }

    public class UserInfo
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// Request for creating a BCF Topic in Trimble Connect.
    /// </summary>
    public class CreateBcfTopicRequest
    {
        public string AccessToken { get; set; }
        public string ProjectId { get; set; }
        public string FolderId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string AssignedTo { get; set; } // Comma-separated email addresses
        public bool CreateMultipleTopics { get; set; } // If true, creates one topic per assignee
    }

    public class CreateBcfTopicResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TopicGuid { get; set; }
        public string DocumentReferenceGuid { get; set; }
        public List<string> AdditionalTopicGuids { get; set; }
        public int TotalTopicsCreated { get; set; }
    }

    /// <summary>
    /// Request for sending email notification.
    /// </summary>
    public class SendNotificationRequest
    {
        public string AccessToken { get; set; }
        public string FileId { get; set; }
        public string FileName { get; set; }
        public string ProjectName { get; set; }
        public List<string> RecipientEmails { get; set; }
        public string NotificationType { get; set; } // "raumprogramm" or "raumbuch"
    }

    public class SendNotificationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int RecipientCount { get; set; }
    }
}
