﻿using SQLite;
using System;
using System.Text.RegularExpressions;

// TODO: finish making database error logs
// TODO: throw after all error found.

namespace VotingPC
{
    internal static class GlobalVariable
    {
        public const int StringMaxLength = 128;
        public const int SmallStringMaxLength = 64;
    }

    public class Candidate
    {
        // Private fields
        private string name;
        private string gender;

        // Public field
        [Ignore]
        public ulong TotalWinningPlaces { get; set; }

        [NotNull, PrimaryKey, Unique]
        [Column("Name")]
        public string Name
        {
            get => name;
            // Cut the string if longer than StringMaxLength
            set
            {
                if (value.Length > GlobalVariable.StringMaxLength)
                {
                    name = value[..GlobalVariable.StringMaxLength];
                }
                else name = value;
            }
        }

        [NotNull]
        [Column("Votes")]
        public long Votes { get; set; }

        [NotNull]
        [Column("Gender")]
        public string Gender
        {
            get => gender;
            // Cut the string if longer than SmallStringMaxLength
            set
            {
                if (value.Length > GlobalVariable.SmallStringMaxLength)
                {
                    gender = value[..GlobalVariable.SmallStringMaxLength];
                }
                else gender = value;
            }
        }

        // Return true if required properties are not null, also reset votes to be safe
        [Ignore]
        public bool IsValid => Name != null && Gender != null;
    }

    [Table("Info")]
    public class Info
    {
        // Private fields
        private string sector;
        private string color;
        private string title;
        private string year;

        [Ignore]
        public string Error { get; private set; } = "";
        [Ignore]
        public string Warning { get; private set; } = "";

        [NotNull, PrimaryKey, Unique]
        [Column("Section")]
        public string Sector
        {
            get => sector;
            set
            {
                if (value.Length > GlobalVariable.SmallStringMaxLength)
                {
                    Error += $"Tên Section quá dài (hơn {GlobalVariable.SmallStringMaxLength} ký tự).\n";
                }
                else sector = value;
            }
        }

        [NotNull]
        [Column("Max")]
        public int? Max { get; set; }

        [NotNull]
        [Column("Color")]
        public string Color
        {
            get => color;
            set
            {
                value = value.Trim();
                // Is #RGB
                if (value.Length == 7)
                {
                    if (new Regex("^#(?:[0-9a-fA-F]{3}){1,2}$").Match(value).Success)
                    {
                        color = value;
                    }
                    else
                    {
                        Error += $"Màu nền RGB không hợp lệ tại Section {Sector}.\n";
                    }
                }
                // Is #ARGB
                else if (value.Length == 9)
                {
                    if (new Regex("^#(?:[0-9a-fA-F]{3,4}){1,2}$").Match(value).Success)
                    {
                        color = value;
                    }
                    else
                    {
                        Error += $"Màu nền ARGB không hợp lệ tại Section {Sector}.\n";
                    }
                }
                else
                {
                    Error += $"Màu nền không hợp lệ tại Section {Sector}.\nVui lòng kiểm tra lại độ dài mã màu.\n";
                }
            }
        }

        [NotNull]
        [Column("Title")]
        public string Title
        {
            get => title;
            set
            {
                if (value.Length > GlobalVariable.StringMaxLength)
                {
                    Warning += $"Tiêu đề Section {Sector} quá dài (hơn {GlobalVariable.StringMaxLength} ký tự). Đã tự động cắt.\n";
                    title = value.Substring(0, GlobalVariable.StringMaxLength);
                }
                else title = value;
            }
        }

        [NotNull]
        [Column("Year")]
        public string Year
        {
            get => year;
            set
            {
                if (value.Length > GlobalVariable.SmallStringMaxLength)
                {
                    Warning += $"Phụ đề niên khóa của Section {Sector} quá dài (hơn {GlobalVariable.SmallStringMaxLength} ký tự). Đã tự động cắt.\n";
                    year = value[..GlobalVariable.SmallStringMaxLength];
                }
                else year = value;
            }
        }

        [Ignore]
        public int TotalVoted { get; set; }

        [Ignore]
        // Return true if all properties are not null
        public bool IsValid => Sector != null && Color != null && Title != null && Year != null && Max != null;
    }
}
