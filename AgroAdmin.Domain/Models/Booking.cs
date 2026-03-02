using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Domain.Models;

public class Booking
{
    public int Id { get; private set; }
    public string GuestName { get; private set; } = string.Empty;
    public string? GuestPhone { get; private set; } // Добавили телефон
    public DateTime ArrivalDate { get; private set; }
    public DateTime DepartureDate { get; private set; }
    public ReservedUnits ReservedUnit { get; private set; }

    public int TotalGuestsCount { get; private set; }
    public int AdultsCount { get; private set; }
    public int ChildrenCount { get; private set; }
    public int InfantsCount { get; private set; } // До 6 лет
    public bool HasDog { get; private set; }
    public bool IsFirstTimeGuest { get; private set; }

    public string? AdminNotes { get; private set; } // Твои заметки

    private readonly List<SaunaOrder> _saunaOrders = new();
    public virtual IReadOnlyCollection<SaunaOrder> SaunaOrders => _saunaOrders.AsReadOnly();

    private Booking() { }

    public Booking(
        string guestName,
        DateTime arrival,
        DateTime departure,
        ReservedUnits unit,
        int totalGuests,
        int adults,
        int children,
        int infants,
        bool hasDog,
        bool isFirstTimeGuest,
        string? adminNotes = null,
        string? guestPhone = null)
    {
        if (departure <= arrival) throw new Exception("Check-out must be after check-in");

        GuestName = guestName;
        ArrivalDate = DateTime.SpecifyKind(arrival, DateTimeKind.Utc);
        DepartureDate = DateTime.SpecifyKind(departure, DateTimeKind.Utc);
        ReservedUnit = unit;

        TotalGuestsCount = totalGuests;
        AdultsCount = adults;
        ChildrenCount = children;
        InfantsCount = infants;

        HasDog = hasDog;
        IsFirstTimeGuest = isFirstTimeGuest;
        AdminNotes = adminNotes;
        GuestPhone = guestPhone;
    }

    public void AddSaunaOrder(DateTime scheduledTime, int durationHours)
    {
        if (scheduledTime < ArrivalDate || scheduledTime > DepartureDate.AddDays(1))
            throw new Exception("Sauna time is outside of booking dates");

        _saunaOrders.Add(new SaunaOrder(scheduledTime, durationHours));
    }
}