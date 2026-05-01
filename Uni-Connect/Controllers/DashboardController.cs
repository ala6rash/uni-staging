using Microsoft.AspNetCore.Mvc;
using Uni_Connect.Models;
using Uni_Connect.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Uni_Connect.Services;

namespace Uni_Connect.Controllers
{

    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IPointService _pointService;
        private readonly IPostService _postService;

        public DashboardController(ApplicationDbContext context, IWebHostEnvironment environment, IPointService pointService, IPostService postService)
        {
            _context = context;
            _environment = environment;
            _pointService = pointService;
            _postService = postService;
        }
        public async Task<IActionResult> Dashboard(string search, string filter)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login_Page", "Login");

            int userId = int.Parse(userIdStr);
            var user = await _context.Users
                .Include(u => u.Notifications)
                .Include(u => u.Posts)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return RedirectToAction("Login_Page", "Login");

            // Fetch all posts with related data
            IQueryable<Post> query = _context.Posts
                .Where(p => !p.IsDeleted)
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
                .Include(p => p.Answers.Where(a => !a.IsDeleted));

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(p => 
                    p.Title.ToLower().Contains(lowerSearch) || 
                    p.Content.ToLower().Contains(lowerSearch) ||
                    (p.CourseCode != null && p.CourseCode.ToLower().Contains(lowerSearch)) ||
                    p.PostTags.Any(pt => pt.Tag.Name.ToLower().Contains(lowerSearch))
                );
            }

            if (filter == "Faculty")
            {
                query = query.Where(p => p.User.Faculty == user.Faculty);
            }
            else if (filter == "Trending")
            {
                query = query.Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-7)).OrderByDescending(p => p.ViewsCount + p.Upvotes);
            }

            if (filter != "Trending")
            {
                query = query.OrderByDescending(p => p.CreatedAt);
            }

            var posts = await query.ToListAsync();

            ViewBag.Posts = posts;
            ViewBag.SearchQuery = search;
            ViewBag.ActiveFilter = filter ?? "All";
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpvotePost(int postId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var success = await _postService.UpvotePost(postId, user.UserID);
            if (!success) return BadRequest("Already upvoted or invalid post.");

            var post = await _context.Posts.FindAsync(postId);
            return Json(new { upvotes = post?.Upvotes ?? 0 });
        }
        public async Task<IActionResult> Profile()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");
            
            // Ensure navigation properties are loaded with counts
            await _context.Entry(user)
                .Collection(u => u.Posts)
                .Query()
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .LoadAsync();

            await _context.Entry(user)
                .Collection(u => u.Answers)
                .Query()
                .Where(a => !a.IsDeleted)
                .Include(a => a.Post)
                .LoadAsync();

            // Fetch session counts
            ViewBag.SessionCount = await _context.PrivateSessions
                .CountAsync(s => (s.StudentID == user.UserID || s.HelperID == user.UserID) && !s.IsDeleted);

            // Also pass the SettingsViewModel in case we are on the settings tab
            ViewBag.SettingsModel = new Uni_Connect.ViewModels.SettingsViewModel
            {
                UserID = user.UserID,
                Name = user.Name,
                Email = user.Email,
                Faculty = user.Faculty,
                YearOfStudy = user.YearOfStudy,
                ProfileImageUrl = user.ProfileImageUrl
            };
            
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemReward(string venue, string discount, int cost)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            if (user.Points < cost)
            {
                return Json(new { success = false, message = "Insufficient points." });
            }

            // Generate a random coupon code
            string couponCode = "PU-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

            // Deduct points
            await _pointService.DeductPoints(user.UserID, cost, "Reward Redeemed", $"{venue}: {discount} (Code: {couponCode})", "🎁");

            // Reload updated balance from DB (in-memory user.Points is stale after deduction)
            var updatedPoints = await _pointService.GetUserPoints(user.UserID);
            return Json(new { success = true, pointsBalance = updatedPoints, couponCode = couponCode });
        }

        // View another user's public profile by username
        public async Task<IActionResult> ViewProfile(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return RedirectToAction("Dashboard");

            var userProfile = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && !u.IsDeleted);

            if (userProfile == null) return RedirectToAction("Dashboard");

            // Load only non-deleted content
            await _context.Entry(userProfile)
                .Collection(u => u.Posts)
                .Query()
                .Where(p => !p.IsDeleted)
                .Include(p => p.Category)
                .LoadAsync();

            await _context.Entry(userProfile)
                .Collection(u => u.Answers)
                .Query()
                .Where(a => !a.IsDeleted)
                .LoadAsync();
            
            return View(userProfile);
        }

        // Settings page (GET)
        public async Task<IActionResult> Settings()
        {
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(Uni_Connect.ViewModels.SettingsViewModel model)
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            // Profile Update
            user.Name = model.Name?.Trim() ?? user.Name;
            user.Faculty = model.Faculty;
            user.YearOfStudy = model.YearOfStudy;
            user.ProfileImageUrl = model.ProfileImageUrl;

            // Password Change Logic
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    TempData["ErrorMessage"] = "Current password is required to change password.";
                    return RedirectToAction("Profile");
                }

                if (model.NewPassword != model.ConfirmPassword)
                {
                    TempData["ErrorMessage"] = "New password and confirmation do not match.";
                    return RedirectToAction("Profile");
                }

                if (model.NewPassword.Length < 6)
                {
                    TempData["ErrorMessage"] = "New password must be at least 6 characters.";
                    return RedirectToAction("Profile");
                }

                // Verify current password (using BCrypt)
                bool isValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);
                if (!isValid)
                {
                    TempData["ErrorMessage"] = "Current password is incorrect.";
                    return RedirectToAction("Profile");
                }

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
                TempData["SuccessMessage"] = "Password updated successfully.";
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            if (TempData["SuccessMessage"] == null)
            {
                TempData["SuccessMessage"] = "Profile settings updated successfully.";
            }

            return RedirectToAction("Profile");
        }
        public async Task<IActionResult> Notifications()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");
            return View(user);
        }
        public async Task<IActionResult> Leaderboard()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");
            
            // Default parameters (can be supplied via querystring)
            string faculty = Request.Query["faculty"].ToString();
            string period = Request.Query["period"].ToString();
            int top = 100;
            int.TryParse(Request.Query["top"].ToString(), out top);
            if (top <= 0) top = 100;

            var query = _context.Users.Where(u => !u.IsDeleted);

            if (!string.IsNullOrEmpty(faculty))
            {
                query = query.Where(u => u.Faculty == faculty);
            }

            List<User> leaderboardUsers;

            if (!string.IsNullOrEmpty(period) && (period == "This Month" || period == "This Week"))
            {
                // Rank by points EARNED in the selected period using PointsTransactions
                DateTime since = period == "This Month"
                    ? DateTime.UtcNow.AddMonths(-1)
                    : DateTime.UtcNow.AddDays(-7);

                var periodEarnerIds = await _context.PointsTransactions
                    .Where(pt => pt.CreatedAt >= since && !pt.IsDeleted && pt.Amount > 0)
                    .GroupBy(pt => pt.UserID)
                    .OrderByDescending(g => g.Sum(pt => pt.Amount))
                    .Take(top)
                    .Select(g => g.Key)
                    .ToListAsync();

                var periodUsersRaw = await query
                    .Where(u => periodEarnerIds.Contains(u.UserID))
                    .ToListAsync();

                // Preserve ranking order from periodEarnerIds (sorted by earned points)
                leaderboardUsers = periodEarnerIds
                    .Select(id => periodUsersRaw.FirstOrDefault(u => u.UserID == id))
                    .Where(u => u != null)
                    .Cast<User>()
                    .ToList();
            }
            else
            {
                leaderboardUsers = await query
                    .OrderByDescending(u => u.Points)
                    .Take(top)
                    .ToListAsync();
            }

            // Calculate user rank
            int userRank = 0;
            if (string.IsNullOrEmpty(faculty))
            {
                userRank = await _context.Users
                    .Where(u => !u.IsDeleted)
                    .CountAsync(u => u.Points > user.Points) + 1;
            }
            else
            {
                userRank = await _context.Users
                    .Where(u => !u.IsDeleted && u.Faculty == faculty)
                    .CountAsync(u => u.Points > user.Points) + 1;
            }

            var faculties = await _context.Users
                .Where(u => !u.IsDeleted && !string.IsNullOrEmpty(u.Faculty))
                .Select(u => u.Faculty)
                .Distinct()
                .ToListAsync();

            ViewBag.Leaderboard = leaderboardUsers;
            ViewBag.Faculties = faculties;
            ViewBag.SelectedFaculty = faculty;
            ViewBag.SelectedPeriod = string.IsNullOrEmpty(period) ? "All Time" : period;
            ViewBag.Top = top;
            ViewBag.UserRank = userRank;

            return View(user);
        }

        public async Task<IActionResult> Points()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            // Calculate level
            int level = Math.Min(user.Points / 500 + 1, 10);
            int pointsForCurrentLevel = (level - 1) * 500;
            int pointsForNextLevel = level * 500;
            int progressToNextLevel = Math.Max(0, user.Points - pointsForCurrentLevel);
            int pointsNeededForNext = Math.Max(0, pointsForNextLevel - user.Points);
            int progressPercentage = (int)((progressToNextLevel / (float)(pointsForNextLevel - pointsForCurrentLevel)) * 100);

            var userPosts = await _context.Posts.CountAsync(p => p.UserID == user.UserID && !p.IsDeleted);
            var userAnswers = await _context.Answers.CountAsync(a => a.UserID == user.UserID && !a.IsDeleted);

            var achievements = new List<ViewModels.Achievement>
            {
                new ViewModels.Achievement { Title = "First Steps", Description = "Score 100 points", Icon = "🎯", Unlocked = user.Points >= 100 },
                new ViewModels.Achievement { Title = "Helper", Description = "Give 5 answers", Icon = "🤝", Unlocked = userAnswers >= 5 },
                new ViewModels.Achievement { Title = "Questioner", Description = "Ask 3 questions", Icon = "❓", Unlocked = userPosts >= 3 }
            };

            // Get real transactions
            var transactions = await _context.PointsTransactions
                .Where(pt => pt.UserID == user.UserID && !pt.IsDeleted)
                .OrderByDescending(pt => pt.CreatedAt)
                .Take(20)
                .Select(pt => new ViewModels.PointTransaction
                {
                    Title = pt.Title,
                    Icon = pt.Icon,
                    Time = pt.CreatedAt.ToString("MMM dd, HH:mm"),
                    Amount = pt.Amount,
                    Detail = pt.Detail
                })
                .ToListAsync();

            var model = new ViewModels.PointsViewModel
            {
                UserID = user.UserID,
                Name = user.Name,
                Faculty = user.Faculty,
                YearOfStudy = user.YearOfStudy,
                CurrentPoints = user.Points,
                CurrentLevel = level,
                NextLevelPoints = pointsNeededForNext,
                ProgressPercentage = progressPercentage,
                QuestionsAsked = userPosts,
                AnswersGiven = userAnswers,
                Achievements = achievements,
                Transactions = transactions
            };

            return View(model);
        }

        public async Task<IActionResult> CreatePost()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            // Fetch common tags for the dropdown
            var commonTags = await _context.Tags
                .OrderByDescending(t => t.PostTags.Count)
                .Take(20)
                .Select(t => t.Name)
                .ToListAsync();

            if (!commonTags.Any())
            {
                commonTags = new List<string> { "Help", "Exam", "Lab", "Assignment", "Project", "Concept", "Error" };
            }

            ViewBag.CommonTags = commonTags;
            return View(new ViewModels.CreatePostViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost(ViewModels.CreatePostViewModel model)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            // Validate model
            if (!ModelState.IsValid)
            {
                if (isAjax) 
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return Json(new { success = false, message = "Validation failed: " + string.Join(", ", errors) });
                }
                return View(model);
            }

            // Get current user
            var user = await GetCurrentUser();
            if (user == null) 
            {
                if (isAjax) return Unauthorized();
                return RedirectToAction("Login_Page", "Login");
            }

            // Check if user has enough points
            if (user.Points < 10)
            {
                if (isAjax) return Json(new { success = false, message = "Insufficient points (10 required)." });
                ModelState.AddModelError("", "You need at least 10 points to post a question.");
                return View(model);
            }

            // Create Post via PostService
            var post = await _postService.CreatePost(model, user.UserID);
            if (post == null)
            {
                if (isAjax) return Json(new { success = false, message = "Failed to create post." });
                return View(model);
            }

            // Deduct points via PointService
            await _pointService.DeductPoints(user.UserID, 10, "Posted a Question", post.Title, "❓");

            if (isAjax) return Json(new { success = true, postId = post.PostID, pointsBalance = user.Points });
            return RedirectToAction("Dashboard");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTutoring(int postId, string description)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var success = await _postService.RequestTutoring(postId, user.UserID, description);
            if (!success) return BadRequest("Could not send tutoring request.");

            return RedirectToAction("SinglePost", new { id = postId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptTutoring(int requestId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var success = await _postService.AcceptTutoring(requestId, user.UserID);
            if (!success) return BadRequest("Could not accept tutoring request.");

            return RedirectToAction("Sessions");
        }

        public async Task<IActionResult> Sessions()
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            var allSessions = await _context.PrivateSessions
                .Where(s => !s.IsDeleted && (s.StudentID == user.UserID || s.HelperID == user.UserID))
                .Include(s => s.Student)
                .Include(s => s.Helper)
                .Include(s => s.Messages)
                .ToListAsync();

            ViewBag.HistorySessions = allSessions.Where(s => !s.IsActive).ToList();

            ViewBag.TutoringRequests = await _context.Requests
                .Include(r => r.Owner)
                .Include(r => r.Post)
                .Where(r => r.Post.UserID == user.UserID && r.Status == "Open")
                .ToListAsync();

            return View(allSessions.Where(s => s.IsActive).ToList());
        }

        public async Task<IActionResult> ChatPage(int id)
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            var session = await _context.PrivateSessions
                .Include(s => s.Student)
                .Include(s => s.Helper)
                .Include(s => s.Messages.OrderBy(m => m.SentAt))
                .FirstOrDefaultAsync(s => s.PrivateSessionID == id && !s.IsDeleted);

            if (session == null || (session.StudentID != user.UserID && session.HelperID != user.UserID))
                return RedirectToAction("Sessions");

            var otherUser = session.StudentID == user.UserID ? session.Helper : session.Student;

            // Sidebar: other active sessions for this user
            ViewBag.SidebarSessions = await _context.PrivateSessions
                .Where(s => !s.IsDeleted && s.IsActive && s.PrivateSessionID != id &&
                            (s.StudentID == user.UserID || s.HelperID == user.UserID))
                .Include(s => s.Student)
                .Include(s => s.Helper)
                .Include(s => s.Messages)
                .ToListAsync();

            ViewBag.Session = session;
            ViewBag.OtherUser = otherUser;
            ViewBag.CurrentUserId = user.UserID;
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePost(int postId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.PostID == postId && p.UserID == user.UserID);
            if (post == null) return NotFound();

            post.IsDeleted = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAnswer(int answerId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var answer = await _context.Answers.FirstOrDefaultAsync(a => a.AnswerID == answerId && a.UserID == user.UserID);
            if (answer == null) return NotFound();

            answer.IsDeleted = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("SinglePost", new { id = answer.PostID });
        }

        public async Task<IActionResult> SinglePost(int id)
        {
            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Category)
                .Include(p => p.Answers)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(p => p.PostID == id);

            if (post == null) return RedirectToAction("Dashboard");

            // Increment view count (persist asynchronously)
            try
            {
                post.ViewsCount += 1;
                _context.Posts.Update(post);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Non-fatal if saving views fails
            }

            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostAnswer(int postId, string content, IFormFile? ImageFile)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Answer content cannot be empty.";
                return RedirectToAction("SinglePost", new { id = postId });
            }

            var user = await GetCurrentUser();
            if (user == null) return RedirectToAction("Login_Page", "Login");

            // Post Answer via PostService
            var answer = await _postService.PostAnswer(postId, content, user.UserID, ImageFile);
            if (answer == null) return RedirectToAction("Dashboard");

            return RedirectToAction("SinglePost", new { id = postId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpvoteAnswer(int answerId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var success = await _postService.UpvoteAnswer(answerId, user.UserID);
            if (!success) return NotFound();

            var answer = await _context.Answers.FindAsync(answerId);
            return Ok(new { success = true, upvotes = answer?.Upvotes ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptAnswer(int answerId)
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            var success = await _postService.AcceptAnswer(answerId, user.UserID);
            if (!success) return BadRequest("Only the question author can accept an answer.");

            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetStats()
        {
            var user = await GetCurrentUser();
            if (user == null) return Unauthorized();

            // Calculate level
            int level = Math.Min(user.Points / 500 + 1, 10);
            int pointsForCurrentLevel = (level - 1) * 500;
            int pointsForNextLevel = level * 500;
            int progressToNextLevel = Math.Max(0, user.Points - pointsForCurrentLevel);
            int progressPercentage = (int)((progressToNextLevel / (float)(pointsForNextLevel - pointsForCurrentLevel)) * 100);

            var unreadNotifs = await _context.Notifications
                .CountAsync(n => n.UserID == user.UserID && !n.IsRead && !n.IsDeleted);

            return Json(new
            {
                points = user.Points,
                unreadNotifications = unreadNotifs,
                level = level,
                progress = progressPercentage,
                levelText = $"Level {level}",
                profileImageUrl = user.ProfileImageUrl ?? $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(user.Name)}&background=3D52A0&color=fff&size=80"
            });
        }

        private async Task<User> GetCurrentUser()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return null;

            int userId = int.Parse(userIdStr);
            return await _context.Users
                .Include(u => u.Notifications)
                .FirstOrDefaultAsync(u => u.UserID == userId);
        }

        [HttpGet]
        public async Task<IActionResult> SearchPostsApi(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
                return Json(new { results = Array.Empty<object>() });

            var lower = q.ToLower();
            var posts = await _context.Posts
                .Where(p => !p.IsDeleted &&
                    (p.Title.ToLower().Contains(lower) ||
                     (p.CourseCode != null && p.CourseCode.ToLower().Contains(lower)) ||
                     p.PostTags.Any(pt => pt.Tag.Name.ToLower().Contains(lower))))
                .Include(p => p.Answers.Where(a => !a.IsDeleted))
                .OrderByDescending(p => p.Upvotes + p.Answers.Count)
                .Take(5)
                .Select(p => new {
                    id = p.PostID,
                    title = p.Title,
                    course = p.CourseCode ?? "",
                    answers = p.Answers.Count(a => !a.IsDeleted),
                    votes = p.Upvotes,
                    solved = p.Answers.Any(a => a.IsAccepted && !a.IsDeleted)
                })
                .ToListAsync();

            return Json(new { results = posts });
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
