namespace UTB.Minute.Contracts;

public record MenuItemDto(int Id, DateOnly Date, FoodDto Food, int AvailablePortions);
public record CreateMenuItemDto(DateOnly Date, int FoodId, int AvailablePortions);
public record UpdateMenuItemDto(DateOnly Date, int AvailablePortions);
public record CopyMenuDto(DateOnly FromDate, DateOnly ToDate);