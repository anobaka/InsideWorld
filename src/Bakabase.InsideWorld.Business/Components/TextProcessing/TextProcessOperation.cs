﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bakabase.InsideWorld.Business.Components.TextProcessing
{
    public enum TextProcessOperation
    {
        Remove = 1,
        SetWithFixedValue,
        AddToStart,
        AddToEnd,
        AddToAnyPosition,
        RemoveFromStart,
        RemoveFromEnd,
        RemoveFromAnyPosition,
        ReplaceFromStart,
        ReplaceFromEnd,
        ReplaceFromAnyPosition,
        ReplaceWithRegex,
    }
}