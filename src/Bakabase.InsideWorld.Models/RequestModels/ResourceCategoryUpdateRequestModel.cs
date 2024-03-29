﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Models.Aos;
using Bakabase.InsideWorld.Models.Models.Dtos;
using Bakabase.InsideWorld.Models.Models.Entities;

namespace Bakabase.InsideWorld.Models.RequestModels
{
    public class ResourceCategoryUpdateRequestModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public CoverSelectOrder? CoverSelectionOrder { get; set; }
        public int? Order { get; set; }
        public bool? GenerateNfo { get; set; }
    }
}