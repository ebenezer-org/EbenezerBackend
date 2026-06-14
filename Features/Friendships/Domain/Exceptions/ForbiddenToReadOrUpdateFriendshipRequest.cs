using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Exceptions;

public class ForbiddenToReadOrUpdateFriendshipRequest() : BaseException(
    "Você não tem permissão para ver ou atualizar solicitações de amizade que não são endereçadas para você.",
    HttpStatusCode.Forbidden
    );