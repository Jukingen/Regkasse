using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Tüm entity'ler için ortak interface
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Entity'nin benzersiz kimliği
        /// </summary>
        Guid Id { get; set; }
        
        /// <summary>
        /// Entity'nin oluşturulma tarihi
        /// </summary>
        DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Entity'nin son güncellenme tarihi
        /// </summary>
        DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Entity'nin aktif olup olmadığı
        /// </summary>
        bool IsActive { get; set; }
    }
}
