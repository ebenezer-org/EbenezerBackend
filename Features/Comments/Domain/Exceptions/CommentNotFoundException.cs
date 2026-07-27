using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Comments.Domain.Exceptions;

public class CommentNotFoundException() : BaseException("Comentário não encontrado.", HttpStatusCode.NotFound);