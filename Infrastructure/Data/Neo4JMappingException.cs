using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Infrastructure.Data;

public class Neo4JMappingException(string message)
    : BaseException(message, HttpStatusCode.InternalServerError);
