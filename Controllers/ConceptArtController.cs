using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.IO;
using System.Threading.Tasks;
using AnimeGameSite.Hubs;

namespace AnimeGameSite.Controllers
{
    public class ConceptArtController : Controller
    {
        private readonly string _conceptArtPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "ConceptArt");
        private readonly IHubContext<ConceptArtHub> _hubContext;

        public ConceptArtController(IHubContext<ConceptArtHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [Route("Data/ConceptArt")]
        public async Task<IActionResult> GetConceptArts()
        {
            if (!Directory.Exists(_conceptArtPath))
            {
                return Json(new { error = "Folder not found" });
            }

            var files = Directory.GetFiles(_conceptArtPath)
                .Select(f => new { name = Path.GetFileName(f) })
                .ToList();
            await _hubContext.Clients.All.SendAsync("ReceiveConceptArts", files);
            return Json(files);
        }

        [Route("Data/ConceptArt/{fileName}")]
        public async Task<IActionResult> GetConceptArt(string fileName)
        {
            var filePath = Path.Combine(_conceptArtPath, fileName);
            if (System.IO.File.Exists(filePath))
            {
                var mimeType = GetMimeType(fileName);
                return PhysicalFile(filePath, mimeType);
            }
            return NotFound();
        }

        [HttpPost]
        [Route("Data/ConceptArt/Upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!Directory.Exists(_conceptArtPath))
                Directory.CreateDirectory(_conceptArtPath);

            var filePath = Path.Combine(_conceptArtPath, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var files = Directory.GetFiles(_conceptArtPath)
                .Select(f => new { name = Path.GetFileName(f) })
                .ToList();
            await _hubContext.Clients.All.SendAsync("ReceiveConceptArts", files);
            return Ok();
        }

        [HttpDelete]
        [Route("Data/ConceptArt/{fileName}")]
        public async Task<IActionResult> Delete(string fileName)
        {
            var filePath = Path.Combine(_conceptArtPath, fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            System.IO.File.Delete(filePath);

            var files = Directory.GetFiles(_conceptArtPath)
                .Select(f => new { name = Path.GetFileName(f) })
                .ToList();
            await _hubContext.Clients.All.SendAsync("ReceiveConceptArts", files);
            return Ok();
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream",
            };
        }
    }
}