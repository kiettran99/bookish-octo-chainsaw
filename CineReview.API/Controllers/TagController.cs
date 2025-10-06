using CineReview.API.Attributes;
using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.Models.TagModels;
using Microsoft.AspNetCore.Mvc;

namespace CineReview.API.Controllers;

/// <summary>
/// Controller for managing review tags
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TagController : CommonController
{
    private readonly ITagService _tagService;

    public TagController(ITagService tagService)
    {
        _tagService = tagService;
    }

    /// <summary>
    /// Get all tags with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTags([FromQuery] TagFilterRequestModel? filter = null)
    {
        var response = await _tagService.GetAllTagsAsync(filter);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get all active tags
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveTags()
    {
        var response = await _tagService.GetActiveTagsAsync();

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get tags grouped by category
    /// Useful for dropdown UI with categories
    /// </summary>
    [HttpGet("by-category")]
    public async Task<IActionResult> GetTagsByCategory()
    {
        var response = await _tagService.GetTagsByCategoryAsync();

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get a specific tag by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTagById(int id)
    {
        var response = await _tagService.GetTagByIdAsync(id);

        if (!response.IsSuccess)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Create a new tag (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequestModel request)
    {
        // TODO: Add admin role check
        var response = await _tagService.CreateTagAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return CreatedAtAction(nameof(GetTagById), new { id = response.Data?.Id }, response);
    }

    /// <summary>
    /// Update an existing tag (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateTag(int id, [FromBody] UpdateTagRequestModel request)
    {
        // TODO: Add admin role check
        if (id != request.Id)
        {
            return BadRequest("ID mismatch");
        }

        var response = await _tagService.UpdateTagAsync(request);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Delete (deactivate) a tag (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteTag(int id)
    {
        // TODO: Add admin role check
        var response = await _tagService.DeleteTagAsync(id);

        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
