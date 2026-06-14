using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Auth.Domain.Exceptions;

public class LoginFailedException() : BaseException("Usuário e/ou senha incorreto(s)!", HttpStatusCode.Unauthorized);