﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bakabase.InsideWorld.Models.RequestModels
{
    public record ResourceCategoryDuplicateRequestModel
    {
        public string Name { get; set; } = null!;
    }
}
