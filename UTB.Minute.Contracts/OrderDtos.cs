using System;

namespace UTB.Minute.Contracts;

public enum OrderStatus { Preparing, Ready, Cancelled, Completed }

public record OrderDto(int Id, MenuItemDto MenuItem, OrderStatus Status, DateTime CreatedAt);
public record CreateOrderDto(int MenuItemId);
public record UpdateOrderStatusDto(OrderStatus Status);