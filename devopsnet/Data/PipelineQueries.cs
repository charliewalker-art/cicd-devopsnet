using System;
using System.Linq;
using devopsnet.Models;

namespace devopsnet.Data;

public static class PipelineQueries
{
    /// <summary>
    /// Récupère uniquement les pipelines appartenant à un utilisateur spécifique.
    /// </summary>
    public static IQueryable<Pipeline> ByUserId(this IQueryable<Pipeline> query, Guid userId)
    {
        return query.Where(p => p.UserId == userId);
    }
}