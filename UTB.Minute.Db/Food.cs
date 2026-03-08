using System.ComponentModel.DataAnnotations;

namespace UTB.Minute.Db;

public class Food 
{
    public int Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public bool IsActive { get; set; } = true;
}