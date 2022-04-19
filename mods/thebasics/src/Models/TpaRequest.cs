using System;

namespace thebasics.Models
{
    public class TpaRequest : IEquatable<TpaRequest>
    {
        public TpaRequestType Type;

        public string RequestPlayerUID;

        public string TargetPlayerUID;

        public double RequestTimeHours;

        public bool Equals(TpaRequest other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type && RequestPlayerUID == other.RequestPlayerUID && TargetPlayerUID == other.TargetPlayerUID && RequestTimeHours.Equals(other.RequestTimeHours);
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