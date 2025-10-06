# CineReview - Pattern Documentation: Entity → Service → API

This document describes the standard pattern for implementing new features in the CineReview application, using the Tag feature as a reference example.

## Architecture Overview

The application follows a clean architecture pattern with clear separation of concerns:

```
CineReview.Domain        → Entities, Enums, Models (no dependencies)
CineReview.Infrastructure → EF Core, Database Configuration
CineReview.Application    → Business Logic, Services
CineReview.API           → Controllers, HTTP Endpoints
```

## Implementation Steps

### 1. Define Domain Entity

**Location**: `CineReview.Domain/AggregatesModel/{Feature}Aggregates/`

Create the entity class that represents your database table:

```csharp
using Common.SeedWork;
using CineReview.Domain.Enums;

public class Tag : Entity  // Entity provides Id, CreatedOnUtc, UpdatedOnUtc
{
    [Column(TypeName = "TEXT")]  // SQLite-compatible type
    public string Name { get; set; } = null!;
    
    public TagCategory Category { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation properties if needed
    public virtual ICollection<RelatedEntity> RelatedItems { get; set; }
}
```

**Key Points**:
- Inherit from `Entity` base class for common fields (Id, CreatedOnUtc, UpdatedOnUtc)
- Use `[Column(TypeName = "TEXT")]` for string fields in SQLite
- Avoid `nvarchar(max)` - use `TEXT` for SQLite compatibility
- Mark nullable fields with `?`
- Use `= null!` for required reference types

### 2. Create Enums (if needed)

**Location**: `CineReview.Domain/Enums/`

```csharp
public enum TagCategory
{
    Content = 0,
    Acting = 1,
    AudioVisual = 2,
    TheaterExperience = 3
}
```

### 3. Configure Entity in Infrastructure

**Location**: `CineReview.Infrastructure/EntityConfigurations/{Feature}Aggregates/`

```csharp
public class TagEntityTypeConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable(nameof(Tag));
        builder.HasKey(x => x.Id);
        
        // Required fields
        builder.Property(x => x.Name).IsRequired();
        
        // Default values
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        
        // Indexes for performance
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => new { x.Category, x.DisplayOrder });
        
        // Relationships
        builder.HasMany(x => x.RelatedItems)
               .WithOne(x => x.Tag)
               .HasForeignKey(x => x.TagId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**EF Core automatically discovers configurations** in the assembly through:
```csharp
// In ApplicationDbContext.OnModelCreating:
modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
```

### 4. Create Request/Response Models

**Location**: `CineReview.Domain/Models/{Feature}Models/`

```csharp
// Request models - what clients send
public class CreateTagRequestModel
{
    public string Name { get; set; } = null!;
    public TagCategory Category { get; set; }
}

public class UpdateTagRequestModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
}

// Response models - what API returns
public class TagResponseModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public TagCategory Category { get; set; }
    public string CategoryName { get; set; } = null!;  // Human-readable
    public DateTime CreatedOnUtc { get; set; }
}

// Filter models - for search/listing
public class TagFilterRequestModel
{
    public TagCategory? Category { get; set; }
    public bool? IsActive { get; set; }
}
```

### 5. Create Service Interface

**Location**: `CineReview.Application/Interfaces/Infrastructures/`

```csharp
public interface ITagService
{
    Task<ServiceResponse<TagResponseModel>> CreateTagAsync(CreateTagRequestModel request);
    Task<ServiceResponse<TagResponseModel>> UpdateTagAsync(UpdateTagRequestModel request);
    Task<ServiceResponse<bool>> DeleteTagAsync(int tagId);
    Task<ServiceResponse<TagResponseModel>> GetTagByIdAsync(int tagId);
    Task<ServiceResponse<List<TagResponseModel>>> GetAllTagsAsync(TagFilterRequestModel? filter = null);
}
```

### 6. Implement Service

**Location**: `CineReview.Application/Implements/Infrastructures/`

```csharp
public class TagService : ITagService
{
    private readonly IUnitOfWork _unitOfWork;

    public TagService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResponse<TagResponseModel>> CreateTagAsync(CreateTagRequestModel request)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new ServiceResponse<TagResponseModel>("Name is required");
            }

            // Business logic
            var tag = new Tag
            {
                Name = request.Name,
                Category = request.Category,
                IsActive = true
            };

            // Save to database
            _unitOfWork.Repository<Tag>().Add(tag);
            await _unitOfWork.SaveChangesAsync();

            // Map to response
            var response = MapToResponseModel(tag);
            return new ServiceResponse<TagResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<TagResponseModel>(ex.Message);
        }
    }

    private TagResponseModel MapToResponseModel(Tag tag)
    {
        return new TagResponseModel
        {
            Id = tag.Id,
            Name = tag.Name,
            Category = tag.Category,
            CategoryName = GetCategoryName(tag.Category),
            CreatedOnUtc = tag.CreatedOnUtc
        };
    }
}
```

**ServiceResponse Pattern**:
- Success: `new ServiceResponse<T>(data)`
- Error: `new ServiceResponse<T>("error message")`
- Check: `response.IsSuccess`, `response.Data`, `response.ErrorMessage`

### 7. Create Controller

**Location**: `CineReview.API/Controllers/`

```csharp
[ApiController]
[Route("api/[controller]")]
public class TagController : CommonController
{
    private readonly ITagService _tagService;

    public TagController(ITagService tagService)
    {
        _tagService = tagService;
    }

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

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequestModel request)
    {
        var response = await _tagService.CreateTagAsync(request);
        
        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }
        
        return CreatedAtAction(nameof(GetTagById), new { id = response.Data?.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateTag(int id, [FromBody] UpdateTagRequestModel request)
    {
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

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var response = await _tagService.DeleteTagAsync(id);
        
        if (!response.IsSuccess)
        {
            return BadRequest(response);
        }
        
        return Ok(response);
    }
}
```

**Controller Best Practices**:
- Inherit from `CommonController` for common utilities
- Use `[Authorize]` attribute for protected endpoints
- Return appropriate HTTP status codes
- Use `CreatedAtAction` for POST endpoints
- Validate ID matching for PUT endpoints

### 8. Register Service (Dependency Injection)

**Location**: `CineReview.API/Extensions/BusinessServiceExtension.cs`

```csharp
public static class BusinessServiceExtension
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<ITagService, TagService>();
        // Add other services...
        
        return services;
    }
}
```

### 9. Create and Apply Migration

```bash
cd CineReview.Infrastructure

# Create migration
dotnet ef migrations add AddTagEntity \
  -p CineReview.Infrastructure.csproj \
  -c ApplicationDbContext \
  -s ../CineReview.API/CineReview.API.csproj \
  --verbose

# Apply migration
dotnet ef database update \
  -p CineReview.Infrastructure.csproj \
  -s ../CineReview.API/CineReview.API.csproj \
  --verbose
```

## Common Patterns & Helpers

### Getting User ID from JWT Token

```csharp
public class MyController : CommonController
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> MyAction()
    {
        var userId = GetUserIdByToken();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        
        // Use userId.Value
    }
}
```

### Querying with UnitOfWork

```csharp
// Get by ID
var entity = await _unitOfWork.Repository<Tag>().GetByIdAsync(id);

// Get queryable for complex queries
var tags = await _unitOfWork.Repository<Tag>().GetQueryable()
    .Where(t => t.IsActive)
    .OrderBy(t => t.Name)
    .ToListAsync();

// Add entity
_unitOfWork.Repository<Tag>().Add(entity);
await _unitOfWork.SaveChangesAsync();

// Update entity
_unitOfWork.Repository<Tag>().Update(entity);
await _unitOfWork.SaveChangesAsync();

// Delete (usually soft delete)
entity.IsActive = false;
_unitOfWork.Repository<Tag>().Update(entity);
await _unitOfWork.SaveChangesAsync();
```

### Working with JSON Fields

For storing complex data as JSON in TEXT fields:

```csharp
using System.Text.Json;

// Serialize to JSON
var jsonString = JsonSerializer.Serialize(myList);
review.DescriptionTag = jsonString;

// Deserialize from JSON
var myList = JsonSerializer.Deserialize<List<TagRatingModel>>(review.DescriptionTag);
```

Example for Tag Ratings:
```csharp
public class TagRatingModel
{
    public int TagId { get; set; }
    public string TagName { get; set; } = null!;
    public int Rating { get; set; }
}

// Store in Review.DescriptionTag
var tagRatings = new List<TagRatingModel>
{
    new() { TagId = 1, TagName = "Plot twist bất ngờ", Rating = 9 },
    new() { TagId = 5, TagName = "Diễn xuất ấn tượng", Rating = 8 }
};
review.DescriptionTag = JsonSerializer.Serialize(tagRatings);
```

## Database Tips

### SQLite Data Types
- Use `TEXT` for strings (not `nvarchar(max)`)
- Use `INTEGER` for int, bool, enums
- Use `REAL` for double, decimal
- Use `TEXT` for DateTime (stored as ISO 8601 strings)

### Common Indexes
```csharp
// Single column
builder.HasIndex(x => x.UserId);

// Composite index
builder.HasIndex(x => new { x.UserId, x.MovieId });

// Unique constraint
builder.HasIndex(x => x.Email).IsUnique();

// Unique composite
builder.HasIndex(x => new { x.UserId, x.ReviewId }).IsUnique();
```

## Testing Endpoints

### Using curl

```bash
# GET all tags
curl -X GET "http://localhost:5000/api/tag"

# GET tags by category
curl -X GET "http://localhost:5000/api/tag?Category=0"

# GET by ID
curl -X GET "http://localhost:5000/api/tag/1"

# POST create (requires auth)
curl -X POST "http://localhost:5000/api/tag" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Cốt truyện hấp dẫn",
    "category": 0,
    "displayOrder": 1
  }'

# PUT update (requires auth)
curl -X PUT "http://localhost:5000/api/tag/1" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "name": "Cốt truyện rất hấp dẫn",
    "category": 0,
    "isActive": true,
    "displayOrder": 1
  }'

# DELETE (requires auth)
curl -X DELETE "http://localhost:5000/api/tag/1" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Checklist for New Features

- [ ] Create Entity in `Domain/AggregatesModel/`
- [ ] Create Enums in `Domain/Enums/` (if needed)
- [ ] Create EntityConfiguration in `Infrastructure/EntityConfigurations/`
- [ ] Create Models in `Domain/Models/`
- [ ] Create Service Interface in `Application/Interfaces/`
- [ ] Implement Service in `Application/Implements/`
- [ ] Create Controller in `API/Controllers/`
- [ ] Register Service in `BusinessServiceExtension`
- [ ] Create Migration: `dotnet ef migrations add <Name>`
- [ ] Apply Migration: `dotnet ef database update`
- [ ] Test all endpoints
- [ ] Update this documentation if pattern changes

## Example: Complete Feature Implementation

See the Tag feature implementation as a reference:
- Entity: `CineReview.Domain/AggregatesModel/TagAggregates/Tag.cs`
- Configuration: `CineReview.Infrastructure/EntityConfigurations/TagAggregates/TagEntityTypeConfiguration.cs`
- Models: `CineReview.Domain/Models/TagModels/TagModels.cs`
- Service: `CineReview.Application/Implements/Infrastructures/TagService.cs`
- Controller: `CineReview.API/Controllers/TagController.cs`
- Migration: `CineReview.Infrastructure/Migrations/*_AddTagEntity.cs`

---

*Last Updated: October 6, 2025*
