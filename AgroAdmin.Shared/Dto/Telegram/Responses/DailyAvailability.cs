namespace AgroAdmin.Shared.Dto.Telegram.Responses
{
    public class DailyAvailability
    {
        public DateTime Date { get; set; }
        public bool IsPondSideFree { get; set; }
        public bool IsParkingSideFree { get; set; }
        public bool IsWholeHouseFree { get; set; }

        // Вспомогательные свойства (можно вычислить в коде, не обязательно в JSON)
        public bool IsFullyFree => IsPondSideFree && IsParkingSideFree && IsWholeHouseFree;
        public bool IsPartiallyFree => (IsPondSideFree || IsParkingSideFree || IsWholeHouseFree) && !IsFullyFree;
        public bool IsFullyOccupied => !IsPondSideFree && !IsParkingSideFree && !IsWholeHouseFree;
    }
}
