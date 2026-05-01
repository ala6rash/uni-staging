using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Uni_Connect.Models;

namespace Uni_Connect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> AdminDashboard()
        {
            ViewBag.UserCount = await _context.Users.IgnoreQueryFilters().CountAsync();
            ViewBag.PostCount = await _context.Posts.IgnoreQueryFilters().CountAsync();
            ViewBag.ReportCount = await _context.Reports.CountAsync(r => !r.IsResolved);
            ViewBag.AnswerCount = await _context.Answers.IgnoreQueryFilters().CountAsync();

            ViewBag.RecentUsers = await _context.Users.OrderByDescending(u => u.CreatedAt).Take(5).ToListAsync();
            ViewBag.RecentReports = await _context.Reports.Include(r => r.Reporter).OrderByDescending(r => r.CreatedAt).Take(5).ToListAsync();
            
            // Faculty Distribution for Chart
            ViewBag.FacultyStats = await _context.Users
                .GroupBy(u => u.Faculty)
                .Select(g => new { Faculty = g.Key, Count = g.Count() })
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> ManageUsers()
        {
            var users = await _context.Users.IgnoreQueryFilters().OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserDelete(int id)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserID == id);
            if (user != null)
            {
                // Prevent self-deletion
                var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                if (currentUserId == id)
                {
                    return RedirectToAction("ManageUsers");
                }

                user.IsDeleted = !user.IsDeleted;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageUsers");
        }

        public async Task<IActionResult> ManageReports()
        {
            var reports = await _context.Reports
                .Include(r => r.Reporter)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveReport(int id)
        {
            var report = await _context.Reports.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.ReportID == id);
            
            if (report != null)
            {
                report.IsResolved = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageReports");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdminRole(int id)
        {
            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserID == id);
            if (user != null)
            {
                // Prevent self-demotion
                var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                if (currentUserId == id)
                {
                    return RedirectToAction("ManageUsers");
                }

                user.Role = (user.Role == "Admin") ? "Student" : "Admin";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("ManageUsers");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePostEndorsement(int id)
        {
            var post = await _context.Posts.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.PostID == id);
            if (post != null)
            {
                post.IsEndorsed = !post.IsEndorsed;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("SinglePost", "Dashboard", new { id = id });
        }
    }
}
