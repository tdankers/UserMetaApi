using BbtEntities.Models;

namespace UserMetaApi.Interfaces
{

    public interface IUserMetaService
    {
        Task CaptureAsync(Guid token, HttpContext context);
    }
}
