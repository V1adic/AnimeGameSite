using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DemoSRP;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IO;

namespace AnimeGameSite.Controllers
{
    [Route("donat")]
    public class DonatController : Controller
    {
        private readonly UserDatabase _userDatabase;
        private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Donat");

        public DonatController()
        {
            _userDatabase = new UserDatabase();
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/Home/Donat.cshtml");
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Donator,Admin")]
        [HttpGet("secure")]
        public IActionResult Secure()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Challenge("Bearer");
            }
            return Ok(new { Message = "Secure donator endpoint accessed successfully", User = User.Identity.Name });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Donator,Admin")]
        [HttpGet("getposts")]
        public IActionResult GetPosts(int page = 1, int pageSize = 10)
        {
            var posts = _userDatabase.GetPosts()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    postId = p.PostId,
                    content = p.Content,
                    photoPaths = p.PhotoPaths.Split(',', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList(),
                    createdAt = p.CreatedAt
                })
                .ToList();

            var hasMore = _userDatabase.GetPosts().Count > page * pageSize;

            return Ok(new { posts, hasMore });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Donator,Admin")]
        [HttpGet("image/{postId}/{index}")]
        public IActionResult GetImage(long postId, int index)
        {
            var post = _userDatabase.GetPosts().FirstOrDefault(p => p.PostId == postId);

            var photoPaths = post.PhotoPaths.Split(',', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();
            if (index < 0 || index >= photoPaths.Count)
            {
                return NotFound();
            }

            var filePath = Path.Combine(_uploadPath, Path.GetFileName(photoPaths[index]));
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return File(fileStream, mimeType);
        }
    }
}