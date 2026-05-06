using System.Net;
using EbenezerBackend.Shared.Exceptions;

namespace EbenezerBackend.Features.Auth.Domain.Exceptions;

public class UserNameOrEmailAlreadyRegisteredException() : BaseException("This username or email is already in use", HttpStatusCode.Conflict);