using System;
using System.Collections.Generic;
using System.Text;

namespace AgroAdmin.Domain.Models
{
    public class SaunaOrder
    {
        public int Id { get; private set; }
        public int BookingId { get; internal set; }
        public DateTime ScheduledTime { get; private set; }
        public int DurationHours { get; private set; }

        private SaunaOrder() { }

        internal SaunaOrder(DateTime scheduledTime, int durationHours)
        {
            ScheduledTime = scheduledTime;
            DurationHours = durationHours;
        }
    }
}
