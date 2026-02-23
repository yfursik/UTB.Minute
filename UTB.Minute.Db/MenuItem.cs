using System.ComponentModel.DataAnnotations;

namespace UTB.Minute.Db;

public class MenuItem 
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    
    public int FoodId { get; set; }
    public Food Food { get; set; } = null!;
    
    public int AvailablePortions { get; set; }
    
    // [Timestamp] добавляет защиту (RowVersion) от ситуации, 
    // когда два студента одновременно заказывают последнюю порцию (за это дают доп. баллы)
    [Timestamp] 
    public byte[] RowVersion { get; set; } = []; 
}