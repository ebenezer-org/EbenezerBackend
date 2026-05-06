using System.Net;
using EbenezerBackend.Shared.Exceptions;

namespace EbenezerBackend.Features.Auth.Domain.Exceptions;

public class LoginFailedException() : BaseException("Login failed", HttpStatusCode.Unauthorized);