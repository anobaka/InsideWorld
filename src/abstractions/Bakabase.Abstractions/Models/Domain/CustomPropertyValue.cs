﻿using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.InsideWorld.Models.RequestModels;
using Newtonsoft.Json;

namespace Bakabase.Abstractions.Models.Domain
{
    public record CustomPropertyValue
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public int ResourceId { get; set; }
        public CustomProperty? Property { get; set; }
        public virtual object? Value { get; set; }
        public int Scope { get; set; }

        public string BizKey => $"{ResourceId}-{PropertyId}-{Scope}";

        private sealed class CustomPropertyValueBizKeyComparer : IEqualityComparer<CustomPropertyValue>
        {
            public bool Equals(CustomPropertyValue? x, CustomPropertyValue? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                return (x.Id > 0 && x.Id == y.Id) || x.BizKey == y.BizKey;
            }

            public int GetHashCode(CustomPropertyValue obj)
            {
                return obj.Id > 0 ? obj.Id : HashCode.Combine(obj.PropertyId, obj.ResourceId, obj.Scope);
            }
        }

        public static IEqualityComparer<CustomPropertyValue> BizKeyComparer { get; } =
            new CustomPropertyValueBizKeyComparer();
    }


    public abstract record CustomPropertyValue<TValue> : CustomPropertyValue
    {
        private TValue? _typedValue;

        public TValue? TypedValue
        {
            get => _typedValue;
            set
            {
                _typedValue = value;
                Value = value;
            }
        }

        public override object? Value => TypedValue;
    }
}