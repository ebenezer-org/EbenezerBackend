using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Exceptions;

public class CannotBefriendYourselfException() : BaseException("Você não pode adicionar a você mesmo como amigo.", HttpStatusCode.BadRequest);