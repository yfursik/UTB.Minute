using System;

namespace UTB.Minute.Contracts;

// Статусы переносим сюда, так как они нужны и базе, и клиентам
public enum OrderStatus { Preparing, Ready, Cancelled, Completed }

public record OrderDto(int Id, MenuItemDto MenuItem, OrderStatus Status, DateTime CreatedAt);
public record CreateOrderDto(int MenuItemId);
public record UpdateOrderStatusDto(OrderStatus Status);