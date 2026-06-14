using System.Net;

namespace EbenezerBackend.Shared.Web.Exceptions;

public class ProfileNotFoundException(string profileIdentifier) : BaseException($"O usuário \"{profileIdentifier}\" não existe!", HttpStatusCode.NotFound);