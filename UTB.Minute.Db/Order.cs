namespace UTB.Minute.Db;

using UTB.Minute.Contracts;
public class Order 
{
    public int Id { get; set; }
    
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    
    public OrderStatus Status { get; set; } = OrderStatus.Preparing;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string StudentUsername { get; set; } = string.Empty;
}