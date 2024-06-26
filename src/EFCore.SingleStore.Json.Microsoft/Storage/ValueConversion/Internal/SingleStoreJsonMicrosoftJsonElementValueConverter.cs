﻿using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// ReSharper disable once CheckNamespace
namespace EntityFrameworkCore.SingleStore.Json.Microsoft.Storage.ValueConversion.Internal
{
    public class SingleStoreJsonMicrosoftJsonElementValueConverter : ValueConverter<JsonElement, string>
    {
        public SingleStoreJsonMicrosoftJsonElementValueConverter()
            : base(
                v => ConvertToProviderCore(v),
                v => ConvertFromProviderCore(v))
        {
        }

        private static string ConvertToProviderCore(JsonElement v)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            v.WriteTo(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static JsonElement ConvertFromProviderCore(string v)
            => JsonDocument.Parse(v).RootElement;
    }
}
