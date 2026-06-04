namespace EbenezerBackend.Infrastructure.Data;

// ReSharper disable once TypeParameterCanBeVariant
public interface IBaseModel<TModel, TEntity> where TModel : IBaseModel<TModel, TEntity>
{
    public abstract TEntity ToEntity();
    public static abstract TModel FromEntity(TEntity entity);
    
}