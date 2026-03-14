using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Dto.Bookings.Requests;

public class BookingFilterDto
{
    // Пагинация
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    // Сортировка
    public string? SortBy { get; set; } = "ArrivalDate"; // ArrivalDate, GuestName, ReservedUnit
    public bool SortDesc { get; set; } = true;

    // Фильтры
    public string? SearchTerm { get; set; } // поиск по имени/телефону
    public ReservedUnits? ReservedUnit { get; set; }
    public bool? IsFirstTimeGuest { get; set; }
    public bool? NeedsSauna { get; set; }
    public bool? NeedsBanquetHall { get; set; }
    public bool? HasDog { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}