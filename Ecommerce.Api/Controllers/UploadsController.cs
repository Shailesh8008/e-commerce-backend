using Ecommerce.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IImageKitService _imageKitService;

    public UploadsController(IImageKitService imageKitService)
    {
        _imageKitService = imageKitService;
    }

    [HttpGet("imagekit-auth")]
    public IActionResult GetImageKitAuth()
    {
        var auth = _imageKitService.GenerateUploadAuth();
        return Ok(auth);
    }
}
