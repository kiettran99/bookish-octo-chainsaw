using CineReview.Domain.AggregatesModel.UserAggregates;
using Common.Helpers;
using Common.SeedWork;
using Common.Shared.Models.Users;
using MassTransit;

namespace CineReview.Infrastructure.Messaging.Comsumers;

public class SyncUserPortalComsumer : IConsumer<SyncUserPortalMessage>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericRepository<User> _userRepository;

    public SyncUserPortalComsumer(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _userRepository = unitOfWork.Repository<User>();
    }

    public async Task Consume(ConsumeContext<SyncUserPortalMessage> context)
    {
        var syncUserMessage = context.Message;

        var user = await _userRepository.GetByIdAsync(syncUserMessage.UserId);
        if (user == null && syncUserMessage.IsNewUser)
        {
            user = new User
            {
                Id = syncUserMessage.UserId,
                Email = syncUserMessage.Email,
                FullName = syncUserMessage.FullName,
                UserName = syncUserMessage.UserName,
                Avatar = syncUserMessage.Avatar,
                Region = CommonHelper.GetRegionByName(syncUserMessage.Region),
                ProviderAccountId = syncUserMessage.ProviderAccountId
            };
            _userRepository.Add(user);

        }
        else if (user != null)
        {
            user.FullName = syncUserMessage.FullName;
            user.Avatar = syncUserMessage.IsUpdateAvatar ? syncUserMessage.Avatar : user.Avatar;
            user.IsBanned = syncUserMessage.IsBanned;

            _userRepository.Update(user);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}