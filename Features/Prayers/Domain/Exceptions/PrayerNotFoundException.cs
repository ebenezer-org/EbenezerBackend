using System.Net;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Prayers.Domain.Exceptions;

public class PrayerNotFoundException() : BaseException("Oração não encontrada", HttpStatusCode.NotFound);