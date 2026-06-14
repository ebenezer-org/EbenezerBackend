using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Exceptions;

public class CannotRemoveFriendshipNonExistentException()
    : BaseException("Vocês já não são amigos!", HttpStatusCode.BadRequest); 