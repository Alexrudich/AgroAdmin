namespace AgroAdmin.Shared.Dto.Telegram.Responses
{
    public class DailyAvailability
    {
        public DateTime Date { get; set; }
        public bool IsPondSideFree { get; set; }
        public bool IsParkingSideFree { get; set; }
        public bool IsWholeHouseFree { get; set; }

        // Полностью свободно - все три объекта свободны
        public bool IsFullyFree => IsPondSideFree && IsParkingSideFree && IsWholeHouseFree;

        // Полностью занято - нет свободных объектов (все три заняты)
        public bool IsFullyOccupied => !IsPondSideFree && !IsParkingSideFree && !IsWholeHouseFree;

        // Частично занято - свободен ровно один объект, остальные два заняты
        public bool IsPartiallyFree => !IsFullyFree && !IsFullyOccupied;
    }
}
