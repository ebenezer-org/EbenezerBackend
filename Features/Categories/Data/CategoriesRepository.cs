using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Data;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Categories.Data;

public class CategoriesRepository(IArangoDBClient db) : BaseRepository<CategoryModel>, ICategoriesRepository
{
    public async Task<CategoryEntity> CreateCategoryAsync(CategoryEntity category, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @ownerUsername
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            INSERT @category INTO {CollectionName}
            LET newCategory = NEW

            INSERT {{
                _from: user._id,
                _to: newCategory._id,
                CreatedAt: DATE_ISO8601(DATE_NOW())
            }} INTO {ArangoDbEdges.CreatedCategory}

            RETURN newCategory
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "ownerUsername", category.OwnerUsername },
            { "category", CategoryModel.FromEntity(category) }
        };

        var response = await db.Cursor.PostCursorAsync<CategoryModel>(query, bindVars, token: ct);
        var result = response.Result.FirstOrDefault();

        return result?.ToEntity() ?? throw new NotFoundException("Usuário dono da categoria não encontrado");
    }

    public async Task<CategoryEntity?> FindCategoryByIdAsync(string categoryId, CancellationToken ct)
    {
        var query = $@"
            FOR category IN {CollectionName}
                FILTER category._key == @categoryId || category._id == @categoryId
                LIMIT 1
                RETURN category
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "categoryId", categoryId }
        };

        var response = await db.Cursor.PostCursorAsync<CategoryModel>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault()?.ToEntity();
    }

    public async Task<IReadOnlyCollection<CategoryEntity>> FindCategoriesByUsernameAsync(string username, bool includePrivate, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @username
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            FOR category IN 1..1 OUTBOUND user._id {ArangoDbEdges.CreatedCategory}
                FILTER @includePrivate || category.IsPublic == true
                SORT LOWER(category.Name)
                RETURN category
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "username", username },
            { "includePrivate", includePrivate }
        };

        var response = await db.Cursor.PostCursorAsync<CategoryModel>(query, bindVars, token: ct);

        return response.Result.Select(category => category.ToEntity()).ToList();
    }

    public async Task<CategoryEntity?> UpdateCategoryAsync(string categoryId, CategoryEntity category, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @ownerUsername
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET categoryToUpdate = FIRST(
                FOR currentCategory IN {CollectionName}
                    FILTER currentCategory._key == @categoryId || currentCategory._id == @categoryId
                    LET ownerLink = FIRST(
                        FOR edge IN {ArangoDbEdges.CreatedCategory}
                            FILTER edge._from == user._id && edge._to == currentCategory._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentCategory
            )

            FILTER categoryToUpdate != null

            UPDATE categoryToUpdate WITH @category IN {CollectionName}
            RETURN NEW
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "categoryId", categoryId },
            { "ownerUsername", category.OwnerUsername },
            {
                "category",
                new Dictionary<string, object>
                {
                    { "Name", category.Name },
                    { "Description", category.Description },
                    { "ColorHex", category.ColorHex },
                    { "IsPublic", category.IsPublic },
                    { "UpdatedAt", category.UpdatedAt }
                }
            }
        };

        var response = await db.Cursor.PostCursorAsync<CategoryModel>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault()?.ToEntity();
    }

    public async Task<bool> DeleteCategoryAsync(string categoryId, string ownerUsername, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @ownerUsername
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET categoryToDelete = FIRST(
                FOR currentCategory IN {CollectionName}
                    FILTER currentCategory._key == @categoryId || currentCategory._id == @categoryId
                    LET ownerLink = FIRST(
                        FOR edge IN {ArangoDbEdges.CreatedCategory}
                            FILTER edge._from == user._id && edge._to == currentCategory._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentCategory
            )

            FILTER categoryToDelete != null

            LET removedOwnershipLinks = (
                FOR edge IN {ArangoDbEdges.CreatedCategory}
                    FILTER edge._to == categoryToDelete._id
                    REMOVE edge IN {ArangoDbEdges.CreatedCategory}
                    RETURN OLD
            )

            LET removedCategoryLinks = (
                FOR categoryEdge IN CategorizedAs
                    FILTER categoryEdge._to == categoryToDelete._id
                    REMOVE categoryEdge IN CategorizedAs
                    RETURN OLD
            )

            REMOVE categoryToDelete IN {CollectionName}
            RETURN MERGE(OLD, {{
                RemovedOwnershipLinks: LENGTH(removedOwnershipLinks),
                RemovedCategoryLinks: LENGTH(removedCategoryLinks)
            }})
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "categoryId", categoryId },
            { "ownerUsername", ownerUsername }
        };

        var response = await db.Cursor.PostCursorAsync<CategoryModel>(query, bindVars, token: ct);

        return response.Result.Any();
    }
}
