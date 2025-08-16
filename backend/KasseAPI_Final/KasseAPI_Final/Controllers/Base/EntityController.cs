using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Controllers.Base
{
    /// <summary>
    /// Generic CRUD işlemleri için base controller
    /// </summary>
    /// <typeparam name="T">Entity tipi</typeparam>
    public abstract class EntityController<T> : BaseController where T : class, IEntity
    {
        protected readonly IGenericRepository<T> _repository;

        protected EntityController(IGenericRepository<T> repository, ILogger logger) : base(logger)
        {
            _repository = repository;
        }

        /// <summary>
        /// Tüm entity'leri getir (sayfalama ile)
        /// </summary>
        [HttpGet]
        public virtual async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var (items, totalCount) = await _repository.GetPagedAsync(validPageNumber, validPageSize);
                
                var response = new
                {
                    items = items,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / validPageSize)
                    }
                };

                return SuccessResponse(response, $"Retrieved {items.Count()} {typeof(T).Name} entities");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetAll {typeof(T).Name}");
            }
        }

        /// <summary>
        /// ID'ye göre entity getir
        /// </summary>
        [HttpGet("{id}")]
        public virtual async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var entity = await _repository.GetByIdAsync(id);
                
                if (entity == null)
                {
                    return ErrorResponse($"{typeof(T).Name} with ID {id} not found", 404);
                }

                return SuccessResponse(entity, $"{typeof(T).Name} retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetById {typeof(T).Name} with ID {id}");
            }
        }

        /// <summary>
        /// Yeni entity oluştur
        /// </summary>
        [HttpPost]
        public virtual async Task<IActionResult> Create([FromBody] T entity)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var createdEntity = await _repository.AddAsync(entity);
                
                return CreatedAtAction(nameof(GetById), new { id = createdEntity.Id }, 
                    SuccessResponse(createdEntity, $"{typeof(T).Name} created successfully"));
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Create {typeof(T).Name}");
            }
        }

        /// <summary>
        /// Entity güncelle
        /// </summary>
        [HttpPut("{id}")]
        public virtual async Task<IActionResult> Update(Guid id, [FromBody] T entity)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                if (id != entity.Id)
                {
                    return ErrorResponse("ID mismatch between URL and request body", 400);
                }

                var updatedEntity = await _repository.UpdateAsync(entity);
                
                return SuccessResponse(updatedEntity, $"{typeof(T).Name} updated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Update {typeof(T).Name} with ID {id}");
            }
        }

        /// <summary>
        /// Entity sil (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var deleted = await _repository.DeleteAsync(id);
                
                if (!deleted)
                {
                    return ErrorResponse($"{typeof(T).Name} with ID {id} not found", 404);
                }

                return SuccessResponse(new { id }, $"{typeof(T).Name} deleted successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Delete {typeof(T).Name} with ID {id}");
            }
        }

        /// <summary>
        /// Entity'yi kalıcı olarak sil
        /// </summary>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "Administrator")] // Sadece admin kalıcı silebilir
        public virtual async Task<IActionResult> HardDelete(Guid id)
        {
            try
            {
                var deleted = await _repository.HardDeleteAsync(id);
                
                if (!deleted)
                {
                    return ErrorResponse($"{typeof(T).Name} with ID {id} not found", 404);
                }

                return SuccessResponse(new { id }, $"{typeof(T).Name} permanently deleted");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"HardDelete {typeof(T).Name} with ID {id}");
            }
        }

        /// <summary>
        /// Entity sayısını getir
        /// </summary>
        [HttpGet("count")]
        public virtual async Task<IActionResult> GetCount()
        {
            try
            {
                var count = await _repository.CountAsync();
                
                return SuccessResponse(new { count }, $"Total {typeof(T).Name} count retrieved");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"GetCount {typeof(T).Name}");
            }
        }

        /// <summary>
        /// Entity'nin var olup olmadığını kontrol et
        /// </summary>
        [HttpGet("{id}/exists")]
        public virtual async Task<IActionResult> Exists(Guid id)
        {
            try
            {
                var exists = await _repository.ExistsAsync(id);
                
                return SuccessResponse(new { exists }, $"Existence check completed for {typeof(T).Name} with ID {id}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Exists {typeof(T).Name} with ID {id}");
            }
        }
    }
}
