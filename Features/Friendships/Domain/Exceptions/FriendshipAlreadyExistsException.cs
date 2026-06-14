using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Exceptions;

public class FriendshipAlreadyExistsException(string friendName) : BaseException($"{friendName} já é seu amigo!", HttpStatusCode.Conflict);