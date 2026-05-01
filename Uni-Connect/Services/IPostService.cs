using Uni_Connect.Models;
using Uni_Connect.ViewModels;

namespace Uni_Connect.Services
{
    public interface IPostService
    {
        Task<Post?> CreatePost(CreatePostViewModel model, int userId);
        Task<Answer?> PostAnswer(int postId, string content, int userId, IFormFile? imageFile);
        Task<bool> UpvoteAnswer(int answerId, int userId);
        Task<bool> AcceptAnswer(int answerId, int userId);
        Task<bool> UpvotePost(int postId, int userId);
        Task<bool> RequestTutoring(int postId, int userId, string description);
        Task<bool> AcceptTutoring(int requestId, int userId);
    }
}
