﻿using Bakabase.Abstractions.Models.Input;
using Bakabase.InsideWorld.Models.Constants.Aos;
using Bakabase.Abstractions.Models.Db;
using Bakabase.Abstractions.Models.Domain;
using Resource = Bakabase.Abstractions.Models.Db.Resource;

namespace Bakabase.Abstractions.Extensions
{
    public static class ResourceExtensions
    {
        public static (Func<Resource, object> SelectKey, bool Asc, IComparer<object>? Comparer)[] BuildForSearch(
            this ResourceSearchOrderInputModel[]? orders)
        {
            var ordersForSearch = new List<(Func<Resource, object> SelectKey, bool Asc, IComparer<object>? Comparer)>();
            if (orders != null)
            {
                ordersForSearch.AddRange(from om in orders
                    let o = om.Property
                    let a = om.Asc
                    let s = ((Func<Resource, object> SelectKey, IComparer<object>? Comparer)) (o switch
                    {
                        ResourceSearchSortableProperty.AddDt => (x => x.CreateDt, null),
                        // ResourceSearchSortableProperty.Category => (x => x.CategoryId, null),
                        // ResourceSearchSortableProperty.MediaLibrary => (x => x.MediaLibraryId, null),
                        ResourceSearchSortableProperty.FileCreateDt => (x => x.FileCreateDt, null),
                        ResourceSearchSortableProperty.FileModifyDt => (x => x.FileModifyDt, null),
                        ResourceSearchSortableProperty.Filename => (x => Path.GetFileName(x.Path),
                            Comparer<object>.Create(StringComparer.OrdinalIgnoreCase.Compare)),
                        // ResourceSearchSortableProperty.ReleaseDt => (x => x.re),
                        _ => throw new ArgumentOutOfRangeException()
                    })
                    select (s.SelectKey, a, s.Comparer));
            }

            if (!ordersForSearch.Any())
            {
                ordersForSearch.Add((t => t.Id, false, null));
            }

            return ordersForSearch.ToArray();
        }
    }
}