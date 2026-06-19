using AgroAdmin.Shared.Dto.Bookings.Requests;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Dto.Guests;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;
using AgroAdmin.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Timer = System.Timers.Timer;

namespace AgroAdmin.Shared.Services;

public class BookingFormService(
    HttpClient http,
    NavigationManager nav,
    ILogger<BookingFormService> logger)
    : IDisposable
{
    private readonly NavigationManager _nav = nav;
    private Timer? _debounceTimer;
    private int? _currentBookingId;
    private bool _isRefreshing = false;

    // Состояние
    public CreateBookingDto Booking { get; set; } = new() { Guest = new GuestDto(), ArrivalDate = DateTime.Today.AddHours(14), DepartureDate = DateTime.Today.AddDays(1).AddHours(14) };
    public List<BookingDto> AllBookings { get; set; } = new();

    // Поиск по имени
    public string GuestNameSearchTerm { get; set; } = string.Empty;
    public List<GuestDto> NameSearchResults { get; set; } = new();
    public bool ShowNameDropdown { get; set; }

    // Поиск по телефону
    public string GuestPhoneSearchTerm { get; set; } = string.Empty;
    public List<GuestDto> PhoneSearchResults { get; set; } = new();
    public bool ShowPhoneDropdown { get; set; }

    // Текущий расчет цены
    public PricingResponseDto? CurrentPricing { get; private set; }

    // Общие поля
    public GuestDto? SelectedGuest { get; set; }
    public bool IsSearching { get; set; }

    public event Action? StateChanged;

    public async Task InitializeAsync(int? id, string uri)
    {
        _currentBookingId = id;

        try
        {
            var result = await http.GetFromJsonAsync<PagedResultDto<BookingDto>>("api/bookings");
            AllBookings = result?.Items ?? new();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка загрузки броней");
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

    public async Task RefreshPricingAsync()
    {
        if (_isRefreshing) return;

        _isRefreshing = true;

        try
        {
            if (Booking.ArrivalDate == default || Booking.DepartureDate == default)
                return;

            var request = new PricingRequestDto
            {
                ArrivalDate = Booking.ArrivalDate,
                DepartureDate = Booking.DepartureDate,
                ReservedUnit = Booking.ReservedUnit,
                AdultsCount = Booking.AdultsCount,
                ChildrenCount = Booking.ChildrenCount,
                InfantsCount = Booking.InfantsCount,
                NeedsSauna = Booking.NeedsSauna,
                NeedsBanquetHall = Booking.NeedsBanquetHall,
                HasDog = Booking.HasDog
            };

            var response = await http.PostAsJsonAsync("api/pricing/calculate", request);
            if (response.IsSuccessStatusCode)
            {
                CurrentPricing = await response.Content.ReadFromJsonAsync<PricingResponseDto>();
                StateChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при расчете цены");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task LoadBookingForEdit(int id)
    {
        try
        {
            var result = await http.GetFromJsonAsync<BookingDto>($"api/bookings/{id}");
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
            Booking.AccommodationCost = result.AccommodationCost;
            Booking.CheckInTime = result.CheckInTime;

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
            logger.LogError(ex, "Ошибка загрузки брони для редактирования {Id}", id);
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
            NameSearchResults = await http.GetFromJsonAsync<List<GuestDto>>(
                $"api/guests/search?term={Uri.EscapeDataString(GuestNameSearchTerm)}") ?? new();
            ShowNameDropdown = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка поиска гостей по имени");
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
            PhoneSearchResults = await http.GetFromJsonAsync<List<GuestDto>>(
                $"api/guests/search?term={Uri.EscapeDataString(GuestPhoneSearchTerm)}") ?? new();
            ShowNameDropdown = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка поиска гостей по телефону");
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

    public void SyncGuestFromSearch()
    {
        if (SelectedGuest != null ||
            string.IsNullOrWhiteSpace(GuestNameSearchTerm) ||
            string.IsNullOrWhiteSpace(GuestPhoneSearchTerm)) return;
        Booking.Guest?.FullName = GuestNameSearchTerm;
        Booking.Guest?.Phone = GuestPhoneSearchTerm;
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

    public async Task OnGuestCompositionChanged()
    {
        UpdateTotal();
        await RefreshPricingAsync();
    }

    public async Task OnOptionsChanged()
    {
        await RefreshPricingAsync();
    }

    public async Task OnDateOrUnitChanged()
    {
        if (Booking.DepartureDate <= Booking.ArrivalDate)
        {
            Booking.DepartureDate = Booking.ArrivalDate.AddDays(1);
        }
        await RefreshPricingAsync();
        StateChanged?.Invoke();
    }

    public async Task<GuestDto?> GetGuestAsync(int id)
    {
        try
        {
            return await http.GetFromJsonAsync<GuestDto>($"api/guests/{id}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка загрузки гостя {Id}", id);
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
                response = await http.PutAsJsonAsync($"api/guests/{id}", guest);
            }
            else
            {
                response = await http.PostAsJsonAsync("api/guests", guest);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка сохранения гостя");
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
                    FeedbackComment = Booking.FeedbackComment,
                    AccommodationCost = Booking.AccommodationCost,
                    CheckInTime = Booking.CheckInTime
                };
                response = await http.PutAsJsonAsync($"api/bookings/{id}", updateDto);
            }
            else
            {
                response = await http.PostAsJsonAsync("api/bookings", Booking);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при сохранении брони");
            return false;
        }
    }

    public async Task<BookingValidationResult?> ValidateAsync(CreateBookingDto booking)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"api/bookings/validate?bookingId={_currentBookingId}", booking);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BookingValidationResult>();
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Validation error");
            return null;
        }
    }

    public void Dispose() => _debounceTimer?.Dispose();
}