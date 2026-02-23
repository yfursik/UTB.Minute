namespace UTB.Minute.Contracts;

public record FoodDto(int Id, string Name, string Description, decimal Price, bool IsActive);
public record CreateFoodDto(string Name, string Description, decimal Price);
public record UpdateFoodDto(string Name, string Description, decimal Price, bool IsActive);