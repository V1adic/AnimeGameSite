using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;

namespace DemoSRP
{
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly UserDatabase _database;
        private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Donat");
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };

        public AdminController(UserDatabase database)
        {
            _database = database;
            Directory.CreateDirectory(_uploadPath); // Ensure upload directory exists
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View("Views/Home/Admin.cshtml");
        }

        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpGet("secure")]
        public IActionResult Secure()
        {
            Console.WriteLine($"User authenticated: {User.Identity.IsAuthenticated}");
            Console.WriteLine($"User name: {User.Identity.Name}");
            return Ok(new { Message = "Secure admin endpoint accessed successfully", User = User.Identity.Name });
        }

        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost("secure/post")]
        public async Task<IActionResult> CreatePost([FromForm] string content, [FromForm] List<IFormFile> photos)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { Message = "Текст поста обязателен." });
            }

            if (photos == null || photos.Count == 0 || photos.Count > 10)
            {
                return BadRequest(new { Message = "Необходимо загрузить от 1 до 10 фотографий." });
            }

            try
            {
                // Проверяем, что все файлы являются изображениями
                foreach (var photo in photos)
                {
                    var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                    if (!_allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { Message = $"Недопустимый формат файла: {photo.FileName}. Разрешены: {string.Join(", ", _allowedExtensions)}" });
                    }
                }

                // Получаем следующий ID для фотографии
                var existingFiles = Directory.GetFiles(_uploadPath).Select(Path.GetFileNameWithoutExtension)
                    .Where(name => int.TryParse(name, out _))
                    .Select(name => int.Parse(name))
                    .DefaultIfEmpty(0)
                    .Max();

                var photoPaths = new List<string>();
                int nextId = existingFiles + 1;

                // Сохраняем фотографии с оригинальным расширением
                foreach (var photo in photos)
                {
                    if (photo.Length > 0)
                    {
                        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                        var fileName = $"{nextId}{extension}";
                        var filePath = Path.Combine(_uploadPath, fileName);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }
                        photoPaths.Add($"/Data/Donat/{fileName}");
                        nextId++;
                    }
                }

                // Сохраняем пост в базу данных через UserDatabase
                var postId = _database.CreatePost(content, string.Join(",", photoPaths));

                return Ok(new { Message = "Пост успешно создан", PostId = postId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании поста: {ex.Message}");
                return StatusCode(500, new { Message = "Ошибка при создании поста" });
            }
        }

        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
        [HttpPost("secure/role")]
        public IActionResult AssignRole([FromBody] AssignRoleModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Role))
            {
                return BadRequest(new { Message = "Имя пользователя и роль обязательны." });
            }

            if (!new[] { "User", "Donator", "Admin" }.Contains(model.Role))
            {
                return BadRequest(new { Message = "Недопустимая роль. Разрешены: User, Donator, Admin." });
            }

            try
            {
                if (!_database.UserExists(model.Username))
                {
                    return BadRequest(new { Message = $"Пользователь {model.Username} не найден." });
                }

                _database.UpdateUserRole(model.Username, model.Role);
                return Ok(new { Message = $"Роль {model.Role} успешно назначена пользователю {model.Username}." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при назначении роли: {ex.Message}");
                return StatusCode(500, new { Message = "Ошибка при назначении роли" });
            }
        }
    }

    public class AssignRoleModel
    {
        public string Username { get; set; }
        public string Role { get; set; }
    }
}