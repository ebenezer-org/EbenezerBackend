using System.Net;

namespace EbenezerBackend.Shared.Web.Exceptions;

public class UnauthorizedException() : BaseException("Usuário não autenticado", HttpStatusCode.Unauthorized){}