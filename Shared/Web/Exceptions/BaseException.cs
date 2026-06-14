using System.Net;

namespace EbenezerBackend.Shared.Web.Exceptions;

public abstract class BaseException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}