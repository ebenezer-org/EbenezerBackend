using System.Net;

namespace EbenezerBackend.Shared.Web.Exceptions;

public class NotFoundException(string message) : BaseException(message, HttpStatusCode.NotFound);