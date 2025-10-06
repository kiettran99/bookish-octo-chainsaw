using CineReview.Domain.Enums;

namespace CineReview.Domain.Models.TagModels;

/// <summary>
/// Request model for creating a new tag
/// </summary>
public class CreateTagRequestModel
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public TagCategory Category { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request model for updating an existing tag
/// </summary>
public class UpdateTagRequestModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public TagCategory Category { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Response model for tag data
/// </summary>
public class TagResponseModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public TagCategory Category { get; set; }
    public string CategoryName { get; set; } = null!;
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }
}

/// <summary>
/// Request model for filtering tags
/// </summary>
public class TagFilterRequestModel
{
    public TagCategory? Category { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Model for a tag rating selected by user
/// Used in Review.DescriptionTag as JSON
/// </summary>
public class TagRatingModel
{
    public int TagId { get; set; }
    public string TagName { get; set; } = null!;
    public int Rating { get; set; } // 1-10
}
