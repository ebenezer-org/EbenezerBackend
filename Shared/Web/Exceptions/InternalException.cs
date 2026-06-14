using System.Net;

namespace EbenezerBackend.Shared.Web.Exceptions;

public class InternalException(string message) : BaseException($"INTERNAL EXCEPTION: {message}", HttpStatusCode.InternalServerError) {}