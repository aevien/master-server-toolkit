using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;

namespace MasterServerToolkit.MasterServer
{
    public static partial class MstJsonExtensions
    {
        public static MstJson ToInferredSchema(this ObservableProfile profile)
        {
            var schema = CreateSchema("object", nameof(ObservableProfile));
            schema.AddField("additionalProperties", false);
            schema.AddField("properties", MstJson.CreateObject());

            if (profile == null)
                return schema;

            foreach (var property in profile)
            {
                string key = StringExtensions.FromHash(property.Key);
                schema["properties"].AddField(key, property.ToInferredSchema());
            }

            return schema;
        }

        public static MstJson ToInferredSchema(this IObservableProperty property)
        {
            if (property == null)
                return CreateSchema("null", string.Empty, false, "null_property");

            if (property is ObservableBool)
                return CreateSchema("boolean", nameof(ObservableBool));

            if (property is ObservableInt)
                return CreateSchema("integer", nameof(ObservableInt), true, null, "int32");

            if (property is ObservableLong)
                return CreateSchema("integer", nameof(ObservableLong), true, null, "int64");

            if (property is ObservableFloat)
                return CreateSchema("number", nameof(ObservableFloat), true, null, "float");

            if (property is ObservableDouble)
                return CreateSchema("number", nameof(ObservableDouble), true, null, "double");

            if (property is ObservableString)
                return CreateSchema("string", nameof(ObservableString));

            if (property is ObservableDateTime)
                return CreateSchema("string", nameof(ObservableDateTime), true, null, "date-time");

            if (property is ObservableListInt)
                return CreateTypedArraySchema(nameof(ObservableListInt), "integer", "int32", property.ToJson());

            if (property is ObservableListFloat)
                return CreateTypedArraySchema(nameof(ObservableListFloat), "number", "float", property.ToJson());

            if (property is ObservableListString)
                return CreateTypedArraySchema(nameof(ObservableListString), "string", null, property.ToJson());

            if (property is ObservableDictStringInt)
                return CreateTypedObjectSchema(nameof(ObservableDictStringInt), "integer", "int32", property.ToJson());

            if (property is ObservableDictStringFloat)
                return CreateTypedObjectSchema(nameof(ObservableDictStringFloat), "number", "float", property.ToJson());

            if (property is ObservableDictionaryString)
                return CreateTypedObjectSchema(nameof(ObservableDictionaryString), "string", null, property.ToJson());

            if (property is ObservableDictionaryInt)
                return CreateTypedObjectSchema(nameof(ObservableDictionaryInt), "integer", "int32", property.ToJson(), "integer");

            return property.ToJson().ToInferredSchema(property.GetType().Name);
        }

        public static MstJson ToInferredSchema(this MstJson json)
        {
            return ToInferredSchema(json, string.Empty);
        }

        public static MstJson ToInferredSchema(this MstJson json, string sourceType)
        {
            if (json == null || json.IsNull)
                return CreateSchema("null", sourceType, false, "null_value");

            if (json.IsBool)
                return CreateSchema("boolean", sourceType);

            if (json.IsString)
                return CreateSchema("string", sourceType);

            if (json.IsNumber)
                return CreateSchema(json.IsInteger ? "integer" : "number", sourceType, true, null, json.IsInteger ? "int64" : "double");

            if (json.IsArray)
                return CreateArraySchema(json, sourceType);

            if (json.IsObject)
                return CreateObjectSchema(json, sourceType);

            return CreateSchema("unknown", sourceType, false, "unsupported_json_type");
        }

        private static MstJson CreateObjectSchema(MstJson json, string sourceType)
        {
            var schema = CreateSchema("object", sourceType);
            schema.AddField("additionalProperties", false);
            schema.AddField("properties", MstJson.CreateObject());

            if (json.Keys == null)
                return schema;

            foreach (string key in json.Keys)
            {
                schema["properties"].AddField(key, json[key].ToInferredSchema());
            }

            return schema;
        }

        private static MstJson CreateArraySchema(MstJson json, string sourceType)
        {
            var schema = CreateSchema("array", sourceType);
            schema.AddField("originalLength", json.Count);
            schema.AddField("allowAdd", true);
            schema.AddField("allowRemove", true);

            if (json.Count == 0)
            {
                var anyItemSchema = CreateSchema("unknown", string.Empty);
                anyItemSchema.AddField("allowAny", true);

                schema.AddField("itemSchemas", MstJson.CreateArray());
                schema.AddField("items", anyItemSchema);
                return schema;
            }

            MstJson itemSchema = null;
            MstJson itemSchemas = MstJson.CreateArray();
            bool mixed = false;

            foreach (MstJson item in json)
            {
                var currentItemSchema = item.ToInferredSchema();
                itemSchemas.Add(currentItemSchema);

                if (itemSchema == null)
                {
                    itemSchema = currentItemSchema;
                    continue;
                }

                if (!HasSameSchemaKind(itemSchema, currentItemSchema))
                {
                    mixed = true;
                }
            }

            if (mixed)
            {
                schema.SetField("editable", false);
                schema.AddField("reason", "mixed_array");
                schema.AddField("itemSchemas", itemSchemas);
                schema.AddField("items", CreateSchema("mixed", string.Empty, false, "mixed_array"));
                return schema;
            }

            schema.AddField("itemSchemas", itemSchemas);
            schema.AddField("items", itemSchema ?? CreateSchema("unknown", string.Empty, false, "empty_array"));
            return schema;
        }

        private static MstJson CreateTypedArraySchema(string sourceType, string itemType, string itemFormat, MstJson value)
        {
            var schema = CreateSchema("array", sourceType);
            schema.AddField("originalLength", value?.Count ?? 0);
            schema.AddField("allowAdd", true);
            schema.AddField("allowRemove", true);
            schema.AddField("items", CreateSchema(itemType, sourceType, true, null, itemFormat));
            return schema;
        }

        private static MstJson CreateTypedObjectSchema(string sourceType, string valueType, string valueFormat, MstJson value, string keyFormat = null)
        {
            var schema = CreateSchema("object", sourceType);
            schema.AddField("additionalProperties", true);
            schema.AddField("allowAdd", true);
            schema.AddField("allowRemove", true);
            schema.AddField("properties", MstJson.CreateObject());
            schema.AddField("keyType", string.IsNullOrEmpty(keyFormat) ? "string" : keyFormat);
            schema.AddField("valueSchema", CreateSchema(valueType, sourceType, true, null, valueFormat));

            if (value?.Keys == null)
                return schema;

            foreach (string key in value.Keys)
            {
                schema["properties"].AddField(key, CreateSchema(valueType, sourceType, true, null, valueFormat));
            }

            return schema;
        }

        private static bool HasSameSchemaKind(MstJson left, MstJson right)
        {
            if (left == null || right == null)
                return false;

            string leftType = left["type"]?.StringValue ?? string.Empty;
            string rightType = right["type"]?.StringValue ?? string.Empty;

            if (leftType != rightType)
                return false;

            string leftFormat = left["format"]?.StringValue ?? string.Empty;
            string rightFormat = right["format"]?.StringValue ?? string.Empty;

            if (leftFormat != rightFormat)
                return false;

            if (leftType == "object")
            {
                MstJson leftProperties = left.HasField("properties") ? left["properties"] : MstJson.CreateObject();
                MstJson rightProperties = right.HasField("properties") ? right["properties"] : MstJson.CreateObject();

                if (leftProperties.Count != rightProperties.Count)
                    return false;

                foreach (string key in leftProperties.Keys)
                {
                    if (!rightProperties.HasField(key) || !HasSameSchemaKind(leftProperties[key], rightProperties[key]))
                        return false;
                }
            }

            if (leftType == "array")
            {
                MstJson leftItems = left.HasField("items") ? left["items"] : null;
                MstJson rightItems = right.HasField("items") ? right["items"] : null;

                if (leftItems == null || rightItems == null)
                    return leftItems == rightItems;

                return HasSameSchemaKind(leftItems, rightItems);
            }

            return true;
        }

        public static bool TryValidateAgainstInferredSchema(this MstJson value, MstJson schema, out string error)
        {
            return TryValidateAgainstInferredSchema(value, schema, "value", out error);
        }

        private static bool TryValidateAgainstInferredSchema(MstJson value, MstJson schema, string path, out string error)
        {
            error = null;

            if (schema == null || !schema.IsObject)
            {
                error = $"{path} schema is missing";
                return false;
            }

            if (path == "value" && schema.HasField("editable") && !schema["editable"].BoolValue)
            {
                error = $"{path} is not editable";
                return false;
            }

            string type = schema.HasField("type") ? schema["type"].StringValue : "unknown";

            if (type == "unknown" && schema.HasField("allowAny") && schema["allowAny"].BoolValue)
                return true;

            switch (type)
            {
                case "boolean":
                    return ValidateJsonKind(value, value != null && value.IsBool, path, "boolean", out error);

                case "integer":
                    if (!ValidateJsonKind(value, value != null && value.IsNumber && value.IsInteger, path, "integer", out error))
                        return false;

                    if (schema.HasField("format") && schema["format"].StringValue == "int32" &&
                        (value.LongValue < int.MinValue || value.LongValue > int.MaxValue))
                    {
                        error = $"{path} must be a 32-bit integer";
                        return false;
                    }

                    return true;

                case "number":
                    return ValidateJsonKind(value, value != null && value.IsNumber, path, "number", out error);

                case "string":
                    return ValidateJsonKind(value, value != null && value.IsString, path, "string", out error);

                case "object":
                    return TryValidateObject(value, schema, path, out error);

                case "array":
                    return TryValidateArray(value, schema, path, out error);

                case "null":
                    return ValidateJsonKind(value, value == null || value.IsNull, path, "null", out error);

                default:
                    error = $"{path} has unsupported schema type '{type}'";
                    return false;
            }
        }

        private static bool TryValidateObject(MstJson value, MstJson schema, string path, out string error)
        {
            error = null;

            if (!ValidateJsonKind(value, value != null && value.IsObject, path, "object", out error))
                return false;

            MstJson properties = schema.HasField("properties") ? schema["properties"] : MstJson.CreateObject();
            bool allowAdditionalProperties = schema.HasField("additionalProperties") && schema["additionalProperties"].BoolValue;

            if (value.Keys == null)
                return true;

            if (!allowAdditionalProperties)
            {
                foreach (string key in properties.Keys)
                {
                    if (!value.HasField(key))
                    {
                        error = $"{path}.{key} is missing";
                        return false;
                    }
                }
            }

            foreach (string key in value.Keys)
            {
                if (allowAdditionalProperties && schema.HasField("keyType") && schema["keyType"].StringValue == "integer" && !int.TryParse(key, out _))
                {
                    error = $"{path}.{key} key must be an integer";
                    return false;
                }

                if (!properties.HasField(key))
                {
                    if (!allowAdditionalProperties)
                    {
                        error = $"{path}.{key} is not allowed";
                        return false;
                    }

                    MstJson valueSchema = schema.HasField("valueSchema")
                        ? schema["valueSchema"]
                        : CreateSchema("unknown", string.Empty, false, "missing_value_schema");

                    if (!TryValidateAgainstInferredSchema(value[key], valueSchema, $"{path}.{key}", out error))
                        return false;

                    continue;
                }

                if (!TryValidateAgainstInferredSchema(value[key], properties[key], $"{path}.{key}", out error))
                    return false;
            }

            return true;
        }

        private static bool TryValidateArray(MstJson value, MstJson schema, string path, out string error)
        {
            error = null;

            if (!ValidateJsonKind(value, value != null && value.IsArray, path, "array", out error))
                return false;

            for (int i = 0; i < value.Count; i++)
            {
                var itemSchema = GetArrayItemSchema(schema, i);

                if (!TryValidateAgainstInferredSchema(value[i], itemSchema, $"{path}[{i}]", out error))
                    return false;
            }

            return true;
        }

        private static MstJson GetArrayItemSchema(MstJson schema, int index)
        {
            if (schema.HasField("itemSchemas") && schema["itemSchemas"].IsArray && schema["itemSchemas"].Count > index)
                return schema["itemSchemas"][index];

            return schema.HasField("items")
                ? schema["items"]
                : CreateSchema("unknown", string.Empty, false, "missing_item_schema");
        }

        private static bool ValidateJsonKind(MstJson value, bool isValid, string path, string expectedType, out string error)
        {
            error = null;

            if (isValid)
                return true;

            error = $"{path} must be {expectedType}";
            return false;
        }

        private static MstJson CreateSchema(string type, string sourceType, bool editable = true, string reason = null, string format = null)
        {
            var schema = MstJson.CreateObject();
            schema.AddField("type", type ?? "unknown");
            schema.AddField("editable", editable);

            if (!string.IsNullOrEmpty(sourceType))
                schema.AddField("sourceType", sourceType);

            if (!string.IsNullOrEmpty(format))
                schema.AddField("format", format);

            if (!string.IsNullOrEmpty(reason))
                schema.AddField("reason", reason);

            return schema;
        }
    }
}
