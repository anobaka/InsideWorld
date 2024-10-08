﻿namespace Bakabase.Abstractions.Models.Domain.Constants;

[Flags]
public enum PropertyPool
{
    Internal = 1 << 0,
    Reserved = 1 << 1,
    Custom = 1 << 2,

    All = Internal | Reserved | Custom
}