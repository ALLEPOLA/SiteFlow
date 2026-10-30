using Microsoft.AspNetCore.Mvc;
using SiteFlow.API.Data;

namespace SiteFlow.API.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DatabaseController(
        AppDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("check")]
    public async Task<IActionResult> Check(
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var connected = await _context.Database
            .CanConnectAsync(cancellationToken);

        if (!connected)
        {
            return StatusCode(503, new
            {
                message = "Database connection failed."
            });
        }

        return Ok(new
        {
            message = "MySQL connected successfully."
        });
    }
}