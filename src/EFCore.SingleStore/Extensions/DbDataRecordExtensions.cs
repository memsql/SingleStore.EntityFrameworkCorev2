﻿// Copyright (c) Pomelo Foundation. All rights reserved.
// Copyright (c) SingleStore Inc. All rights reserved.
// Licensed under the MIT. See LICENSE in the project root for license information.

using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace System.Data.Common
{
    internal static class DbDataRecordExtensions
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public static T GetValueOrDefault<T>([NotNull] this DbDataRecord record, [NotNull] string name)
        {
            var idx = record.GetOrdinal(name);
            return record.IsDBNull(idx)
                ? default
                : (T)record.GetValue(idx);
        }
    }
}
