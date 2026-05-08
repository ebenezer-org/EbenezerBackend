using System.Net;

namespace EbenezerBackend.Shared.Exceptions;

public class InternalException(string message) : BaseException($"INTERNAL EXCEPTION: {message}", HttpStatusCode.InternalServerError) {}