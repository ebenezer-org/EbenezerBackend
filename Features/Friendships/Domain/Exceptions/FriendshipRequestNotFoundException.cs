using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Friendships.Domain.Exceptions;

public class FriendshipRequestNotFoundException(string? withName = null) : BaseException(
    withName == null ?
        "Solicitação de amizade não encontrada." :
        $"Não existe uma solicitação de amizade sua com {withName}.",
    HttpStatusCode.NotFound
    );