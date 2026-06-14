using System.Net;

namespace EbenezerBackend.Infrastructure.Extensions.Web;

public static class HttpCodeExtensions
{
    public static bool IsSuccess(this HttpStatusCode code)
    {    
        return (int)code >= 200 && (int)code < 300;
    }            
}