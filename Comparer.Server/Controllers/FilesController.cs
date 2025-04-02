using System.IO;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace Comparer.Server.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFiles([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest("Nie przes�ano �adnych plik�w.");
            }

            var uploadDirectory = "Uploads";
            Directory.CreateDirectory(uploadDirectory); // Tworzy folder, je�li nie istnieje

            foreach (var file in files)
            {
                var filePath = Path.Combine(uploadDirectory, file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            return Ok(new { Message = $"{files.Count} plik�w przes�anych pomy�lnie!" });
        }
    }
}