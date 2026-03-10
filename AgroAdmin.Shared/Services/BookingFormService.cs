using AgroAdmin.Shared.Dto;
using AgroAdmin.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Timer = System.Timers.Timer;

namespace AgroAdmin.Shared.Services;

public class BookingFormService : IDisposable
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly ILogger<BookingFormService> _logger;
    private Timer? _debounceTimer;
    private int? _currentBookingId;

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

    // Поиск по имени
    public string GuestNameSearchTerm { get; set; } = string.Empty;
    public List<GuestDto> NameSearchResults { get; set; } = new();
    public bool ShowNameDropdown { get; set; }

    // Поиск по телефону
    public string GuestPhoneSearchTerm { get; set; } = string.Empty;
    public List<GuestDto> PhoneSearchResults { get; set; } = new();
    public bool ShowPhoneDropdown { get; set; }

    // Общие поля
    public GuestDto? SelectedGuest { get; set; }
    public bool IsSearching { get; set; }

    public event Action? StateChanged;

    public async Task InitializeAsync(int? id, string uri)
    {
        _currentBookingId = id;

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
                GuestNameSearchTerm = result.Guest.FullName ?? string.Empty;
                GuestPhoneSearchTerm = result.Guest.Phone ?? string.Empty;
                Booking.Guest = result.Guest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки брони для редактирования {Id}", id);
        }
    }

    public void OnNameSearchInput(string value)
    {
        GuestNameSearchTerm = value;

        // Если есть выбранный гость и имя изменилось - сбрасываем
        if (SelectedGuest != null && value != SelectedGuest.FullName)
        {
            ClearGuest();
        }

        if (string.IsNullOrWhiteSpace(GuestNameSearchTerm) || GuestNameSearchTerm.Length < 2)
        {
            NameSearchResults.Clear();
            ShowNameDropdown = false;
            StateChanged?.Invoke();
            return;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(300);
        _debounceTimer.Elapsed += async (_, _) => await SearchByName();
        _debounceTimer.AutoReset = false;
        _debounceTimer.Start();
    }

    public void OnPhoneSearchInput(string value)
    {
        GuestPhoneSearchTerm = value;

        if (string.IsNullOrWhiteSpace(GuestPhoneSearchTerm) || GuestPhoneSearchTerm.Length < 3)
        {
            PhoneSearchResults.Clear();
            ShowPhoneDropdown = false;
            StateChanged?.Invoke();
            return;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(300);
        _debounceTimer.Elapsed += async (_, _) => await SearchByPhone();
        _debounceTimer.AutoReset = false;
        _debounceTimer.Start();
    }

    private async Task SearchByName()
    {
        if (string.IsNullOrWhiteSpace(GuestNameSearchTerm) || GuestNameSearchTerm.Length < 2)
            return;

        IsSearching = true;
        StateChanged?.Invoke();

        try
        {
            NameSearchResults = await _http.GetFromJsonAsync<List<GuestDto>>(
                $"api/guests/search?term={Uri.EscapeDataString(GuestNameSearchTerm)}") ?? new();
            ShowNameDropdown = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска гостей по имени");
        }
        finally
        {
            IsSearching = false;
            StateChanged?.Invoke();
        }
    }

    private async Task SearchByPhone()
    {
        if (string.IsNullOrWhiteSpace(GuestPhoneSearchTerm) || GuestPhoneSearchTerm.Length < 3)
            return;

        IsSearching = true;
        StateChanged?.Invoke();

        try
        {
            PhoneSearchResults = await _http.GetFromJsonAsync<List<GuestDto>>(
                $"api/guests/search?term={Uri.EscapeDataString(GuestPhoneSearchTerm)}") ?? new();
            ShowPhoneDropdown = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска гостей по телефону");
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
        GuestNameSearchTerm = guest.FullName;
        GuestPhoneSearchTerm = guest.Phone;
        ShowNameDropdown = false;
        ShowPhoneDropdown = false;

        if (Booking.Guest != null)
        {
            Booking.Guest.Id = guest.Id;
            Booking.Guest.FullName = guest.FullName;
            Booking.Guest.Phone = guest.Phone;
            Booking.Guest.Comment = guest.Comment;
        }

        StateChanged?.Invoke();
    }

    public void CreateNewGuestFromName()
    {
        if (Booking.Guest != null)
        {
            Booking.Guest.FullName = GuestNameSearchTerm;
            Booking.Guest.Phone = "";
        }

        SelectedGuest = null;
        ShowNameDropdown = false;
        StateChanged?.Invoke();
    }

    public void ClearGuest()
    {
        SelectedGuest = null;
        GuestNameSearchTerm = string.Empty;
        GuestPhoneSearchTerm = string.Empty;
        Booking.Guest = new GuestDto { FullName = "", Phone = "" };
        NameSearchResults.Clear();
        PhoneSearchResults.Clear();
        StateChanged?.Invoke();
    }

    public void HideNameDropdown()
    {
        ShowNameDropdown = false;
        StateChanged?.Invoke();
    }

    public void HidePhoneDropdown()
    {
        ShowPhoneDropdown = false;
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

        if (selectedSideBusy)
        {
            // Выбранная половинка занята - красный
            return "full-busy";
        }
        else
        {
            // Выбранная половинка свободна - проверяем другую
            bool otherSideBusy = Booking.ReservedUnit == ReservedUnits.PondSide ? parkingSideBusy : pondSideBusy;

            // Если другая половинка занята - желтый, если нет - зеленый
            return otherSideBusy ? "partial-busy" : "free";
        }
    }

    public ReservedUnits GetBusyUnitsOnDate(DateTime date)
    {
        var bookingsOnDate = AllBookings?.Where(b =>
                b.Id != (_currentBookingId ?? 0) &&
                date >= b.ArrivalDate.Date &&    // дата >= заезда
                date < b.DepartureDate.Date      // И дата < выезда
        ).ToList() ?? new();

        if (!bookingsOnDate.Any()) return 0;

        ReservedUnits busy = 0;

        // Если есть бронь на весь дом - возвращаем WholeHouse
        if (bookingsOnDate.Any(b => b.ReservedUnit == ReservedUnits.WholeHouse))
            return ReservedUnits.WholeHouse;

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