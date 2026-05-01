using Microsoft.EntityFrameworkCore;
using Uni_Connect.Models;
using Uni_Connect.ViewModels;

namespace Uni_Connect.Services
{
    public class PostService : IPostService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IPointService _pointService;

        public PostService(ApplicationDbContext context, IWebHostEnvironment environment, IPointService pointService)
        {
            _context = context;
            _environment = environment;
            _pointService = pointService;
        }

        public async Task<Post?> CreatePost(CreatePostViewModel model, int userId)
        {
            // Map faculty to category
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Faculty == model.Faculty);
            if (category == null)
            {
                category = new Category { Faculty = model.Faculty, Name = model.Faculty };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            // Handling Image Upload
            string? imageUrl = await SaveImage(model.ImageFile, "posts");

            // Create Post
            var post = new Post
            {
                Title = model.Title,
                Content = model.Content,
                UserID = userId,
                CategoryID = category.CategoryID,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                ImageUrl = imageUrl,
                CourseCode = model.CourseCode
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Process tags
            if (!string.IsNullOrWhiteSpace(model.Tags))
            {
                var tagNames = model.Tags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Distinct().Take(5);
                foreach (var tagName in tagNames)
                {
                    var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == tagName.ToLower()) 
                              ?? new Tag { Name = tagName };
                    
                    if (tag.TagID == 0) 
                    {
                        _context.Tags.Add(tag);
                        await _context.SaveChangesAsync();
                    }

                    _context.PostTags.Add(new PostTag { PostID = post.PostID, TagID = tag.TagID });
                }
                await _context.SaveChangesAsync();
            }

            return post;
        }

        public async Task<Answer?> PostAnswer(int postId, string content, int userId, IFormFile? imageFile)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostID == postId);
            if (post == null) return null;

            // Handle Image Upload
            string? imageUrl = await SaveImage(imageFile, "answers");

            var answer = new Answer
            {
                PostID = postId,
                UserID = userId,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                Upvotes = 0,
                IsDeleted = false,
                IsAccepted = false,
                ImageUrl = imageUrl
            };

            _context.Answers.Add(answer);
            
            // Award points via PointService
            await _pointService.AwardPoints(userId, 5, "Answered a Question", 
                post.Title.Length > 25 ? post.Title.Substring(0, 25) + "..." : post.Title, "🤝");

            // Create Notification for the Post Author
            if (post.UserID != userId)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = post.UserID,
                    Type = "Answer",
                    ReferenceID = post.PostID,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return answer;
        }

        public async Task<bool> UpvoteAnswer(int answerId, int userId)
        {
            var answer = await _context.Answers
                .Include(a => a.User)
                .Include(a => a.Post)
                .FirstOrDefaultAsync(a => a.AnswerID == answerId && !a.IsDeleted);

            if (answer == null) return false;

            // Prevent self-upvoting (Logic Fault Fix)
            if (answer.UserID == userId) return false;

            // Prevent duplicate upvoting
            var existingUpvote = await _context.AnswerUpvotes
                .AnyAsync(au => au.AnswerID == answerId && au.UserID == userId);
            if (existingUpvote) return false;

            // Record the upvote
            _context.AnswerUpvotes.Add(new AnswerUpvote { AnswerID = answerId, UserID = userId });

            answer.Upvotes += 1;
            
            // Award points to the answer author
            if (answer.User != null)
            {
                await _pointService.AwardPoints(answer.User.UserID, 10, "Received an Upvote", "Your answer was upvoted", "👍");
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AcceptAnswer(int answerId, int userId)
        {
            var answer = await _context.Answers
                .Include(a => a.Post)
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AnswerID == answerId && !a.IsDeleted);

            if (answer == null || answer.Post.UserID != userId) return false;

            // Prevent multiple accepted answers on the same post
            var alreadyAccepted = await _context.Answers
                .AnyAsync(a => a.PostID == answer.PostID && a.IsAccepted && !a.IsDeleted);
            if (alreadyAccepted) return false;

            // Mark as accepted
            answer.IsAccepted = true;
            
            // Award points for best answer
            if (answer.User != null)
            {
                await _pointService.AwardPoints(answer.User.UserID, 15, "Marked as Best Answer", "Your answer was accepted as the best", "🏆");
            }

            // Create Notification for the Answer Author
            if (answer.UserID != userId)
            {
                _context.Notifications.Add(new Notification
                {
                    UserID = answer.UserID,
                    Type = "BestAnswer",
                    ReferenceID = answer.PostID,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpvotePost(int postId, int userId)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PostID == postId && !p.IsDeleted);

            if (post == null) return false;

            // Prevent self-upvoting
            if (post.UserID == userId) return false;

            // Prevent duplicate upvoting
            var existingUpvote = await _context.PostUpvotes
                .AnyAsync(pu => pu.PostID == postId && pu.UserID == userId);
            if (existingUpvote) return false;

            // Record the upvote
            _context.PostUpvotes.Add(new PostUpvote { PostID = postId, UserID = userId });

            post.Upvotes += 1;

            // Award points to post author
            if (post.User != null)
            {
                await _pointService.AwardPoints(post.User.UserID, 5, "Post Upvoted", "Your question was upvoted", "🔥");
            }

            // Create notification
            _context.Notifications.Add(new Notification
            {
                UserID = post.UserID,
                Type = "PostUpvote",
                ReferenceID = post.PostID,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RequestTutoring(int postId, int userId, string description)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostID == postId && !p.IsDeleted);
            if (post == null || post.UserID == userId) return false;

            // Check if already requested
            var existing = await _context.Requests.AnyAsync(r => r.PostID == postId && r.OwnerID == userId);
            if (existing) return false;

            var request = new Request
            {
                PostID = postId,
                OwnerID = userId,
                Description = description,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.Requests.Add(request);

            // Notify post author
            _context.Notifications.Add(new Notification
            {
                UserID = post.UserID,
                Type = "TutoringRequest",
                ReferenceID = postId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AcceptTutoring(int requestId, int userId)
        {
            var request = await _context.Requests
                .Include(r => r.Post)
                .FirstOrDefaultAsync(r => r.RequestID == requestId && !r.IsDeleted);

            if (request == null || request.Post.UserID != userId) return false;

            request.Status = "Accepted";

            // Create Private Session
            var session = new PrivateSession
            {
                RequestID = requestId,
                StudentID = request.OwnerID,
                HelperID = userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsDeleted = false
            };

            _context.PrivateSessions.Add(session);

            // Notify student
            _context.Notifications.Add(new Notification
            {
                UserID = request.OwnerID,
                Type = "SessionAccepted",
                ReferenceID = requestId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<string?> SaveImage(IFormFile? file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            if (file.Length > 5 * 1024 * 1024) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext)) return null;

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/{folder}/{uniqueFileName}";
        }
    }
}
