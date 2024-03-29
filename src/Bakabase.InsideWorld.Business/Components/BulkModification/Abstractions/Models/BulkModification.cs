﻿using System;
using Bakabase.InsideWorld.Business.Components.BulkModification.Abstractions.Models.Constants;

namespace Bakabase.InsideWorld.Business.Components.BulkModification.Abstractions.Models
{
    public record BulkModification
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public BulkModificationStatus Status { get; set; } = BulkModificationStatus.Processing;
        public string? Filter { get; set; }
        public string? Processes { get; set; }
        public string? Variables { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? FilteredAt { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public DateTime? AppliedAt { get; set; }
        public DateTime? RevertedAt { get; set; }
    }
}