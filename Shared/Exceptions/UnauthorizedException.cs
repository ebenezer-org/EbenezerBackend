using System.Net;

namespace EbenezerBackend.Shared.Exceptions;

public class UnauthorizedException() : BaseException("Usuário não autenticado", HttpStatusCode.Unauthorized){}