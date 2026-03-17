using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Domain.Models;

public class Booking
{
    public int Id { get; private set; }
    public int GuestId { get; private set; }
    public virtual Guest Guest { get; private set; } = null!;
    public DateTime ArrivalDate { get; private set; } = DateTime.UtcNow;
    public DateTime DepartureDate { get; private set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public ReservedUnits ReservedUnit { get; private set; }
    public bool NeedsSauna { get; private set; }
    public bool NeedsBanquetHall { get; private set; }
    public int TotalGuestsCount { get; private set; }
    public int AdultsCount { get; private set; }
    public int ChildrenCount { get; private set; }
    public int InfantsCount { get; private set; }
    public bool HasDog { get; private set; }
    public bool IsFirstTimeGuest { get; private set; }
    public string? AdminNotes { get; private set; }
    public string? FeedbackComment { get; private set; }

    private readonly List<SaunaOrder> _saunaOrders = new();
    public virtual IReadOnlyCollection<SaunaOrder> SaunaOrders => _saunaOrders.AsReadOnly();

    private Booking() { }

    public Booking(
        int guestId,
        DateTime arrival,
        DateTime departure,
        DateTime createdAt,
        ReservedUnits unit,
        int totalGuests,
        int adults,
        int children,
        int infants,
        bool hasDog,
        bool isFirstTimeGuest,
        bool needsSauna,
        bool needsBanquetHall,
        string? adminNotes = null,
        string? feedbackComment = null)
    {
        if (departure <= arrival) throw new Exception("Check-out must be after check-in");

        GuestId = guestId;
        ArrivalDate = arrival.Kind == DateTimeKind.Utc ? arrival : arrival.ToUniversalTime();
        DepartureDate = departure.Kind == DateTimeKind.Utc ? departure : departure.ToUniversalTime();
        CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime();
        ReservedUnit = unit;

        TotalGuestsCount = totalGuests;
        AdultsCount = adults;
        ChildrenCount = children;
        InfantsCount = infants;

        NeedsSauna = needsSauna;
        NeedsBanquetHall = needsBanquetHall;
        HasDog = hasDog;
        IsFirstTimeGuest = isFirstTimeGuest;
        AdminNotes = adminNotes;
        FeedbackComment = feedbackComment;
    }

    public void AddFeedback(string feedback)
    {
        FeedbackComment = feedback;
    }

    public void AddSaunaOrder(DateTime scheduledTime, int durationHours)
    {
        if (scheduledTime < ArrivalDate || scheduledTime > DepartureDate.AddDays(1))
            throw new Exception("Sauna time is outside of booking dates");

        _saunaOrders.Add(new SaunaOrder(scheduledTime, durationHours));
    }
}