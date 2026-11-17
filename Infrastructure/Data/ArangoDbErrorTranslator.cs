using System.Net;
using ArangoDBNetStandard;

namespace EbenezerBackend.Infrastructure.Data;

public static class ArangoDbErrorTranslator
{
    public const int UniqueConstraintViolated = 1210;

    public static bool IsUniqueConstraintViolation(ApiErrorException ex)
        => ex.ApiError.Code == HttpStatusCode.Conflict
           && ex.ApiError.ErrorNum == UniqueConstraintViolated;
}
