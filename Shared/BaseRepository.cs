using EbenezerBackend.Shared.CustomAttributes;

namespace EbenezerBackend.Shared;

public class BaseRepository<TDataModel>
{
    protected static readonly string CollectionName =
        ((CollectionNameAttribute)Attribute.GetCustomAttribute(typeof(TDataModel), typeof(CollectionNameAttribute))!).Name;
}