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
    public static int GetMaxNodePort(this IQueryable<Pipeline> query, int defaultStartPort)
    {
        // Si la table Pipelines est vide ou qu'aucun port n'a été saisi
        if (!query.Any())
        {
            return defaultStartPort - 1;
        }

        return query.Max(p => p.NodePort);
    }

}