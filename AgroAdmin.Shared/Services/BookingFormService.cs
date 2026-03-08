using AgroAdmin.Shared.Dto;
using AgroAdmin.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AgroAdmin.Shared.Services;

public class BookingFormService : IDisposable
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly ILogger<BookingFormService> _logger;
    private Timer? _debounceTimer;
    private int? _currentBookingId; // 👈 Добавили поле

    public BookingFormService(
        HttpClient http,
        NavigationManager nav,
        ILogger<BookingFormService> logger)
    {
        _http = http;
        _nav = nav;
        _logger = logger;
    }

    // Состояние
    public CreateBookingDto Booking { get; set; } = new() { Guest = new GuestDto() };
    public List<BookingDto> AllBookings { get; set; } = new();

    // Поиск гостей
    public string GuestSearchTerm { get; set; } = string.Empty;
    public List<GuestDto> SearchResults { get; set; } = new();
    public GuestDto? SelectedGuest { get; set; }
    public bool ShowGuestDropdown { get; set; }
    public bool IsSearching { get; set; }

    public event Action? StateChanged;

    public async Task InitializeAsync(int? id, string uri)
    {
        _currentBookingId = id; // 👈 Сохраняем ID редактируемой брони

        try
        {
            AllBookings = await _http.GetFromJsonAsync<List<BookingDto>>("api/bookings") ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки броней");
            AllBookings = new();
        }

        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);

        if (query.TryGetValue("start", out var startVal))
        {
            Booking.ArrivalDate = DateTime.Parse(startVal!);
            Booking.DepartureDate = Booking.ArrivalDate.AddDays(1);
        }

        if (query.TryGetValue("unit", out var unitVal) && int.TryParse(unitVal, out var unit))
        {
            Booking.ReservedUnit = (ReservedUnits)unit;
        }

        if (id.HasValue)
        {
            await LoadBookingForEdit(id.Value);
        }

        StateChanged?.Invoke();
    }

    private async Task LoadBookingForEdit(int id)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<BookingDto>($"api/bookings/{id}");
            if (result == null) return;

            Booking.ArrivalDate = result.ArrivalDate;
            Booking.DepartureDate = result.DepartureDate;
            Booking.ReservedUnit = result.ReservedUnit;
            Booking.TotalGuestsCount = result.TotalGuestsCount;
            Booking.AdultsCount = result.AdultsCount;
            Booking.ChildrenCount = result.ChildrenCount;
            Booking.InfantsCount = result.InfantsCount;
            Booking.HasDog = result.HasDog;
            Booking.NeedsSauna = result.NeedsSauna;
            Booking.NeedsBanquetHall = result.NeedsBanquetHall;
            Booking.IsFirstTimeGuest = result.IsFirstTimeGuest;
            Booking.AdminNotes = result.AdminNotes;
            Booking.FeedbackComment = result.FeedbackComment;

            if (result.Guest != null)
            {
                SelectedGuest = result.Guest;
                GuestSearchTerm = result.Guest.FullName ?? string.Empty;
                Booking.Guest = result.Guest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки брони для редактирования {Id}", id);
        }
    }

    public void OnSearchInput(string value)
    {
        GuestSearchTerm = value;

        if (string.IsNullOrWhiteSpace(GuestSearchTerm) || GuestSearchTerm.Length < 2)
        {
            SearchResults.Clear();
            ShowGuestDropdown = false;
            StateChanged?.Invoke();
            return;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(300);
        _debounceTimer.Elapsed += async (_, _) => await SearchGuests();
        _debounceTimer.AutoReset = false;
        _debounceTimer.Start();
    }

    private async Task SearchGuests()
    {
        if (string.IsNullOrWhiteSpace(GuestSearchTerm) || GuestSearchTerm.Length < 2)
            return;

        IsSearching = true;
        StateChanged?.Invoke();

        try
        {
            SearchResults = await _http.GetFromJsonAsync<List<GuestDto>>(
                $"api/guests/search?term={Uri.EscapeDataString(GuestSearchTerm)}") ?? new();
            ShowGuestDropdown = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска гостей");
        }
        finally
        {
            IsSearching = false;
            StateChanged?.Invoke();
        }
    }

    public void SelectGuest(GuestDto guest)
    {
        SelectedGuest = guest;
        GuestSearchTerm = guest.FullName;
        ShowGuestDropdown = false;

        Booking.Guest?.Id = guest.Id;
        Booking.Guest?.FullName = guest.FullName;
        Booking.Guest?.Phone = guest.Phone;
        Booking.Guest?.Comment = guest.Comment;

        StateChanged?.Invoke();
    }

    public void ClearGuest()
    {
        SelectedGuest = null;
        GuestSearchTerm = string.Empty;
        Booking.Guest = new GuestDto { FullName = "", Phone = "" };
        SearchResults.Clear();
        StateChanged?.Invoke();
    }

    public void HideDropdown()
    {
        ShowGuestDropdown = false;
        StateChanged?.Invoke();
    }

    public void UpdateTotal()
    {
        Booking.TotalGuestsCount = Booking.AdultsCount + Booking.ChildrenCount + Booking.InfantsCount;
        StateChanged?.Invoke();
    }

    public string GetDateStatus(DateTime date)
    {
        if (AllBookings == null || !AllBookings.Any()) return "free";

        var bookingsOnDate = AllBookings.Where(b =>
            b.Id != (_currentBookingId ?? 0) &&
            date >= b.ArrivalDate.Date &&
            date < b.DepartureDate.Date
        ).ToList();

        if (!bookingsOnDate.Any()) return "free";

        // Проверяем, есть ли бронь на весь дом
        bool hasWholeHouse = bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.WholeHouse);

        // Если есть бронь на весь дом - всё красное для любого выбора
        if (hasWholeHouse) return "full-busy";

        // Проверяем, какие половинки заняты
        bool pondSideBusy = bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.PondSide);
        bool parkingSideBusy = bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.ParkingSide);

        // Если заняты обе половинки - красный для любого выбора
        if (pondSideBusy && parkingSideBusy) return "full-busy";

        // Для выбора всего дома - красный если занята хотя бы одна половинка
        if (Booking.ReservedUnit == ReservedUnits.WholeHouse)
        {
            return (pondSideBusy || parkingSideBusy) ? "full-busy" : "free";
        }

        // Для выбора половинки
        bool selectedSideBusy = Booking.ReservedUnit == ReservedUnits.PondSide ? pondSideBusy : parkingSideBusy;

        if (!selectedSideBusy)
        {
            // Выбранная половинка свободна - зеленая ячейка
            return "free";
        }
        else
        {
            // Выбранная половинка занята - проверяем вторую
            bool otherSideBusy = Booking.ReservedUnit == ReservedUnits.PondSide ? parkingSideBusy : pondSideBusy;

            // Если вторая свободна - желтый (можно выбрать другую половинку)
            // Если вторая тоже занята - красный
            return otherSideBusy ? "full-busy" : "partial-busy";
        }
    }

    public ReservedUnits GetBusyUnitsOnDate(DateTime date)
    {
        var bookingsOnDate = AllBookings?.Where(b =>
            b.Id != (_currentBookingId ?? 0) &&
            date >= b.ArrivalDate.Date &&
            date < b.DepartureDate.Date
        ).ToList() ?? new();

        if (!bookingsOnDate.Any()) return 0;

        ReservedUnits busy = 0;

        // Если есть бронь на весь дом - обе половинки заняты
        if (bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.WholeHouse))
            return ReservedUnits.PondSide | ReservedUnits.ParkingSide;

        // Проверяем занятость половинок
        if (bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.PondSide))
            busy |= ReservedUnits.PondSide;

        if (bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.ParkingSide))
            busy |= ReservedUnits.ParkingSide;

        return busy;
    }

    public async Task<GuestDto?> GetGuestAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<GuestDto>($"api/guests/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки гостя {Id}", id);
            return null;
        }
    }

    public async Task<bool> SaveGuestAsync(GuestDto guest, int? id)
    {
        try
        {
            HttpResponseMessage response;

            if (id.HasValue)
            {
                response = await _http.PutAsJsonAsync($"api/guests/{id}", guest);
            }
            else
            {
                response = await _http.PostAsJsonAsync("api/guests", guest);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения гостя");
            return false;
        }
    }

    public async Task<bool> SubmitAsync(int? id)
    {
        UpdateTotal();

        try
        {
            HttpResponseMessage response;

            if (id.HasValue)
            {
                var updateDto = new UpdateBookingDto
                {
                    Id = id.Value,
                    Guest = Booking.Guest,
                    ArrivalDate = Booking.ArrivalDate,
                    DepartureDate = Booking.DepartureDate,
                    ReservedUnit = Booking.ReservedUnit,
                    TotalGuestsCount = Booking.TotalGuestsCount,
                    AdultsCount = Booking.AdultsCount,
                    ChildrenCount = Booking.ChildrenCount,
                    InfantsCount = Booking.InfantsCount,
                    HasDog = Booking.HasDog,
                    NeedsSauna = Booking.NeedsSauna,
                    NeedsBanquetHall = Booking.NeedsBanquetHall,
                    IsFirstTimeGuest = Booking.IsFirstTimeGuest,
                    AdminNotes = Booking.AdminNotes,
                    FeedbackComment = Booking.FeedbackComment
                };
                response = await _http.PutAsJsonAsync($"api/bookings/{id}", updateDto);
            }
            else
            {
                response = await _http.PostAsJsonAsync("api/bookings", Booking);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении брони");
            return false;
        }
    }

    public void Dispose() => _debounceTimer?.Dispose();
}