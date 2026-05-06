using System.Net;

namespace EbenezerBackend.Shared.Exceptions;

public class NotFoundException(string message) : BaseException(message, HttpStatusCode.NotFound);