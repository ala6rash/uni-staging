using System;

namespace Uni_Connect.Models
{
    public class PostUpvote
    {
        public int PostUpvoteID { get; set; }
        public int PostID { get; set; }
        public int UserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Post Post { get; set; }
        public User User { get; set; }
    }
}
