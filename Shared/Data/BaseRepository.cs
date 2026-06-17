using EbenezerBackend.Shared.CustomAttributes;

namespace EbenezerBackend.Shared.Data;

public abstract class BaseRepository<TDataModel>
{
    protected static readonly string CollectionName =
        ((CollectionNameAttribute)Attribute.GetCustomAttribute(typeof(TDataModel), typeof(CollectionNameAttribute))!).Name;
}