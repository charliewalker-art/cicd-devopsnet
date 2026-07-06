using devopsnet.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace devopsnet.Services
{
    public interface IArgoCDService
    {
        Task<List<ArgoApplicationDto>> GetAllApplicationsAsync();
       //Task<int> CreateApplicationAsync(ArgoApplicationCreateDto dto); // <--- AJOUTE CETTE LIGNE
    }
}