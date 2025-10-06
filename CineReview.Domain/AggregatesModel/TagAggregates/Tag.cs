using System.ComponentModel.DataAnnotations.Schema;
using CineReview.Domain.Enums;
using Common.SeedWork;

namespace CineReview.Domain.AggregatesModel.TagAggregates;

/// <summary>
/// Tag entity for movie reviews
/// Users can select tags and rate them 1-10
/// </summary>
public class Tag : Entity
{
    [Column(TypeName = "TEXT")]
    public string Name { get; set; } = null!;

    [Column(TypeName = "TEXT")]
    public string? Description { get; set; }

    public TagCategory Category { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
