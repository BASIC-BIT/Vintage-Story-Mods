using System;

namespace thebasics.ModSystems.TPA.Models
{
    public class TpaRequest : IEquatable<TpaRequest>
    {
        public TpaRequestType Type;

        public string RequestPlayerUID;

        public string TargetPlayerUID;

        public double RequestTimeHours;

        /// <summary>
        /// Real-time timestamp when the request was made (DateTime.UtcNow.Ticks)
        /// </summary>
        public long RequestTimeRealTicks;

        /// <summary>
        /// Whether a temporal gear was consumed when this request was made
        /// </summary>
        public bool TemporalGearConsumed;

        public bool Equals(TpaRequest other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type && 
                   RequestPlayerUID == other.RequestPlayerUID && 
                   TargetPlayerUID == other.TargetPlayerUID && 
                   RequestTimeHours.Equals(other.RequestTimeHours) && 
                   RequestTimeRealTicks == other.RequestTimeRealTicks && 
                   TemporalGearConsumed == other.TemporalGearConsumed;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TpaRequest) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Type;
                hashCode = (hashCode * 397) ^ (RequestPlayerUID != null ? RequestPlayerUID.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (TargetPlayerUID != null ? TargetPlayerUID.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ RequestTimeHours.GetHashCode();
                hashCode = (hashCode * 397) ^ RequestTimeRealTicks.GetHashCode();
                hashCode = (hashCode * 397) ^ TemporalGearConsumed.GetHashCode();
                return hashCode;
            }
        }
    }

    public enum TpaRequestType
    {
        Goto, // tpa
        Bring, // tpahere
    }
}