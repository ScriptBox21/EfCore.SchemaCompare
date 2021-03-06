﻿// Copyright (c) 2020 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace EfSchemaCompare.Internal
{
    internal class DatabaseIndexData
    {
        /// <summary>
        ///     The ordered list of columns that make up the index.
        /// </summary>
        public IList<DatabaseColumn> Columns;

        /// <summary>
        ///     Indicates whether or not the index constrains uniqueness.
        /// </summary>
        public bool IsUnique;

        /// <summary>The index name.</summary>
        public string Name;

        public DatabaseTable Table;

        public DatabaseIndexData(DatabaseIndex index)
        {
            Table = index.Table;
            Name = index.Name;
            Columns = index.Columns;
            IsUnique = index.IsUnique;
        }

        public DatabaseIndexData(DatabaseUniqueConstraint index)
        {
            Table = index.Table;
            Name = index.Name;
            Columns = index.Columns;
            IsUnique = true;
        }

        public static IEnumerable<DatabaseIndexData> GetIndexesAndUniqueConstraints(DatabaseTable table)
        {
            var result = table.Indexes.Select(x => new DatabaseIndexData(x)).ToList();
            result.AddRange(table.UniqueConstraints.Select(x => new DatabaseIndexData(x)));
            return result;
        }
    }
}