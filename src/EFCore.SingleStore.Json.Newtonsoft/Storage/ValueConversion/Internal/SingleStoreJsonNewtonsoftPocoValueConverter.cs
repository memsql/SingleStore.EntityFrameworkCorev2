﻿// Copyright (c) Pomelo Foundation. All rights reserved.
// Copyright (c) SingleStore Inc. All rights reserved.
// Licensed under the MIT. See LICENSE in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

// ReSharper disable once CheckNamespace
namespace EntityFrameworkCore.SingleStore.Json.Newtonsoft.Storage.ValueConversion.Internal
{
    public class SingleStoreJsonNewtonsoftPocoValueConverter<T> : ValueConverter<T, string>
    {
        public SingleStoreJsonNewtonsoftPocoValueConverter()
            : base(
                v => ConvertToProviderCore(v),
                v => ConvertFromProviderCore(v))
        {
        }

        private static string ConvertToProviderCore(T v)
            => JsonConvert.SerializeObject(v);

        private static T ConvertFromProviderCore(string v)
            => JsonConvert.DeserializeObject<T>(v);
    }
}
