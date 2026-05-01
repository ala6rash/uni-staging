using System;

namespace Uni_Connect.Models
{
    public class AnswerUpvote
    {
        public int AnswerUpvoteID { get; set; }
        public int AnswerID { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Answer Answer { get; set; }
        public User User { get; set; }
    }
}
