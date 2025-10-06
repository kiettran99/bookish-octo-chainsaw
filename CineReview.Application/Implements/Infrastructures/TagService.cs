using CineReview.Application.Interfaces.Infrastructures;
using CineReview.Domain.AggregatesModel.TagAggregates;
using CineReview.Domain.Enums;
using CineReview.Domain.Models.TagModels;
using Common.Models;
using Common.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace CineReview.Application.Implements.Infrastructures;

/// <summary>
/// Service implementation for Tag CRUD operations
/// </summary>
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
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new ServiceResponse<TagResponseModel>("Tag name is required");
            }

            // Check if tag with same name already exists in category
            var existingTag = await _unitOfWork.Repository<Tag>().GetQueryable()
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.Category == request.Category);

            if (existingTag != null)
            {
                return new ServiceResponse<TagResponseModel>("Tag with this name already exists in this category");
            }

            var tag = new Tag
            {
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                DisplayOrder = request.DisplayOrder,
                IsActive = true
            };

            _unitOfWork.Repository<Tag>().Add(tag);
            await _unitOfWork.SaveChangesAsync();

            var response = MapToResponseModel(tag);
            return new ServiceResponse<TagResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<TagResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<TagResponseModel>> UpdateTagAsync(UpdateTagRequestModel request)
    {
        try
        {
            var tag = await _unitOfWork.Repository<Tag>().GetByIdAsync(request.Id);

            if (tag == null)
            {
                return new ServiceResponse<TagResponseModel>("Tag not found");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new ServiceResponse<TagResponseModel>("Tag name is required");
            }

            // Check if another tag with same name exists in category
            var duplicateTag = await _unitOfWork.Repository<Tag>().GetQueryable()
                .FirstOrDefaultAsync(t => t.Name == request.Name && t.Category == request.Category && t.Id != request.Id);

            if (duplicateTag != null)
            {
                return new ServiceResponse<TagResponseModel>("Another tag with this name already exists in this category");
            }

            tag.Name = request.Name;
            tag.Description = request.Description;
            tag.Category = request.Category;
            tag.IsActive = request.IsActive;
            tag.DisplayOrder = request.DisplayOrder;
            tag.UpdatedOnUtc = DateTime.UtcNow;

            _unitOfWork.Repository<Tag>().Update(tag);
            await _unitOfWork.SaveChangesAsync();

            var response = MapToResponseModel(tag);
            return new ServiceResponse<TagResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<TagResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<bool>> DeleteTagAsync(int tagId)
    {
        try
        {
            var tag = await _unitOfWork.Repository<Tag>().GetByIdAsync(tagId);

            if (tag == null)
            {
                return new ServiceResponse<bool>("Tag not found");
            }

            // Soft delete - just deactivate
            tag.IsActive = false;
            tag.UpdatedOnUtc = DateTime.UtcNow;

            _unitOfWork.Repository<Tag>().Update(tag);
            await _unitOfWork.SaveChangesAsync();

            return new ServiceResponse<bool>(true);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<bool>(ex.Message);
        }
    }

    public async Task<ServiceResponse<TagResponseModel>> GetTagByIdAsync(int tagId)
    {
        try
        {
            var tag = await _unitOfWork.Repository<Tag>().GetByIdAsync(tagId);

            if (tag == null)
            {
                return new ServiceResponse<TagResponseModel>("Tag not found");
            }

            var response = MapToResponseModel(tag);
            return new ServiceResponse<TagResponseModel>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<TagResponseModel>(ex.Message);
        }
    }

    public async Task<ServiceResponse<List<TagResponseModel>>> GetAllTagsAsync(TagFilterRequestModel? filter = null)
    {
        try
        {
            var query = _unitOfWork.Repository<Tag>().GetQueryable();

            if (filter != null)
            {
                if (filter.Category.HasValue)
                {
                    query = query.Where(t => t.Category == filter.Category.Value);
                }

                if (filter.IsActive.HasValue)
                {
                    query = query.Where(t => t.IsActive == filter.IsActive.Value);
                }
            }

            var tags = await query
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            var response = tags.Select(MapToResponseModel).ToList();
            return new ServiceResponse<List<TagResponseModel>>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<List<TagResponseModel>>(ex.Message);
        }
    }

    public async Task<ServiceResponse<List<TagResponseModel>>> GetActiveTagsAsync()
    {
        try
        {
            var tags = await _unitOfWork.Repository<Tag>().GetQueryable()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            var response = tags.Select(MapToResponseModel).ToList();
            return new ServiceResponse<List<TagResponseModel>>(response);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<List<TagResponseModel>>(ex.Message);
        }
    }

    public async Task<ServiceResponse<Dictionary<string, List<TagResponseModel>>>> GetTagsByCategoryAsync()
    {
        try
        {
            var tags = await _unitOfWork.Repository<Tag>().GetQueryable()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.DisplayOrder)
                .ThenBy(t => t.Name)
                .ToListAsync();

            var grouped = tags
                .Select(MapToResponseModel)
                .GroupBy(t => t.CategoryName)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            return new ServiceResponse<Dictionary<string, List<TagResponseModel>>>(grouped);
        }
        catch (Exception ex)
        {
            return new ServiceResponse<Dictionary<string, List<TagResponseModel>>>(ex.Message);
        }
    }

    private TagResponseModel MapToResponseModel(Tag tag)
    {
        return new TagResponseModel
        {
            Id = tag.Id,
            Name = tag.Name,
            Description = tag.Description,
            Category = tag.Category,
            CategoryName = GetCategoryName(tag.Category),
            IsActive = tag.IsActive,
            DisplayOrder = tag.DisplayOrder,
            CreatedOnUtc = tag.CreatedOnUtc,
            UpdatedOnUtc = tag.UpdatedOnUtc
        };
    }

    private string GetCategoryName(TagCategory category)
    {
        return category switch
        {
            TagCategory.Content => "Nội dung phim",
            TagCategory.Acting => "Diễn xuất",
            TagCategory.AudioVisual => "Âm thanh & hình ảnh",
            TagCategory.TheaterExperience => "Trải nghiệm rạp",
            _ => category.ToString()
        };
    }
}
