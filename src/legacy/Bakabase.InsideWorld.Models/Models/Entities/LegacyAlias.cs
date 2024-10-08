﻿using System;
using System.ComponentModel.DataAnnotations;

namespace Bakabase.InsideWorld.Models.Models.Entities
{
    [Obsolete]
    public class LegacyAlias
    {
        /// <summary>
        /// 无意义字段，不使用，因为EF不支持修改PK
        /// </summary>
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(256)] public string Name { set; get; } = null!;
        public int GroupId { set; get; }
        public bool IsPreferred { set; get; } = false;
    }
}