﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bakabase.InsideWorld.Business.Components.BulkModification.Abstractions.Models.Constants;
using Bakabase.InsideWorld.Business.Components.BulkModification.ExpressionBuilders.Infrastructures;
using Bakabase.InsideWorld.Models.Models.Dtos;

namespace Bakabase.InsideWorld.Business.Components.BulkModification.ExpressionBuilders
{
    public class BmFileNameFilterExpressionBuilder : BmSingleValuePropertyFilterExpressionBuilder<string>
    {
        public static BmFileNameFilterExpressionBuilder Instance = new();

        protected override BulkModificationProperty Property => BulkModificationProperty.FileName;
        protected override string? GetValue(ResourceDto resource)
        {
            return resource.RawName;
        }
    }
}
