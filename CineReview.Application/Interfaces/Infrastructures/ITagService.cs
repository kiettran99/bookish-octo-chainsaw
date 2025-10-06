using CineReview.Domain.Models.TagModels;
using Common.Models;

namespace CineReview.Application.Interfaces.Infrastructures;

/// <summary>
/// Service interface for Tag CRUD operations
/// </summary>
public interface ITagService
{
    Task<ServiceResponse<TagResponseModel>> CreateTagAsync(CreateTagRequestModel request);
    Task<ServiceResponse<TagResponseModel>> UpdateTagAsync(UpdateTagRequestModel request);
    Task<ServiceResponse<bool>> DeleteTagAsync(int tagId);
    Task<ServiceResponse<TagResponseModel>> GetTagByIdAsync(int tagId);
    Task<ServiceResponse<List<TagResponseModel>>> GetAllTagsAsync(TagFilterRequestModel? filter = null);
    Task<ServiceResponse<List<TagResponseModel>>> GetActiveTagsAsync();
    Task<ServiceResponse<Dictionary<string, List<TagResponseModel>>>> GetTagsByCategoryAsync();
}
