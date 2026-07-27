using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Comments.Domain.Exceptions;

public class CommentAccessDeniedException()
    : BaseException("Você não tem permissão para modificar este comentário.", HttpStatusCode.Forbidden);
