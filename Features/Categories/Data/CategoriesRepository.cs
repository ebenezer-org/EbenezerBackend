using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EbenezerBackend.Features.Categories.Data;

public class CategoriesRepository(IMongoDatabase database) : BaseRepository<CategoryModel>, ICategoriesRepository
{
    private readonly IMongoCollection<CategoryModel> _collection = database.GetCollection<CategoryModel>(CollectionName);

    private readonly IMongoCollection<PrayerModel> _prayers = database.GetCollection<PrayerModel>(DbCollections.Prayers);

    public async Task<CategoryEntity> CreateCategoryAsync(CategoryEntity category, CancellationToken ct)
    {
        var model = CategoryModel.FromEntity(category);

        await _collection.InsertOneAsync(model, cancellationToken: ct);

        category.Id = model.Id;
        return category;
    }

    public async Task<CategoryEntity?> FindCategoryByIdAsync(string categoryId, CancellationToken ct)
    {
        if (!ObjectId.TryParse(categoryId, out _))
        {
            return null;
        }

        var filter = Builders<CategoryModel>.Filter.Eq(x => x.Id, categoryId);
        var model = await _collection.Find(filter).FirstOrDefaultAsync(ct);

        return model?.ToEntity();
    }

    public async Task<IReadOnlyCollection<CategoryEntity>> FindCategoriesByUsernameAsync(
        string username, bool includePrivate, CancellationToken ct)
    {
        var filter = Builders<CategoryModel>.Filter.Eq(x => x.OwnerUsername, username);

        if (!includePrivate)
        {
            filter &= Builders<CategoryModel>.Filter.Eq(x => x.IsPublic, true);
        }

        var models = await _collection.Find(filter).ToListAsync(ct);

        // Ordena sem diferenciar maiúsculas de minúsculas (igual ao SORT LOWER(Name) do ArangoDB).
        return models
            .OrderBy(model => model.Name.ToLowerInvariant(), StringComparer.Ordinal)
            .Select(model => model.ToEntity())
            .ToList()
            .AsReadOnly();
    }

    public async Task<CategoryEntity?> UpdateCategoryAsync(string categoryId, CategoryEntity category, CancellationToken ct)
    {
        if (!ObjectId.TryParse(categoryId, out _))
        {
            return null;
        }

        var filter = Builders<CategoryModel>.Filter.And(
            Builders<CategoryModel>.Filter.Eq(x => x.Id, categoryId),
            Builders<CategoryModel>.Filter.Eq(x => x.OwnerUsername, category.OwnerUsername)
        );

        var update = Builders<CategoryModel>.Update
            .Set(x => x.Name, category.Name)
            .Set(x => x.Description, category.Description)
            .Set(x => x.ColorHex, category.ColorHex)
            .Set(x => x.IsPublic, category.IsPublic)
            .Set(x => x.UpdatedAt, category.UpdatedAt);

        var options = new FindOneAndUpdateOptions<CategoryModel> { ReturnDocument = ReturnDocument.After };

        var model = await _collection.FindOneAndUpdateAsync(filter, update, options, ct);

        return model?.ToEntity();
    }

    public async Task<bool> DeleteCategoryAsync(string categoryId, string ownerUsername, CancellationToken ct)
    {
        if (!ObjectId.TryParse(categoryId, out _))
        {
            return false;
        }

        var filter = Builders<CategoryModel>.Filter.And(
            Builders<CategoryModel>.Filter.Eq(x => x.Id, categoryId),
            Builders<CategoryModel>.Filter.Eq(x => x.OwnerUsername, ownerUsername)
        );

        var result = await _collection.DeleteOneAsync(filter, ct);

        if (result.DeletedCount == 0)
        {
            return false;
        }

        // Equivalente à remoção das arestas CategorizedAs no ArangoDB: as orações deixam de
        // referenciar a categoria excluída.
        await _prayers.UpdateManyAsync(
            Builders<PrayerModel>.Filter.AnyEq(p => p.CategoryIds, categoryId),
            Builders<PrayerModel>.Update.Pull(p => p.CategoryIds, categoryId),
            cancellationToken: ct);

        return true;
    }
}
